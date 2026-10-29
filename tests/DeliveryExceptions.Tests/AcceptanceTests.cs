using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeliveryExceptions.Client;
using DeliveryExceptions.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;
using Xunit.Abstractions;

namespace DeliveryExceptions.Tests;

[Collection("Local stack")]
[Trait("Category", "Acceptance")]
public sealed class AcceptanceTests(DemoStack stack, ITestOutputHelper output)
{
    private const string A = "a0000000-0000-0000-0000-000000000001";
    private const string B = "b0000000-0000-0000-0000-000000000001";
    private OAuthCompatibilityTests Compatibility => new(stack, output);
    private DeliveryToolsTests Tools => new(stack);
    private AuditTests Audit => new(stack);
    private static Dictionary<string, object?> Id(string id) => new() { ["delivery_id"] = id };
    private static JsonElement Data(ModelContextProtocol.Protocol.CallToolResult result) => DeliveryToolsTests.Payload(result);
    private async Task ResetAsync() { await using var source = DemoDatabase.Source(); await DeliveryRepositoryTests.ResetAsync(source); }
    private static async Task<string> SnapshotAsync(string id)
    {
        await using var source = DemoDatabase.Source();
        await using var command = source.CreateCommand("select row_to_json(d)::text from deliveries d where id=@id");
        command.Parameters.AddWithValue("id", Guid.Parse(id));
        return (string)(await command.ExecuteScalarAsync())!;
    }

