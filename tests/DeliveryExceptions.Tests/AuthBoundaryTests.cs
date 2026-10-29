using System.Net;
using System.Net.Http.Headers;
using DeliveryExceptions.Client;
using DeliveryExceptions.Tests.Support;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace DeliveryExceptions.Tests;

[Collection("Local stack")]
[Trait("Category", "Tokens")]
public sealed class AuthBoundaryTests(DemoStack stack)
{
    [Fact]
    public void AudienceMustMatchExactResourceUri() => Assert.Equal(5, SignedTokenFixtures.VerifyAndCreateNegatives().Count());

    [Fact]
    public async Task RouteAliasesEnforceReadScopeAndDenialAudit()
    {
        var browser = new BrowserLogin();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        using var goodProbe = new OAuthProbe(); using var goodHttp = new HttpClient(goodProbe);
        await using var good = await new OAuthLogin(browser.OpenAsync, goodHttp).ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token);
        using var noScopeProbe = new OAuthProbe(); using var noScopeHttp = new HttpClient(noScopeProbe);
        var login = new OAuthLogin((uri, ct) => browser.OpenAsync(new UriBuilder(uri) { Query = QueryString.Create(QueryHelpers.ParseQuery(uri.Query).Select(p => new KeyValuePair<string, string?>(p.Key, p.Key == "scope" ? "openid" : p.Value.ToString()))).Value }.Uri, ct), noScopeHttp);
        await Assert.ThrowsAnyAsync<Exception>(() => login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token));
        Assert.NotNull(goodProbe.AccessToken); Assert.NotNull(noScopeProbe.AccessToken);
        await AssertRouteAliasesAsync(stack, goodProbe.AccessToken!, noScopeProbe.AccessToken!);
    }

    internal static async Task AssertRouteAliasesAsync(DemoStack stack, string goodToken, string noScopeToken)
    {
        await using var source = DemoDatabase.Source();
        foreach (var path in new[] { "/mcp/", "/MCP/" })
        {
            using var good = await SendAsync(stack.Http, path, goodToken);
            Assert.Equal(HttpStatusCode.OK, good.StatusCode);
            using var denied = await SendAsync(stack.Http, path, noScopeToken);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            foreach (var token in new string?[] { null, TokenFixtures.Unsigned() })
            {
                using var invalid = await SendAsync(stack.Http, path, token);
                Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
                Assert.True(invalid.Headers.TryGetValues("X-Correlation-Id", out var correlations), "MCP alias skipped transport audit");
                await using var query = source.CreateCommand("select subject,tenant_id,reason from audit where correlation_id=@id");
                query.Parameters.AddWithValue("id", correlations.Single());
                await using var row = await query.ExecuteReaderAsync();
                Assert.True(await row.ReadAsync()); Assert.True(row.IsDBNull(0)); Assert.True(row.IsDBNull(1));
                Assert.Equal(token is null ? "missing_token" : "invalid_token", row.GetString(2));
            }
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient http, string path, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5180" + path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.ParseAdd("application/json"); request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("Mcp-Method", "tools/list"); request.Headers.Add("Mcp-Protocol-Version", "2026-07-28");
        request.Content = System.Net.Http.Json.JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list", @params = new Dictionary<string, object?> { ["_meta"] = new Dictionary<string, object?> { ["io.modelcontextprotocol/protocolVersion"] = "2026-07-28", ["io.modelcontextprotocol/clientCapabilities"] = new { } } } });
        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }
}
