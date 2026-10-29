using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace DeliveryExceptions.Tests.Support;

public static class TokenFixtures
{
    public static string Unsigned() => Base64UrlEncoder.Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}") + "." +
        Base64UrlEncoder.Encode(JsonSerializer.Serialize(new { iss = "http://127.0.0.1:5182/realms/delivery-demo", sub = "20000000-0000-0000-0000-000000000001", scope = "deliveries.read exceptions.review", exp = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds() })) + ".";
}
