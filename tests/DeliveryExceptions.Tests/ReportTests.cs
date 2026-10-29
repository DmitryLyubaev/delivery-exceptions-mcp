using System.Diagnostics;
using DeliveryExceptions.Tests.Support;
using Xunit;

namespace DeliveryExceptions.Tests;

[Trait("Category", "Report")]
public sealed class ReportTests
{
    private static async Task RunAsync(string assertions)
    {
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("$ErrorActionPreference='Stop'; Import-Module ./scripts/evidence.psm1 -Force; " +
            "$identity=@{source=('a'*40);tree=('b'*40);fixture=('c'*64);config=('d'*64)}; " +
            "$cases=@(1..12 | ForEach-Object { @{id=('A'+$_);status='pass';identity=$identity;provenance='live-local'} }); " + assertions);
        start.WorkingDirectory = DemoStack.Root;
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await stdout + await stderr);
    }

    [Fact] public Task AllTwelveCasesRequired() => RunAsync("foreach($bad in @(@($cases[0..10]),@($cases+$cases[0]))) { $rejected=$false; try { New-AcceptanceReport $identity $bad @{passed=$true} | Out-Null } catch { $rejected=$true }; if(!$rejected){throw 'Incomplete/duplicate report accepted'} }; $changed=@{}+$identity; $changed.config='e'*64; $cases[3].identity=$changed; $rejected=$false; try { New-AcceptanceReport $identity $cases @{passed=$true}|Out-Null } catch {$rejected=$true}; if(!$rejected){throw 'Mismatch accepted'}");
    [Fact] public Task PartialRunKeepsFailureAndNotRun() => RunAsync("$cases[4].status='fail'; $cases[5].status='not_run'; $report=New-AcceptanceReport $identity $cases @{passed=$true} 1; if($report.accepted -or $report.cases[4].status -ne 'fail' -or $report.cases[5].status -ne 'not_run'){throw 'Failure erased'}; $script:cleaned=$false; try { Invoke-EvidenceRun {throw 'injected'} {$script:cleaned=$true} } catch {}; if(!$script:cleaned){throw 'Cleanup bypassed'}; $report=New-AcceptanceReport $identity @($cases|ForEach-Object {$_.status='pass';$_}) @{passed=$false}; if($report.accepted){throw 'Cleanup failure accepted'}");
    [Fact] public Task PublishedProjectionHasNoSecrets() => RunAsync("$cases[0].token='secret-marker'; $cases[1].exception='secret-marker'; $identity.password='secret-marker'; $report=New-AcceptanceReport $identity $cases @{passed=$true;stdout='secret-marker'}; $json=$report|ConvertTo-Json -Depth 20; if($json.Contains('secret-marker')){throw 'Unapproved field exported'}; if(!$report.accepted){throw 'Positive report denied'}");
    [Fact] public Task ReportCannotClaimRemoteOrModelAcceptance() => RunAsync("$cases[0].provenance='hosted-or-model'; $rejected=$false; try { New-AcceptanceReport $identity $cases @{passed=$true}|Out-Null } catch {$rejected=$true}; if(!$rejected){throw 'Unsupported provenance accepted'}");
    [Fact]
    public Task DuplicateTrxRetainsObservedFailureAndPass() => RunAsync("$file=Join-Path '.local/results' ('report-parser-'+[guid]::NewGuid().ToString('N')+'.trx'); '<TestRun><Results><UnitTestResult testName=\"Demo.A01_First\" outcome=\"Failed\"/><UnitTestResult testName=\"Demo.A02_Second\" outcome=\"Passed\"/><UnitTestResult testName=\"Demo.A02_Second\" outcome=\"Passed\"/></Results></TestRun>'|Set-Content $file; $observed=@($cases|ForEach-Object {$_.status='not_run';$_}); $valid=$false; try {$parsed=Read-AcceptanceCases $file $identity; $observed=$parsed.cases;$valid=$parsed.valid} catch {}; if($valid -or $observed[0].status -ne 'fail' -or $observed[1].status -ne 'pass' -or $observed[2].status -ne 'not_run'){throw 'Observed outcomes lost after duplicate result'}");
}
