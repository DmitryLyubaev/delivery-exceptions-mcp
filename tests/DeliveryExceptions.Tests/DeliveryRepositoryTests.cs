using DeliveryExceptions.Service.Storage;
using DeliveryExceptions.Tests.Support;
using Npgsql;
using Xunit;
using DeliveryExceptions.Service.Auth;
using DeliveryExceptions.Service.Deliveries;

namespace DeliveryExceptions.Tests;

[Collection("Local stack")]
[Trait("Category", "Database")]
public sealed class DeliveryRepositoryTests(DemoStack stack)
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid A1 = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid B1 = Guid.Parse("b0000000-0000-0000-0000-000000000001");

    internal static async Task ResetAsync(NpgsqlDataSource source)
    {
        var database = new Database(source);
        await database.MigrateAsync(CancellationToken.None);
        await using var connection = await source.OpenConnectionAsync();
        await using var reset = new NpgsqlCommand("truncate audit,deliveries,memberships,tenants restart identity cascade", connection);
        await reset.ExecuteNonQueryAsync();
        await database.SeedAsync(Path.Combine(DemoStack.Root, "fixtures"), CancellationToken.None);
    }

    [Fact]
    public async Task MigrationCreatesRequiredTables()
    {
        await using var source = DemoDatabase.Source();
        await new Database(source).MigrateAsync(CancellationToken.None);
        await using var connection = await source.OpenConnectionAsync();
        await using var query = new NpgsqlCommand("select to_regclass('public.deliveries')::text", connection);
        Assert.Equal("deliveries", await query.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ReadersSeeOnlyOwnTenant()
    {
        await using var source = DemoDatabase.Source();
        await ResetAsync(source);
        var repository = new DeliveryRepository(source);
        var a = await repository.ListDelayedAsync(TenantA, CancellationToken.None);
        var b = await repository.ListDelayedAsync(TenantB, CancellationToken.None);
        Assert.Equal(2, a.Count);
        Assert.Equal(2, b.Count);
        Assert.All(a, record => Assert.StartsWith("A-", record.Reference));
        Assert.All(b, record => Assert.StartsWith("B-", record.Reference));
        Assert.NotNull(await repository.GetAsync(TenantA, A1, CancellationToken.None));
    }

    [Fact]
    public async Task KnownForeignAndMissingHaveSameResult()
    {
        await using var source = DemoDatabase.Source();
        await ResetAsync(source);
        var repository = new DeliveryRepository(source);
        Assert.Null(await repository.GetAsync(TenantA, B1, CancellationToken.None));
        Assert.Null(await repository.GetAsync(TenantA, Guid.Empty, CancellationToken.None));
        Assert.NotNull(await repository.GetAsync(TenantB, B1, CancellationToken.None));
    }

    [Fact]
    public async Task MissingMembershipDenied()
    {
        await using var source = DemoDatabase.Source();
        await ResetAsync(source);
        var policy = new AccessPolicy(source);
        var actor = Actor.FromPrincipal(AccessPolicyTests.Principal())!;
        Assert.Null(await policy.ResolveAsync(actor with { Subject = "unknown" }, false, CancellationToken.None));
        Assert.NotNull(await policy.ResolveAsync(actor, false, CancellationToken.None));
    }

    [Fact]
    public async Task ReaderCannotAcquireOperatorEntitlement()
    {
        await using var source = DemoDatabase.Source();
        await ResetAsync(source);
        var policy = new AccessPolicy(source);
        var actor = Actor.FromPrincipal(AccessPolicyTests.Principal("deliveries.read exceptions.review"))! with { Subject = "10000000-0000-0000-0000-000000000002" };
        Assert.Null(await policy.ResolveAsync(actor, true, CancellationToken.None));
        Assert.NotNull(await policy.ResolveAsync(actor, false, CancellationToken.None));
    }

    [Fact]
    public async Task OperatorReadScopeCannotReview()
    {
        await using var source = DemoDatabase.Source();
        await ResetAsync(source);
        var policy = new AccessPolicy(source);
        var actor = Actor.FromPrincipal(AccessPolicyTests.Principal())!;
        Assert.Null(await policy.ResolveAsync(actor, true, CancellationToken.None));
        Assert.NotNull(await policy.ResolveAsync(actor with { Scopes = new HashSet<string> { "deliveries.read", "exceptions.review" } }, true, CancellationToken.None));
    }

    [Fact]
    public async Task OrdinaryStartupDoesNotReseed()
    {
        await using var source = DemoDatabase.Source();
        await ResetAsync(source);
        await using var connection = await source.OpenConnectionAsync();
        await using var update = new NpgsqlCommand("update deliveries set version=9 where id=@id", connection);
        update.Parameters.AddWithValue("id", A1);
        await update.ExecuteNonQueryAsync();
        await new Database(source).MigrateAsync(CancellationToken.None);
        await new Database(source).SeedAsync(Path.Combine(DemoStack.Root, "fixtures"), CancellationToken.None);
        var previousPid = stack.ServiceProcessId;
        await stack.RestartAsync();
        Assert.NotEqual(previousPid, stack.ServiceProcessId);
        Assert.Equal(9, (await new DeliveryRepository(source).GetAsync(TenantA, A1, CancellationToken.None))?.Version);
    }

    [Fact]
    public async Task AuditAppendUsesCallerTransaction()
    {
        await using var source = DemoDatabase.Source();
        await ResetAsync(source);
        await using var connection = await source.OpenConnectionAsync();
        var entry = new AuditEvent("test-correlation", DateTimeOffset.UtcNow, "test-issuer", "test-subject", TenantA, "test", A1, "allow", "test", 1, 2);
        await using (var rollback = await connection.BeginTransactionAsync())
        {
            await new AuditStore().AppendAsync(entry, connection, rollback, CancellationToken.None);
            await rollback.RollbackAsync();
        }
        await using var query = new NpgsqlCommand("select count(*) from audit", connection);
        Assert.Equal(0L, await query.ExecuteScalarAsync());
        await new AuditStore().AppendAsync(entry, connection, null, CancellationToken.None);
        Assert.Equal(1L, await query.ExecuteScalarAsync());
    }
}
