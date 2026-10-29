using Npgsql;

namespace DeliveryExceptions.Service.Deliveries;

public sealed class DeliveryRepository(NpgsqlDataSource source)
{
    internal const string Columns = "id,reference,status,carrier_note,reviewed,version,reviewed_by,reviewed_at";

    public async Task<IReadOnlyList<DeliveryView>> ListDelayedAsync(Guid tenantId, CancellationToken ct)
    {
        await using var command = source.CreateCommand($"select {Columns} from deliveries where tenant_id=@tenant and status='delayed' order by id");
        command.Parameters.AddWithValue("tenant", tenantId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var records = new List<DeliveryView>();
        while (await reader.ReadAsync(ct)) records.Add(Read(reader));
        return records;
    }

    public async Task<DeliveryView?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await using var command = source.CreateCommand($"select {Columns} from deliveries where tenant_id=@tenant and id=@id");
        command.Parameters.AddWithValue("tenant", tenantId);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Read(reader) : null;
    }

    internal static DeliveryView Read(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetString(1),
        reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetInt64(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
}
