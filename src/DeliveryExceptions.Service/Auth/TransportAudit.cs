using DeliveryExceptions.Service.Deliveries;
using DeliveryExceptions.Service.Storage;
using Npgsql;

namespace DeliveryExceptions.Service.Auth;

public sealed class TransportAudit(NpgsqlDataSource source, AuditStore audit)
{
    public async Task RecordAsync(AuditEvent entry, CancellationToken ct)
    {
        await using var connection = await source.OpenConnectionAsync(ct);
        await audit.AppendAsync(entry, connection, null, ct);
    }
}
