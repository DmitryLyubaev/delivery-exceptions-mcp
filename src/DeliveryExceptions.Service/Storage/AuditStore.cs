using Npgsql;
using DeliveryExceptions.Service.Deliveries;

namespace DeliveryExceptions.Service.Storage;

public sealed class AuditStore
{
    public async Task AppendAsync(AuditEvent entry, NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into audit(correlation_id,at,issuer,subject,tenant_id,operation,delivery_id,outcome,reason,before_version,after_version) values (@correlation,@at,@issuer,@subject,@tenant,@operation,@delivery,@outcome,@reason,@before,@after)", connection, transaction);
        void Add(string name, NpgsqlTypes.NpgsqlDbType type, object? value) => command.Parameters.AddWithValue(name, type, value ?? DBNull.Value);
        Add("correlation", NpgsqlTypes.NpgsqlDbType.Text, entry.CorrelationId);
        Add("at", NpgsqlTypes.NpgsqlDbType.TimestampTz, entry.At);
        Add("issuer", NpgsqlTypes.NpgsqlDbType.Text, entry.Issuer);
        Add("subject", NpgsqlTypes.NpgsqlDbType.Text, entry.Subject);
        Add("tenant", NpgsqlTypes.NpgsqlDbType.Uuid, entry.TenantId);
        Add("operation", NpgsqlTypes.NpgsqlDbType.Text, entry.Operation);
        Add("delivery", NpgsqlTypes.NpgsqlDbType.Uuid, entry.DeliveryId);
        Add("outcome", NpgsqlTypes.NpgsqlDbType.Text, entry.Outcome);
        Add("reason", NpgsqlTypes.NpgsqlDbType.Text, entry.Reason);
        Add("before", NpgsqlTypes.NpgsqlDbType.Bigint, entry.BeforeVersion);
        Add("after", NpgsqlTypes.NpgsqlDbType.Bigint, entry.AfterVersion);
        await command.ExecuteNonQueryAsync(ct);
    }
}
