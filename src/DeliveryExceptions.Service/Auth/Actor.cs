using System.Security.Claims;

namespace DeliveryExceptions.Service.Auth;

public sealed record Actor(string Issuer, string Subject, IReadOnlySet<string> Scopes)
{
    public static Actor? FromPrincipal(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true) return null;
        var issuers = principal.FindAll("iss").ToArray();
        var subjects = principal.FindAll("sub").ToArray();
        var scopes = principal.FindAll("scope").ToArray();
        if (issuers.Length != 1 || subjects.Length != 1 || scopes.Length > 1 ||
            issuers[0].Value != "http://127.0.0.1:5182/realms/delivery-demo" ||
            string.IsNullOrWhiteSpace(subjects[0].Value) || subjects[0].Value.Length > 100 ||
            subjects[0].Value.Any(char.IsWhiteSpace) || (scopes.FirstOrDefault()?.Value.Length ?? 0) > 500) return null;
        return new(issuers[0].Value, subjects[0].Value,
            (scopes.FirstOrDefault()?.Value ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal));
    }
}

public sealed record Membership(Guid TenantId, bool CanRead, bool CanReview);
