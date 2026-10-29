using DeliveryExceptions.Tests.Support;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace DeliveryExceptions.Tests;

[Collection("Local stack")]
[Trait("Category", "Tools")]
public sealed class DeliveryToolsTests(DemoStack stack)
{
    private const string A1 = "a0000000-0000-0000-0000-000000000001";
    private const string B1 = "b0000000-0000-0000-0000-000000000001";
    private static Dictionary<string, object?> Arguments(string id = A1, object? version = null) => new() { ["delivery_id"] = id, ["expected_version"] = version ?? 1L };
    internal static JsonElement Payload(CallToolResult result) => result.StructuredContent!.Value;

    [Fact]
    public async Task ThreeToolsHaveNoTenantArgument()
    {
        var client = await stack.GetClientAsync();
        var tools = await client.ListToolsAsync();
        Assert.Equal(new[] { "get_delivery", "list_delayed_deliveries", "mark_exception_reviewed" }, tools.Select(tool => tool.Name).Order().ToArray());
        foreach (var tool in tools)
        {
            Assert.False(tool.ProtocolTool.InputSchema.GetProperty("additionalProperties").GetBoolean());
            Assert.DoesNotContain(tool.ProtocolTool.InputSchema.GetProperty("properties").EnumerateObject(), property => property.Name is "tenant_id" or "actor" or "subject");
        }
    }

    [Fact]
    public async Task OperatorReviewsOwnDelayedRecord()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var client = await stack.GetClientAsync();
        var result = await client.CallToolAsync("mark_exception_reviewed", Arguments());
        Assert.NotEqual(true, result.IsError);
        var data = Payload(result).GetProperty("data");
        Assert.True(data.GetProperty("reviewed").GetBoolean());
        Assert.Equal(2, data.GetProperty("version").GetInt64());
        Assert.Equal("10000000-0000-0000-0000-000000000001", data.GetProperty("reviewedBy").GetString());
        Assert.Equal(TimeSpan.Zero, data.GetProperty("reviewedAt").GetDateTimeOffset().Offset);
        await using var count = source.CreateCommand("select count(*) from audit where operation='mark_exception_reviewed' and outcome='allow'");
        Assert.Equal(1L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ReaderAndReadOnlyTokenCannotWrite()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        foreach (var client in new[] { await stack.GetClientAsync("a-reader", true), await stack.GetClientAsync("a-operator", false) })
        {
            var result = await client.CallToolAsync("mark_exception_reviewed", Arguments());
            Assert.Equal(true, result.IsError);
            Assert.Equal("forbidden", Payload(result).GetProperty("error").GetString());
        }
        await AssertUnchangedAsync(source);
    }

    [Fact]
    public async Task ForeignWriteIsNotFound()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var client = await stack.GetClientAsync();
        var result = await client.CallToolAsync("mark_exception_reviewed", Arguments(B1));
        Assert.Equal(true, result.IsError);
        Assert.Equal("not_found", Payload(result).GetProperty("error").GetString());
        await AssertUnchangedAsync(source, B1);
    }

    [Fact]
    public async Task ForeignConflictDoesNotExposeVersion()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        await using var change = source.CreateCommand("update deliveries set version=99 where id='b0000000-0000-0000-0000-000000000001'");
        await change.ExecuteNonQueryAsync();
        var result = await (await stack.GetClientAsync()).CallToolAsync("mark_exception_reviewed", Arguments(B1));
        Assert.Equal("not_found", Payload(result).GetProperty("error").GetString());
        Assert.Equal(new[] { "correlationId", "data", "error", "untrustedFields" }, Payload(result).EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Matches("^[0-9a-f]{32}$", Payload(result).GetProperty("correlationId").GetString());
        Assert.Equal(JsonValueKind.Null, Payload(result).GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task SameVersionConcurrentWritesYieldOneSuccess()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var client = await stack.GetClientAsync();
        var outcomes = await Task.WhenAll(client.CallToolAsync("mark_exception_reviewed", Arguments()).AsTask(), client.CallToolAsync("mark_exception_reviewed", Arguments()).AsTask());
        Assert.Single(outcomes, result => result.IsError != true);
        Assert.Single(outcomes, result => Payload(result).GetProperty("error").GetString() == "conflict");
        await using var count = source.CreateCommand("select count(*) from audit where operation='mark_exception_reviewed' and outcome='allow'");
        Assert.Equal(1L, await count.ExecuteScalarAsync());
    }

    [Fact]
    public async Task RepeatedOrStaleReviewConflicts()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var client = await stack.GetClientAsync();
        Assert.Equal("conflict", Payload(await client.CallToolAsync("mark_exception_reviewed", Arguments(A1, 2))).GetProperty("error").GetString());
        Assert.NotEqual(true, (await client.CallToolAsync("mark_exception_reviewed", Arguments())).IsError);
        Assert.Equal("conflict", Payload(await client.CallToolAsync("mark_exception_reviewed", Arguments())).GetProperty("error").GetString());
    }

    [Fact]
    public async Task OnTimeAndReviewedConflict()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var client = await stack.GetClientAsync();
        foreach (var suffix in new[] { "2", "3" })
        {
            var result = await client.CallToolAsync("mark_exception_reviewed", Arguments("a0000000-0000-0000-0000-00000000000" + suffix));
            Assert.Equal("conflict", Payload(result).GetProperty("error").GetString());
        }
    }

    [Fact]
    public async Task InvalidVersionOrOversizedIdRejected()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var client = await stack.GetClientAsync();
        foreach (var version in new object[] { 0, -1, "1", 1.5 })
            Assert.Equal("invalid_argument", Payload(await client.CallToolAsync("mark_exception_reviewed", Arguments(A1, version))).GetProperty("error").GetString());
        Assert.Equal("invalid_argument", Payload(await client.CallToolAsync("mark_exception_reviewed", Arguments(new string('a', 2000)))).GetProperty("error").GetString());
        await AssertUnchangedAsync(source);
    }

    [Fact]
    public async Task ExtraTenantArgumentRejected()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        var args = Arguments();
        args["tenant_id"] = "bbbbbbbb-0000-0000-0000-000000000001";
        var result = await (await stack.GetClientAsync()).CallToolAsync("mark_exception_reviewed", args);
        Assert.Equal("invalid_argument", Payload(result).GetProperty("error").GetString());
        await AssertUnchangedAsync(source);
    }

    [Fact]
    public async Task AuditInsertFailureRollsBackReview()
    {
        await using var source = DemoDatabase.Source();
        await DeliveryRepositoryTests.ResetAsync(source);
        await using var reject = source.CreateCommand("alter table audit add constraint test_reject_allow check(outcome<>'allow')");
        await reject.ExecuteNonQueryAsync();
        try
        {
            var result = await (await stack.GetClientAsync()).CallToolAsync("mark_exception_reviewed", Arguments());
            Assert.Equal("unavailable", Payload(result).GetProperty("error").GetString());
            await AssertUnchangedAsync(source);
        }
        finally
        {
            await using var restore = source.CreateCommand("alter table audit drop constraint test_reject_allow");
            await restore.ExecuteNonQueryAsync();
        }
    }

    private static async Task AssertUnchangedAsync(NpgsqlDataSource source, string id = A1)
    {
        await using var query = source.CreateCommand("select reviewed,version from deliveries where id=@id");
        query.Parameters.AddWithValue("id", Guid.Parse(id));
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.False(reader.GetBoolean(0));
        Assert.Equal(1L, reader.GetInt64(1));
    }
}
