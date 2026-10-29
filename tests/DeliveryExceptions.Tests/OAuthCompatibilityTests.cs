using System.Net;
using Xunit;
using DeliveryExceptions.Client;
using System.Net.Sockets;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Http;

namespace DeliveryExceptions.Tests;

[Collection("Local stack")]
[Trait("Category", "Compatibility")]
public sealed class OAuthCompatibilityTests(Support.DemoStack stack, Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public async Task ChallengeAdvertisesExactResource()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5180/mcp");
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await stack.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("resource_metadata=\"http://127.0.0.1:5180/.well-known/oauth-protected-resource/mcp\"", response.Headers.WwwAuthenticate.ToString());
        using var metadataResponse = await stack.Http.GetAsync("http://127.0.0.1:5180/.well-known/oauth-protected-resource/mcp", HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, metadataResponse.StatusCode);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var metadata = await System.Text.Json.JsonDocument.ParseAsync(await metadataResponse.Content.ReadAsStreamAsync(deadline.Token), cancellationToken: deadline.Token);
        Assert.Equal("http://127.0.0.1:5180/mcp", metadata.RootElement.GetProperty("resource").GetString());
        Assert.Equal("http://127.0.0.1:5182/realms/delivery-demo", metadata.RootElement.GetProperty("authorization_servers")[0].GetString());
    }

    [Fact]
    public async Task BrowserPkceLoginDiscoversExpectedTools()
    {
        var browser = new Support.BrowserLogin();
        using var probe = new Support.OAuthProbe();
        using var http = new HttpClient(probe);
        var sawAuthorization = false;
        var login = new OAuthLogin(async (uri, ct) =>
        {
            var query = QueryHelpers.ParseQuery(uri.Query);
            Assert.Equal("http://127.0.0.1:5180/mcp", query["resource"]);
            Assert.Equal("S256", query["code_challenge_method"]);
            Assert.DoesNotContain("offline_access", query["scope"].ToString());
            sawAuthorization = true;
            await browser.OpenAsync(uri, ct);
        }, http);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        ModelContextProtocol.Client.McpClient client;
        try { client = await login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token); }
        catch { output.WriteLine(System.Text.Json.JsonSerializer.Serialize(probe.SafeResponses)); throw; }
        await using var ownedClient = client;
        Assert.Equal("2026-07-28", client.NegotiatedProtocolVersion);
        Assert.Equal(3, (await client.ListToolsAsync(cancellationToken: deadline.Token)).Count);
        Assert.True(sawAuthorization);
        Assert.Equal(["http://127.0.0.1:5180/mcp"], probe.Resources);
    }

    [Fact]
    public async Task UnexpectedHostOrOriginRejected()
    {
        using var badHost = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:5180/mcp");
        badHost.Headers.Host = "unexpected.invalid";
        using var hostResponse = await stack.Http.SendAsync(badHost, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.BadRequest, hostResponse.StatusCode);
        using var badOrigin = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:5180/mcp");
        badOrigin.Headers.Add("Origin", "https://unexpected.invalid");
        using var originResponse = await stack.Http.SendAsync(badOrigin, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Forbidden, originResponse.StatusCode);
    }

    [Fact]
    public async Task OccupiedCallbackPortFailsWithoutStoppingOwner()
    {
        using var owner = new TcpListener(IPAddress.Loopback, 5181);
        owner.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var browserOpened = false;
        var login = new OAuthLogin((uri, ct) => { browserOpened = true; return Task.CompletedTask; });
        await Assert.ThrowsAnyAsync<Exception>(() => login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token));
        Assert.False(browserOpened);
        using var connection = new TcpClient();
        await connection.ConnectAsync(IPAddress.Loopback, 5181, deadline.Token);
        Assert.True(connection.Connected);
    }

    [Fact]
    public async Task UnknownResourceFails()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        var browser = new Support.BrowserLogin();
        using var probe = new Support.OAuthProbe("unknown-resource");
        using var http = new HttpClient(probe);
        var attempted = false;
        var login = new OAuthLogin(async (uri, ct) =>
        {
            attempted = true;
            var query = QueryHelpers.ParseQuery(uri.Query).Select(entry => new KeyValuePair<string, string?>(entry.Key, entry.Key == "resource" ? "http://127.0.0.1:5180/unknown" : entry.Value.ToString()));
            var changed = new UriBuilder(uri) { Query = QueryString.Create(query).Value };
            await browser.OpenAsync(changed.Uri, ct);
        }, http);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token));
        Assert.True(attempted);
        output.WriteLine(System.Text.Json.JsonSerializer.Serialize(probe.SafeResponses));
        Assert.Equal(0, probe.SuccessfulTokens);
        Assert.Equal("invalid_target", probe.LastOAuthError);
        Assert.Equal(["http://127.0.0.1:5180/unknown"], probe.SentResources);
        Assert.IsNotAssignableFrom<OperationCanceledException>(error);
    }

    [Theory]
    [InlineData("changed-resource")]
    [InlineData("wrong-verifier")]
    public async Task ChangedResourceAtExchangeOrWrongVerifierFails(string mode)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(180));
        var browser = new Support.BrowserLogin();
        using var probe = new Support.OAuthProbe(mode);
        using var http = new HttpClient(probe);
        var login = new OAuthLogin(browser.OpenAsync, http);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token));
        Assert.True(probe.TokenRequests > 0);
        Assert.Equal(0, probe.SuccessfulTokens);
        Assert.Equal(mode == "wrong-verifier" ? "invalid_grant" : "invalid_target", probe.LastOAuthError);
        Assert.IsNotAssignableFrom<OperationCanceledException>(error);
    }

    [Fact]
    public async Task MismatchedStateFails()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var probe = new Support.OAuthProbe();
        using var http = new HttpClient(probe);
        var callbackReached = false;
        var login = new OAuthLogin(async (_, ct) =>
        {
            using var response = await stack.Http.GetAsync("http://127.0.0.1:5181/callback?code=synthetic&state=incorrect&iss=http%3A%2F%2F127.0.0.1%3A5182%2Frealms%2Fdelivery-demo", HttpCompletionOption.ResponseHeadersRead, ct);
            callbackReached = response.IsSuccessStatusCode;
        }, http);
        var error = await Assert.ThrowsAnyAsync<Exception>(() => login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token));
        Assert.True(callbackReached);
        Assert.Equal(0, probe.TokenRequests);
        Assert.IsNotAssignableFrom<OperationCanceledException>(error);
    }

    [Fact]
    public async Task WrongCallbackPathFails()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var rejected = false;
        var login = new OAuthLogin(async (_, ct) =>
        {
            using var response = await stack.Http.GetAsync("http://127.0.0.1:5181/wrong?code=synthetic&state=incorrect", HttpCompletionOption.ResponseHeadersRead, ct);
            rejected = response.StatusCode == HttpStatusCode.NotFound;
            using var finish = await stack.Http.GetAsync("http://127.0.0.1:5181/callback?code=synthetic&state=incorrect", HttpCompletionOption.ResponseHeadersRead, ct);
        });
        await Assert.ThrowsAnyAsync<Exception>(() => login.ConnectAsync(new("http://127.0.0.1:5180/mcp"), false, deadline.Token));
        Assert.True(rejected);
    }

    [Fact]
    public async Task AnonymousClientRegistrationRejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5182/realms/delivery-demo/clients-registrations/openid-connect");
        request.Content = System.Net.Http.Json.JsonContent.Create(new { client_name = "must-not-register", redirect_uris = new[] { "http://127.0.0.1:5181/callback" } });
        using var response = await stack.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden });
    }
}

[CollectionDefinition("Local stack", DisableParallelization = true)]
public sealed class StackCollection : ICollectionFixture<Support.DemoStack>;
