using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;

namespace DeliveryExceptions.Tests.Support;

public sealed class OAuthProbe(string mode = "normal") : DelegatingHandler(new SocketsHttpHandler())
{
    public int TokenRequests { get; private set; }
    public List<string> Resources { get; } = [];
    public List<string> SentResources { get; } = [];
    public List<object> SafeResponses { get; } = [];
    public int SuccessfulTokens { get; private set; }
    public List<int> McpStatuses { get; } = [];
    public string? LastOAuthError { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri?.AbsolutePath.EndsWith("/token", StringComparison.Ordinal) == true)
        {
            TokenRequests++;
            var body = await request.Content!.ReadAsStringAsync(ct);
            var fields = QueryHelpers.ParseQuery(body).ToDictionary(entry => entry.Key, entry => entry.Value.ToString());
            Resources.Add(fields.GetValueOrDefault("resource", "missing"));
            if (mode is "changed-resource" or "unknown-resource") fields["resource"] = "http://127.0.0.1:5180/unknown";
            if (mode == "wrong-verifier") fields["code_verifier"] = new string('x', 50);
            SentResources.Add(fields.GetValueOrDefault("resource", "missing"));
            request.Content = new FormUrlEncodedContent(fields);
        }
        var response = await base.SendAsync(request, ct);
        if (request.RequestUri?.AbsolutePath == "/mcp") McpStatuses.Add((int)response.StatusCode);
        if (request.RequestUri?.AbsolutePath == "/mcp" && response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            SafeResponses.Add(new { status = 401, hasBearer = request.Headers.Authorization?.Scheme == "Bearer", failure = response.Headers.TryGetValues("X-Demo-Auth-Failure", out var failures) ? failures.First() : "none", type = response.Headers.TryGetValues("X-Demo-Validated-Type", out var types) ? types.First() : "not-validated", subject = response.Headers.TryGetValues("X-Demo-Has-Subject", out var subjects) ? subjects.First() : "not-validated" });
        if (request.RequestUri?.AbsolutePath.EndsWith("/token", StringComparison.Ordinal) == true)
        {
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (response.IsSuccessStatusCode)
            {
                SuccessfulTokens++;
                var token = new JwtSecurityTokenHandler().ReadJwtToken(body.RootElement.GetProperty("access_token").GetString());
                SafeResponses.Add(new { status = (int)response.StatusCode, audience = token.Audiences.ToArray(), issuer = token.Issuer, type = token.Claims.FirstOrDefault(claim => claim.Type == "typ")?.Value, algorithm = token.Header.Alg });
            }
            else
            {
                LastOAuthError = body.RootElement.TryGetProperty("error", out var error) ? error.GetString() : "missing";
                SafeResponses.Add(new { status = (int)response.StatusCode, error = LastOAuthError });
            }
        }
        return response;
    }
}
