using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using DeliveryExceptions.Tests.Support;
using Xunit;

namespace DeliveryExceptions.Tests;

[Collection("Local stack")]
[Trait("Category", "Report")]
public sealed class ReportLifecycleTests(DemoStack stack)
{
    [Fact]
    public async Task OccupiedServicePortFailsWithoutStoppingOwner()
    {
        var original = stack.ServiceProcessId;
        var contender = new DemoStack();
        try { await Assert.ThrowsAnyAsync<Exception>(() => contender.InitializeAsync()); }
        finally { await contender.DisposeAsync(); }
        Assert.Equal(original, stack.ServiceProcessId);
        using var response = await stack.Http.GetAsync("http://127.0.0.1:5180/mcp", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private static async Task<string> CommandAsync(string program, params string[] arguments)
    {
        if (!OperatingSystem.IsWindows() && program == "wsl") { program = "docker"; arguments = arguments.Skip(4).ToArray(); }
        var start = new ProcessStartInfo(program) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = DemoStack.Root };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await stderr);
        return (await stdout).Trim();
    }

    [Fact]
    public async Task CleanupDoesNotTouchUnownedProcessOrComposeProject()
    {
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-Command"); start.ArgumentList.Add("Start-Sleep -Seconds 180");
        using var independent = Process.Start(start)!;
        using var port = new TcpListener(IPAddress.Loopback, 5184); port.Start();
        using var versions = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(DemoStack.Root, "infra", "versions.json")));
        var canary = await CommandAsync("wsl", "-d", "Ubuntu", "--", "docker", "run", "-d", "--label", "com.docker.compose.project=p18-cleanup-canary", "--entrypoint", "sleep", versions.RootElement.GetProperty("postgres").GetString()!, "180");
        var volumes = await CommandAsync("wsl", "-d", "Ubuntu", "--", "docker", "volume", "ls", "--filter", "label=com.docker.compose.project=p18-delivery-exceptions", "--format", "{{.Name}}");
        try
        {
            await CommandAsync("pwsh", "-NoProfile", "-File", "scripts/stop.ps1");
            Assert.False(independent.HasExited);
            Assert.Equal("true", await CommandAsync("wsl", "-d", "Ubuntu", "--", "docker", "inspect", "--format", "{{.State.Running}}", canary));
            Assert.Equal(volumes, await CommandAsync("wsl", "-d", "Ubuntu", "--", "docker", "volume", "ls", "--filter", "label=com.docker.compose.project=p18-delivery-exceptions", "--format", "{{.Name}}"));
            using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, 5184); Assert.True(client.Connected);
            using var response = await stack.Http.GetAsync("http://127.0.0.1:5180/mcp", HttpCompletionOption.ResponseHeadersRead);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }
        finally
        {
            if (!independent.HasExited) { independent.Kill(); await independent.WaitForExitAsync(); }
            await CommandAsync("wsl", "-d", "Ubuntu", "--", "docker", "stop", canary);
            await CommandAsync("wsl", "-d", "Ubuntu", "--", "docker", "rm", canary);
            await CommandAsync("pwsh", "-NoProfile", "-File", "scripts/dependencies.ps1", "up");
            var ready = false;
            for (var n = 0; n < 90; n++)
            {
                try { using var response = await stack.Http.GetAsync("http://127.0.0.1:5182/realms/delivery-demo/.well-known/openid-configuration", HttpCompletionOption.ResponseHeadersRead); if (response.IsSuccessStatusCode) { ready = true; break; } } catch (HttpRequestException) { }
                await Task.Delay(1000);
            }
            Assert.True(ready, "Owned identity provider did not return after lifecycle test");
        }
    }
}
