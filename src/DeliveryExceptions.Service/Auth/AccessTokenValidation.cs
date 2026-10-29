using Microsoft.IdentityModel.Tokens;

namespace DeliveryExceptions.Service.Auth;

public static class AccessTokenValidation
{
    public static TokenValidationParameters Create() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "http://127.0.0.1:5182/realms/delivery-demo",
        ValidateAudience = true,
        ValidAudience = "http://127.0.0.1:5180/mcp",
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromSeconds(2),
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
    };
}
