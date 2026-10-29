using ModelContextProtocol.Client;
using ModelContextProtocol.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;

namespace DeliveryExceptions.Client;

public sealed class OAuthLogin(Func<Uri, CancellationToken, Task>? openBrowser = null, HttpClient? http = null, TimeSpan? callbackTimeout = null)
{
    public TimeSpan CallbackTimeout { get; } = callbackTimeout ?? TimeSpan.FromSeconds(120);
    public async Task<McpClient> ConnectAsync(Uri resource, bool requestReview, CancellationToken ct)
    {
        if (CallbackTimeout <= TimeSpan.Zero || CallbackTimeout > TimeSpan.FromSeconds(120))
            throw new ArgumentOutOfRangeException(nameof(callbackTimeout));
        if (resource.AbsoluteUri != "http://127.0.0.1:5180/mcp")
            throw new ArgumentException("Only the configured local MCP resource is supported", nameof(resource));
        var options = new HttpClientTransportOptions
        {
            Endpoint = resource,
            TransportMode = HttpTransportMode.StreamableHttp,
            OAuth = new ClientOAuthOptions
            {
                ClientId = "delivery-demo-cli",
                RedirectUri = new("http://127.0.0.1:5181/callback"),
                ScopeSelector = _ => requestReview ? ["openid", "deliveries.read", "exceptions.review"] : ["openid", "deliveries.read"],
                AuthorizationCallbackHandler = ReceiveAsync
            }
        };
        var transport = http is null ? new HttpClientTransport(options) : new HttpClientTransport(options, http);
        try
        {
            return await McpClient.CreateAsync(transport, new() { ProtocolVersion = "2026-07-28", InitializationTimeout = TimeSpan.FromSeconds(150) }, cancellationToken: ct);
        }
        catch { await transport.DisposeAsync(); throw; }
    }

    private async Task<AuthorizationResult?> ReceiveAsync(AuthorizationCallbackContext context, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(CallbackTimeout);
        var completion = new TaskCompletionSource<AuthorizationResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 5181));
        await using var callback = builder.Build();
        callback.MapGet("/callback", (HttpContext request) =>
        {
            if (request.Request.Host.Host != "127.0.0.1" || request.Request.Headers.ContainsKey("Origin"))
                return Results.StatusCode(400);
            var query = request.Request.Query;
            if (query.ContainsKey("error"))
                completion.TrySetException(new InvalidOperationException("Authorization provider refused the request"));
            else if (query["code"].Count == 1 && query["state"].Count == 1 && query["iss"].Count <= 1)
                completion.TrySetResult(new() { Code = query["code"]!, State = query["state"], Iss = query["iss"].FirstOrDefault() });
            else return Results.StatusCode(400);
            return Results.Text("Sign-in response received. You may close this window.", "text/plain");
        });
        await callback.StartAsync(timeout.Token);
        try
        {
            if (openBrowser is not null) await openBrowser(context.AuthorizationUri, timeout.Token);
            else Process.Start(new ProcessStartInfo(context.AuthorizationUri.AbsoluteUri) { UseShellExecute = true });
            return await completion.Task.WaitAsync(timeout.Token);
        }
        finally { await callback.StopAsync(CancellationToken.None); }
    }
}
