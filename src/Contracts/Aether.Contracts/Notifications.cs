using System.Text.Json;

namespace Aether.Contracts;

public sealed record SubmitNotificationRequest(
    string? ClientReference,
    string Priority,
    string DeliveryMode,
    RecipientRequest Recipient,
    MessageRequest[] Messages,
    DateTimeOffset ExpiresAt,
    JsonElement? Metadata);

public sealed record RecipientRequest(string Email);
public sealed record MessageRequest(string Channel, EmailContentRequest Content);
public sealed record EmailContentRequest(string Subject, string? Text, string? Html);
public sealed record NotificationAcceptedResponse(
    Guid NotificationId, string Status, DateTimeOffset AcceptedAt, int DispatchCount,
    DateTimeOffset IdempotencyExpiresAt, NotificationLinks Links);
public sealed record NotificationLinks(string Self, string Dispatches);
public sealed record DispatchStatusResponse(Guid DispatchId, string Channel, string ExecutionState, string DeliveryState);
public sealed record NotificationStatusResponse(Guid NotificationId, string Status,
    DateTimeOffset AcceptedAt, DateTimeOffset ExpiresAt, DispatchStatusResponse[] Dispatches);
public sealed record DispatchEnvelope(int SchemaVersion, Guid MessageId, Guid TenantId,
    Guid DispatchId, Guid NotificationId, Guid CorrelationId, int Generation);
