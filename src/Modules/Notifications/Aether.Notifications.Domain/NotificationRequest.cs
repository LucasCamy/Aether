namespace Aether.Notifications.Domain;

public sealed class NotificationRequest
{
    private NotificationRequest() { }
    public NotificationRequest(Guid tenantId, Guid id, Guid correlationId,
        DateTimeOffset acceptedAt, DateTimeOffset expiresAt, string priority,
        string? clientReference, string metadata)
    {
        if (tenantId == Guid.Empty || id == Guid.Empty || correlationId == Guid.Empty)
            throw new ArgumentException("Identidades obrigatórias.");
        if (expiresAt <= acceptedAt) throw new ArgumentException("Validade deve ser futura.");
        TenantId = tenantId;
        Id = id;
        CorrelationId = correlationId;
        AcceptedAt = acceptedAt;
        ExpiresAt = expiresAt;
        Priority = priority;
        ClientReference = clientReference;
        Metadata = metadata;
    }
    public Guid TenantId { get; private set; }
    public Guid Id { get; private set; }
    public Guid CorrelationId { get; private set; }
    public DateTimeOffset AcceptedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string Priority { get; private set; } = "";
    public string? ClientReference { get; private set; }
    public string Metadata { get; private set; } = "{}";
}
