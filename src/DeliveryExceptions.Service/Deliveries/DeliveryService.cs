using DeliveryExceptions.Service.Auth;
using DeliveryExceptions.Service.Storage;
using Npgsql;

namespace DeliveryExceptions.Service.Deliveries;

public sealed class DeliveryService(NpgsqlDataSource source, AccessPolicy policy, DeliveryRepository repository, AuditStore audit)
{
    public async Task<ToolOutcome<IReadOnlyList<DeliveryView>>> ListAsync(Actor actor, CancellationToken ct)
    {
        var correlation = Guid.NewGuid().ToString("N");
        try
        {
            var membership = await policy.ResolveAsync(actor, false, ct);
            if (membership is null) return new(null, "forbidden", correlation);
            return new(await repository.ListDelayedAsync(membership.TenantId, ct), null, correlation);
        }
        catch (NpgsqlException) { return new(null, "unavailable", correlation); }
        catch (TimeoutException) { return new(null, "unavailable", correlation); }
    }

    public async Task<ToolOutcome<DeliveryView>> GetAsync(Actor actor, Guid id, CancellationToken ct)
    {
        var correlation = Guid.NewGuid().ToString("N");
        try
        {
            var membership = await policy.ResolveAsync(actor, false, ct);
            if (membership is null) return new(null, "forbidden", correlation);
            var value = await repository.GetAsync(membership.TenantId, id, ct);
            return new(value, value is null ? "not_found" : null, correlation);
        }
        catch (NpgsqlException) { return new(null, "unavailable", correlation); }
        catch (TimeoutException) { return new(null, "unavailable", correlation); }
    }

    public async Task<ToolOutcome<DeliveryView>> ReviewAsync(Actor actor, Guid id, long expectedVersion, CancellationToken ct)
    {
        var correlation = Guid.NewGuid().ToString("N");
        if (expectedVersion <= 0) return new(null, "invalid_argument", correlation);
        try
        {
            var membership = await policy.ResolveAsync(actor, true, ct);
            if (membership is null) return new(null, "forbidden", correlation);
            await using var connection = await source.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            DeliveryView? current;
            await using (var read = new NpgsqlCommand($"select {DeliveryRepository.Columns} from deliveries where tenant_id=@tenant and id=@id for update", connection, transaction))
            {
                read.Parameters.AddWithValue("tenant", membership.TenantId);
                read.Parameters.AddWithValue("id", id);
                await using var reader = await read.ExecuteReaderAsync(ct);
                current = await reader.ReadAsync(ct) ? DeliveryRepository.Read(reader) : null;
            }
            if (current is null) return new(null, "not_found", correlation);
            if (current.Reviewed || current.Status != "delayed" || current.Version != expectedVersion)
                return new(null, "conflict", correlation);
            DeliveryView updated;
            await using (var update = new NpgsqlCommand($"update deliveries set reviewed=true,reviewed_by=@subject,reviewed_at=@now,version=version+1 where tenant_id=@tenant and id=@id and version=@version and not reviewed and status='delayed' returning {DeliveryRepository.Columns}", connection, transaction))
            {
                update.Parameters.AddWithValue("tenant", membership.TenantId);
                update.Parameters.AddWithValue("id", id);
                update.Parameters.AddWithValue("version", expectedVersion);
                update.Parameters.AddWithValue("subject", actor.Subject);
                update.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
                await using var reader = await update.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return new(null, "conflict", correlation);
                updated = DeliveryRepository.Read(reader);
            }
            await audit.AppendAsync(new(correlation, DateTimeOffset.UtcNow, actor.Issuer, actor.Subject,
                membership.TenantId, "mark_exception_reviewed", id, "allow", "reviewed", current.Version,
                updated.Version), connection, transaction, ct);
            await transaction.CommitAsync(ct);
            return new(updated, null, correlation);
        }
        catch (NpgsqlException) { return new(null, "unavailable", correlation); }
        catch (TimeoutException) { return new(null, "unavailable", correlation); }
    }
}
