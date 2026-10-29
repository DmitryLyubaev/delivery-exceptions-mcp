using System.Security.Claims;
using DeliveryExceptions.Service.Auth;
using Xunit;

namespace DeliveryExceptions.Tests;

[Trait("Category", "Policy")]
public sealed class AccessPolicyTests
{
    [Fact]
    public void ValidActorPreservesExactIdentityAndScopes()
    {
        var actor = Actor.FromPrincipal(Principal());
        Assert.NotNull(actor);
        Assert.Equal("http://127.0.0.1:5182/realms/delivery-demo", actor.Issuer);
        Assert.Equal("10000000-0000-0000-0000-000000000001", actor.Subject);
        Assert.Contains("deliveries.read", actor.Scopes);
    }

    [Theory]
    [InlineData("sub")]
    [InlineData("iss")]
    [InlineData("scope")]
    public void MalformedOrDuplicateIdentityDenied(string duplicate)
    {
        var identity = (ClaimsIdentity)Principal().Identity!;
        identity.AddClaim(new(duplicate, identity.FindFirst(duplicate)!.Value));
        Assert.Null(Actor.FromPrincipal(new(identity)));
        Assert.Null(Actor.FromPrincipal(new(new ClaimsIdentity())));
    }

    [Fact]
    public void ScopeNamesRequireExactMatch()
    {
        var principal = Principal("deliveries.read.extra exceptions.review");
        var actor = Actor.FromPrincipal(principal);
        Assert.NotNull(actor);
        Assert.DoesNotContain("deliveries.read", actor.Scopes);
        Assert.Contains("exceptions.review", actor.Scopes);
    }

    internal static ClaimsPrincipal Principal(string scope = "deliveries.read") => new(new ClaimsIdentity([
        new("iss", "http://127.0.0.1:5182/realms/delivery-demo"),
        new("sub", "10000000-0000-0000-0000-000000000001"), new("scope", scope)
    ], "Bearer"));
}