    [Fact] public async Task A01_DiscoveryAndLogin() { await Compatibility.ChallengeAdvertisesExactResource(); await Compatibility.BrowserPkceLoginDiscoversExpectedTools(); await Tools.ThreeToolsHaveNoTenantArgument(); }
    [Fact]
    public async Task A02_ReadIsolation()
    {
        await ResetAsync();
        foreach (var user in new[] { "a-operator", "a-reader", "b-operator" })
        {
            var result = Data(await (await stack.GetClientAsync(user, false)).CallToolAsync("list_delayed_deliveries"));
            Assert.Equal(JsonValueKind.Null, result.GetProperty("error").ValueKind);
            Assert.Equal(new[] { user[0] + "0000000-0000-0000-0000-000000000001", user[0] + "0000000-0000-0000-0000-000000000002" }, result.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("id").GetString()).Order());
        }
    }
    [Fact]
    public async Task A03_ForeignAndAbsentRead()
    {
        await ResetAsync(); var client = await stack.GetClientAsync();
        foreach (var id in new[] { B, Guid.Empty.ToString() })
        {
            var result = Data(await client.CallToolAsync("get_delivery", Id(id)));
            Assert.Equal("not_found", result.GetProperty("error").GetString());
            Assert.Equal(JsonValueKind.Null, result.GetProperty("data").ValueKind);
            Assert.Equal(new[] { "correlationId", "data", "error", "untrustedFields" }, result.EnumerateObject().Select(p => p.Name).Order());
        }
    }
    [Fact]
    public async Task A04_PermittedWrite()
    {
        await Tools.OperatorReviewsOwnDelayedRecord();
        await using var source = DemoDatabase.Source();
        await using var query = source.CreateCommand("select subject,tenant_id,before_version,after_version from audit where operation='mark_exception_reviewed' and outcome='allow'");
        await using var row = await query.ExecuteReaderAsync();
        Assert.True(await row.ReadAsync());
        Assert.Equal("10000000-0000-0000-0000-000000000001", row.GetString(0));
        Assert.Equal(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), row.GetGuid(1));
        Assert.Equal(1, row.GetInt64(2)); Assert.Equal(2, row.GetInt64(3));
        Assert.False(await row.ReadAsync());
    }
    [Fact] public Task A05_ScopeAndEntitlement() => Tools.ReaderAndReadOnlyTokenCannotWrite();
    [Fact]
    public async Task A06_ForeignWrite()
    {
        await ResetAsync(); var before = await SnapshotAsync(B);
        await Tools.ForeignWriteIsNotFound(); Assert.Equal(before, await SnapshotAsync(B));
        await Tools.ForeignConflictDoesNotExposeVersion();
    }
    [Fact]
    public async Task A07_TokenRejection()
    {
        await ResetAsync();
        var beforeA = await SnapshotAsync(A); var beforeB = await SnapshotAsync(B);
        using var probe = new OAuthProbe("short-lived"); using var http = new HttpClient(probe);
        var browser = new BrowserLogin();
        var login = new OAuthLogin((uri, ct) => browser.OpenAsync(ChangeQuery(uri, "client_id", "delivery-demo-short"), ct), http);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        await using var client = await login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token);
        Assert.NotNull(probe.AccessToken); Assert.NotNull(probe.IdToken);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(probe.AccessToken);
        Assert.Equal(10, (token.ValidTo - token.IssuedAt).TotalSeconds);
        Assert.Equal(HttpStatusCode.OK, await PostAsync(probe.AccessToken));
        var parts = probe.AccessToken!.Split('.'); parts[1] = parts[1][0..^1] + (parts[1][^1] == 'A' ? 'B' : 'A');
        foreach (var bad in new string?[] { null, "malformed", string.Join('.', parts), TokenFixtures.Unsigned(), probe.IdToken }.Concat(SignedTokenFixtures.VerifyAndCreateNegatives()))
            Assert.Equal(HttpStatusCode.Unauthorized, await PostAsync(bad));
        var remaining = new DateTimeOffset(token.ValidTo, TimeSpan.Zero).AddSeconds(3) - DateTimeOffset.UtcNow;
        if (remaining > TimeSpan.Zero) await Task.Delay(remaining, deadline.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, await PostAsync(probe.AccessToken));
        // New explicit browser sign-in gets a fresh token; the old token is never refreshed for the expiry probe.
        using var freshProbe = new OAuthProbe(); using var freshHttp = new HttpClient(freshProbe);
        await using var fresh = await new OAuthLogin(browser.OpenAsync, freshHttp).ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token);
        Assert.Equal(HttpStatusCode.OK, await PostAsync(freshProbe.AccessToken));
        using var noScopeProbe = new OAuthProbe(); using var noScopeHttp = new HttpClient(noScopeProbe);
        await Assert.ThrowsAnyAsync<Exception>(() => new OAuthLogin((uri, ct) => browser.OpenAsync(ChangeQuery(uri, "scope", "openid"), ct), noScopeHttp).ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token));
        Assert.NotNull(noScopeProbe.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, await PostAsync(noScopeProbe.AccessToken));
        Assert.Equal(beforeA, await SnapshotAsync(A)); Assert.Equal(beforeB, await SnapshotAsync(B));
    }
    private static Uri ChangeQuery(Uri uri, string key, string value) => new UriBuilder(uri) { Query = QueryString.Create(QueryHelpers.ParseQuery(uri.Query).Select(p => new KeyValuePair<string, string?>(p.Key, p.Key == key ? value : p.Value.ToString()))).Value }.Uri;
    private async Task<HttpStatusCode> PostAsync(string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5180/mcp");
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json"); request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2026-07-28");
        request.Headers.Add("Mcp-Method", "tools/list");
        request.Content = System.Net.Http.Json.JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list", @params = new Dictionary<string, object?> { ["_meta"] = new Dictionary<string, object?> { ["io.modelcontextprotocol/protocolVersion"] = "2026-07-28", ["io.modelcontextprotocol/clientCapabilities"] = new { } } } });
        using var response = await stack.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        return response.StatusCode;
    }
    [Fact] public async Task A08_ConflictsAndConcurrency() { await Tools.RepeatedOrStaleReviewConflicts(); await Tools.OnTimeAndReviewedConflict(); await Tools.SameVersionConcurrentWritesYieldOneSuccess(); }
    [Fact] public Task A09_InjectionBoundary() => Audit.CarrierNoteIsNeverAuthority();
    [Fact]
    public async Task A10_AuditPrivacy()
    {
        await Audit.AuthenticationDenialHasNoTrustedSubject(); await Audit.DenialSurvivesDomainRollback();
        await Audit.AuditUnavailablePreventsReadAndWriteSuccess(); await Tools.OperatorReviewsOwnDelayedRecord();
        await using var source = DemoDatabase.Source(); await using var query = source.CreateCommand("select row_to_json(a)::text from audit a");
        await using var rows = await query.ExecuteReaderAsync();
        using var secrets = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(DemoStack.Root, ".local", "credentials.json")));
        var forbidden = secrets.RootElement.GetProperty("users").EnumerateObject().Select(p => p.Value.GetString()!).Append(secrets.RootElement.GetProperty("postgresPassword").GetString()!).ToArray();
        var texts = new List<string> { stack.ServiceOutput }; while (await rows.ReadAsync()) texts.Add(rows.GetString(0));
        foreach (var text in texts) { foreach (var secret in forbidden) Assert.DoesNotContain(secret, text); Assert.DoesNotContain("eyJ", text); Assert.DoesNotContain("Ignore all permissions", text); }
        await new ClientTests(stack).LogOutputContainsNoTokenCodeOrPassword();
    }
    [Fact]
    public async Task A11_Persistence()
    {
        await Tools.OperatorReviewsOwnDelayedRecord(); var before = await SnapshotAsync(A);
        await using var source = DemoDatabase.Source(); await using var count = source.CreateCommand("select count(*) from audit where operation='mark_exception_reviewed' and outcome='allow'");
        var audits = await count.ExecuteScalarAsync(); var pid = stack.ServiceProcessId; await stack.RestartAsync();
        Assert.NotEqual(pid, stack.ServiceProcessId); Assert.Equal(before, await SnapshotAsync(A)); Assert.Equal(audits, await count.ExecuteScalarAsync());
        var read = Data(await (await stack.GetClientAsync()).CallToolAsync("get_delivery", Id(A)));
        Assert.True(read.GetProperty("data").GetProperty("reviewed").GetBoolean()); Assert.Equal(2, read.GetProperty("data").GetProperty("version").GetInt64());
    }
    [Fact] public async Task A12_TargetBinding() { await Compatibility.UnknownResourceFails(); await Compatibility.ChangedResourceAtExchangeOrWrongVerifierFails("changed-resource"); await Compatibility.ChangedResourceAtExchangeOrWrongVerifierFails("wrong-verifier"); await Compatibility.MismatchedStateFails(); await Compatibility.BrowserPkceLoginDiscoversExpectedTools(); }
}
