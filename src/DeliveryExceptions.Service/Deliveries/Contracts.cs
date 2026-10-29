namespace DeliveryExceptions.Service.Deliveries;

public sealed record DeliveryView(Guid Id, string Reference, string Status, string CarrierNote,
    bool Reviewed, long Version, string? ReviewedBy, DateTimeOffset? ReviewedAt);
public sealed record ToolOutcome<T>(T? Value, string? ErrorCode, string CorrelationId);
public sealed record AuditEvent(string CorrelationId, DateTimeOffset At, string? Issuer,
    string? Subject, Guid? TenantId, string Operation, Guid? DeliveryId, string Outcome,
    string Reason, long? BeforeVersion, long? AfterVersion);
