using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using DeliveryExceptions.Service.Auth;
using Xunit;

namespace DeliveryExceptions.Tests.Support;

public static class SignedTokenFixtures
{
    public static IEnumerable<string> VerifyAndCreateNegatives()
    {
        // Fixture key is trusted only by this isolated validator, never by the HTTP service.
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa);
        var parameters = AccessTokenValidation.Create();
        parameters.IssuerSigningKey = key;
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        string Create(string issuer, string audience, DateTime expires) => handler.WriteToken(new JwtSecurityToken(
            issuer, audience, [new("sub", "10000000-0000-0000-0000-000000000001"), new("typ", "Bearer"), new("scope", "deliveries.read")],
            DateTime.UtcNow.AddMinutes(-10), expires, new SigningCredentials(key, SecurityAlgorithms.RsaSha256)));
        var positive = Create(parameters.ValidIssuer!, parameters.ValidAudience!, DateTime.UtcNow.AddMinutes(1));
        handler.ValidateToken(positive, parameters, out _);
        var issuer = Create("http://wrong-issuer.invalid", parameters.ValidAudience!, DateTime.UtcNow.AddMinutes(1));
        var audience = Create(parameters.ValidIssuer!, "http://wrong-audience.invalid", DateTime.UtcNow.AddMinutes(1));
        var slashAudience = Create(parameters.ValidIssuer!, parameters.ValidAudience! + "/", DateTime.UtcNow.AddMinutes(1));
        var expired = Create(parameters.ValidIssuer!, parameters.ValidAudience!, DateTime.UtcNow.AddSeconds(-10));
        Assert.Throws<SecurityTokenInvalidIssuerException>(() => handler.ValidateToken(issuer, parameters, out _));
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => handler.ValidateToken(audience, parameters, out _));
        Assert.Throws<SecurityTokenExpiredException>(() => handler.ValidateToken(expired, parameters, out _));
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => handler.ValidateToken(slashAudience, parameters, out _));
        return [issuer, audience, expired, positive, slashAudience];
    }
}
