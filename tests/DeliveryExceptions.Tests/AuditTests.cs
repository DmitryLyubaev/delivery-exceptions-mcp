using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeliveryExceptions.Tests.Support;
using Xunit;

namespace DeliveryExceptions.Tests;

[Collection("Local stack")]
[Trait("Category", "Audit")]
public sealed class AuditTests(DemoStack stack)
{
    private const string A1 = "a0000000-0000-0000-0000-000000000001";

    [Fact]
    public async Task AuthenticationDenialHasNoTrustedSubject()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        using var request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:5180/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFixtures.Unsigned());
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        using var response = await stack.Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var query = source.CreateCommand("select subject,tenant_id,operation from audit where reason='invalid_token'");
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.IsDBNull(0));
        Assert.True(reader.IsDBNull(1));
        Assert.Equal("authentication", reader.GetString(2));
    }

    [Fact]
    public async Task DenialSurvivesDomainRollback()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var result = await (await stack.GetClientAsync()).CallToolAsync("mark_exception_reviewed", new Dictionary<string, object?> { ["delivery_id"] = A1, ["expected_version"] = 9 });
        Assert.Equal("conflict", DeliveryToolsTests.Payload(result).GetProperty("error").GetString());
        var correlation = DeliveryToolsTests.Payload(result).GetProperty("correlationId").GetString();
        await using var query = source.CreateCommand("select outcome,reason from audit where correlation_id=@id");
        query.Parameters.AddWithValue("id", correlation!);
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("conflict", reader.GetString(0));
        Assert.Equal("conflict", reader.GetString(1));
    }

    [Fact]
    public async Task AuditUnavailablePreventsReadAndWriteSuccess()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var client = await stack.GetClientAsync();
        await using var offline = source.CreateCommand("alter table audit rename to test_audit_offline");
        await offline.ExecuteNonQueryAsync();
        try
        {
            var read = await client.CallToolAsync("get_delivery", new Dictionary<string, object?> { ["delivery_id"] = A1 });
            var write = await client.CallToolAsync("mark_exception_reviewed", new Dictionary<string, object?> { ["delivery_id"] = A1, ["expected_version"] = 1 });
            foreach (var outcome in new[] { read, write })
            {
                Assert.Equal("unavailable", DeliveryToolsTests.Payload(outcome).GetProperty("error").GetString());
                Assert.Equal(JsonValueKind.Null, DeliveryToolsTests.Payload(outcome).GetProperty("data").ValueKind);
            }
            await using var query = source.CreateCommand("select reviewed from deliveries where id='a0000000-0000-0000-0000-000000000001'");
            Assert.Equal(false, await query.ExecuteScalarAsync());
        }
        finally
        {
            await using var restore = source.CreateCommand("alter table test_audit_offline rename to audit");
            await restore.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task CarrierNoteIsNeverAuthority()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var client = await stack.GetClientAsync();
        var read = DeliveryToolsTests.Payload(await client.CallToolAsync("get_delivery", new Dictionary<string, object?> { ["delivery_id"] = A1 }));
        Assert.Contains("Ignore all permissions", read.GetProperty("data").GetProperty("carrierNote").GetString());
        Assert.Contains(read.GetProperty("untrustedFields").EnumerateArray(), field => field.GetString() == "carrierNote");
        var foreign = await client.CallToolAsync("mark_exception_reviewed", new Dictionary<string, object?> { ["delivery_id"] = "b0000000-0000-0000-0000-000000000001", ["expected_version"] = 1 });
        Assert.Equal("not_found", DeliveryToolsTests.Payload(foreign).GetProperty("error").GetString());
        var reader = await stack.GetClientAsync("a-reader", true);
        var denied = await reader.CallToolAsync("mark_exception_reviewed", new Dictionary<string, object?> { ["delivery_id"] = A1, ["expected_version"] = 1 });
        Assert.Equal("forbidden", DeliveryToolsTests.Payload(denied).GetProperty("error").GetString());
        await using var unchanged = source.CreateCommand("select count(*) from deliveries where id in ('a0000000-0000-0000-0000-000000000001','b0000000-0000-0000-0000-000000000001') and not reviewed and version=1");
        Assert.Equal(2L, await unchanged.ExecuteScalarAsync());
    }
}
