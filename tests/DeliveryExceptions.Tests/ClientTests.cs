using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using DeliveryExceptions.Client;
using DeliveryExceptions.Tests.Support;
using Xunit;

namespace DeliveryExceptions.Tests;

[Collection("Local stack")]
[Trait("Category", "Client")]
public sealed class ClientTests(DemoStack stack)
{
    private const string A1 = "a0000000-0000-0000-0000-000000000001";

    [Fact]
    public async Task CliShowsCorrelationWithoutToken()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        using var output = new StringWriter();
        var browser = new BrowserLogin();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        Assert.Equal(0, await new ClientCommands(new OAuthLogin(browser.OpenAsync), output).RunAsync(["get", "--id", A1], deadline.Token));
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal("A-DELAY-01", document.RootElement.GetProperty("data").GetProperty("reference").GetString());
        Assert.Matches("^[0-9a-f]{32}$", document.RootElement.GetProperty("correlationId").GetString());
        Assert.False(Regex.IsMatch(output.ToString(), @"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\."), "CLI output contains JWT-shaped material");
    }

    [Fact]
    public async Task ExplicitCliReviewUpdatesOneRecord()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var browser = new BrowserLogin();
        using var output = new StringWriter();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        Assert.Equal(0, await new ClientCommands(new OAuthLogin(browser.OpenAsync), output).RunAsync(["review", "--id", A1, "--expected-version", "1"], deadline.Token));
        using var result = JsonDocument.Parse(output.ToString());
        Assert.True(result.RootElement.GetProperty("data").GetProperty("reviewed").GetBoolean());
    }

    [Fact]
    public async Task InvalidCliArgumentsNeverOpenBrowser()
    {
        var opened = false;
        var login = new OAuthLogin((_, _) => { opened = true; return Task.CompletedTask; });
        using var output = new StringWriter();
        var commands = new ClientCommands(login, output);
        foreach (var args in new[] { Array.Empty<string>(), new[] { "review", "--id", A1 }, new[] { "get", "--id", "bad-id" }, new[] { "list", "--tenant", "other" }, new[] { "review", "--id", A1, "--expected-version", "0" } })
            Assert.Equal(2, await commands.RunAsync(args, CancellationToken.None));
        Assert.False(opened);
    }

    [Fact]
    public async Task TransportAndToolFailuresRemainDistinct()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        using var probe = new OAuthProbe();
        using var http = new HttpClient(probe);
        var browser = new BrowserLogin();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        await using var client = await new OAuthLogin(browser.OpenAsync, http).ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token);
        var denied = await client.CallToolAsync("mark_exception_reviewed", new Dictionary<string, object?> { ["delivery_id"] = A1, ["expected_version"] = 1 });
        Assert.Equal(true, denied.IsError);
        Assert.Equal("forbidden", DeliveryToolsTests.Payload(denied).GetProperty("error").GetString());
        Assert.Equal(200, probe.McpStatuses.Last());
        using var rawRequest = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5180/mcp")
        { Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json") };
        using var raw = await stack.Http.SendAsync(rawRequest, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Unauthorized, raw.StatusCode);
    }

    [Fact]
    public async Task CancelledLoginClosesListener()
    {
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var login = new OAuthLogin((_, _) => { opened.TrySetResult(); return Task.CompletedTask; });
        var connect = login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token);
        await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
        deadline.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connect);
        using var released = new TcpListener(IPAddress.Loopback, 5181);
        released.Start();
    }

    [Fact]
    public async Task CallbackTimeoutClosesListener()
    {
        var elapsed = new Stopwatch();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var login = new OAuthLogin((_, _) => { elapsed.Start(); return Task.CompletedTask; }, callbackTimeout: TimeSpan.FromMilliseconds(300));
        Assert.Equal(TimeSpan.FromSeconds(120), new OAuthLogin().CallbackTimeout);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token));
        Assert.True(elapsed.IsRunning, "The actual callback listener was never reached");
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2), "The shorter callback timeout was not applied");
        using var released = new TcpListener(IPAddress.Loopback, 5181);
        released.Start();
    }

    [Fact]
    public async Task LogOutputContainsNoTokenCodeOrPassword()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var browser = new BrowserLogin();
        using var output = new StringWriter();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        Assert.Equal(0, await new ClientCommands(new OAuthLogin(browser.OpenAsync), output).RunAsync(["get", "--id", A1], deadline.Token));
        await using var query = source.CreateCommand("select coalesce(json_agg(audit)::text,'[]') from audit");
        var retained = output.ToString() + stack.ServiceOutput + (string)(await query.ExecuteScalarAsync())!;
        using var credentials = JsonDocument.Parse(File.ReadAllText(Path.Combine(DemoStack.Root, ".local", "credentials.json")));
        foreach (var password in credentials.RootElement.GetProperty("users").EnumerateObject())
            Assert.False(retained.Contains(password.Value.GetString()!, StringComparison.Ordinal), "Generated user credential leaked");
        Assert.False(Regex.IsMatch(retained, @"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\."), "JWT-shaped material leaked");
        Assert.DoesNotContain("code_verifier", retained);
        Assert.DoesNotContain("refresh_token", retained);
    }
}
