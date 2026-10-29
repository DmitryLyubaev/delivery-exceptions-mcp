using Npgsql;
using System.Security.Cryptography;
using System.Text.Json;

namespace DeliveryExceptions.Service.Storage;

public sealed class Database(NpgsqlDataSource source)
{
    public async Task MigrateAsync(CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var prepare = new NpgsqlCommand("create table if not exists migration_versions(version integer primary key,sha256 text not null)", connection, transaction);
        await prepare.ExecuteNonQueryAsync(ct);
        await using var stream = typeof(Database).Assembly.GetManifestResourceStream("DeliveryExceptions.Service.Storage.Migrations.001_initial.sql") ?? throw new InvalidOperationException("Migration missing");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync(ct);
        var hash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql)));
        await using var check = new NpgsqlCommand("select sha256 from migration_versions where version=1", connection, transaction);
        var existing = await check.ExecuteScalarAsync(ct);
        if (existing is string oldHash && oldHash != hash) throw new InvalidOperationException("Migration hash changed");
        if (existing is null)
        {
            await using var migration = new NpgsqlCommand(sql, connection, transaction);
            await migration.ExecuteNonQueryAsync(ct);
            await using var record = new NpgsqlCommand("insert into migration_versions values (1,@hash)", connection, transaction);
            record.Parameters.AddWithValue("hash", hash);
            await record.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }

    public async Task SeedAsync(string fixtureRoot, CancellationToken ct)
    {
        using var members = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixtureRoot, "memberships.json"), ct));
        using var deliveries = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(fixtureRoot, "deliveries.json"), ct));
        await using var connection = await source.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        foreach (var member in members.RootElement.EnumerateArray())
        {
            await using var tenant = new NpgsqlCommand("insert into tenants values (@id,@name) on conflict do nothing", connection, transaction);
            tenant.Parameters.AddWithValue("id", member.GetProperty("tenantId").GetGuid());
            tenant.Parameters.AddWithValue("name", member.GetProperty("tenantName").GetString()!);
            await tenant.ExecuteNonQueryAsync(ct);
            await using var membership = new NpgsqlCommand("insert into memberships values (@issuer,@subject,@tenant,@read,@review) on conflict do nothing", connection, transaction);
            membership.Parameters.AddWithValue("issuer", member.GetProperty("issuer").GetString()!);
            membership.Parameters.AddWithValue("subject", member.GetProperty("subject").GetString()!);
            membership.Parameters.AddWithValue("tenant", member.GetProperty("tenantId").GetGuid());
            membership.Parameters.AddWithValue("read", member.GetProperty("canRead").GetBoolean());
            membership.Parameters.AddWithValue("review", member.GetProperty("canReview").GetBoolean());
            await membership.ExecuteNonQueryAsync(ct);
        }
        foreach (var row in deliveries.RootElement.EnumerateArray())
        {
            await using var insert = new NpgsqlCommand("insert into deliveries values (@id,@tenant,@reference,@expected,@observed,@status,@note,@reviewed,@version,@by,@at) on conflict do nothing", connection, transaction);
            insert.Parameters.AddWithValue("id", row.GetProperty("id").GetGuid());
            insert.Parameters.AddWithValue("tenant", row.GetProperty("tenantId").GetGuid());
            insert.Parameters.AddWithValue("reference", row.GetProperty("reference").GetString()!);
            insert.Parameters.AddWithValue("expected", row.GetProperty("expectedAt").GetDateTimeOffset());
            insert.Parameters.AddWithValue("observed", NpgsqlTypes.NpgsqlDbType.TimestampTz, row.GetProperty("observedAt").ValueKind == JsonValueKind.Null ? DBNull.Value : row.GetProperty("observedAt").GetDateTimeOffset());
            insert.Parameters.AddWithValue("status", row.GetProperty("status").GetString()!);
            insert.Parameters.AddWithValue("note", row.GetProperty("carrierNote").GetString()!);
            insert.Parameters.AddWithValue("reviewed", row.GetProperty("reviewed").GetBoolean());
            insert.Parameters.AddWithValue("version", row.GetProperty("version").GetInt64());
            insert.Parameters.AddWithValue("by", NpgsqlTypes.NpgsqlDbType.Text, (object?)row.GetProperty("reviewedBy").GetString() ?? DBNull.Value);
            insert.Parameters.AddWithValue("at", NpgsqlTypes.NpgsqlDbType.TimestampTz, row.GetProperty("reviewedAt").ValueKind == JsonValueKind.Null ? DBNull.Value : row.GetProperty("reviewedAt").GetDateTimeOffset());
            await insert.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
