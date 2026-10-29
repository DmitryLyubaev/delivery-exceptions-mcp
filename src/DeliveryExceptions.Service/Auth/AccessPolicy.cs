using Npgsql;

namespace DeliveryExceptions.Service.Auth;

public sealed class AccessPolicy(NpgsqlDataSource source)
{
    public async Task<Membership?> ResolveAsync(Actor actor, bool review, CancellationToken ct)
    {
        if (!actor.Scopes.Contains("deliveries.read") || (review && !actor.Scopes.Contains("exceptions.review"))) return null;
        await using var command = source.CreateCommand("select tenant_id,can_read,can_review from memberships where issuer=@issuer and subject=@subject");
        command.Parameters.AddWithValue("issuer", actor.Issuer);
        command.Parameters.AddWithValue("subject", actor.Subject);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var membership = new Membership(reader.GetGuid(0), reader.GetBoolean(1), reader.GetBoolean(2));
        return membership.CanRead && (!review || membership.CanReview) ? membership : null;
    }
}
