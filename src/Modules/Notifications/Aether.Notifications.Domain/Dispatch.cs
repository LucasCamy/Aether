namespace Aether.Notifications.Domain;

public enum ExecutionState { Scheduled, BlockedByPolicy, Pending, InFlight, RetryScheduled,
    OutcomeUnknown, ProviderAccepted, Failed, Cancelled, Expired }

public sealed class Dispatch
{
    private Dispatch() { }
    public Dispatch(Guid tenantId, Guid id, Guid requestId, DateTimeOffset createdAt,
        DateTimeOffset expiresAt, string email, string content)
    {
        if (tenantId == Guid.Empty || id == Guid.Empty || requestId == Guid.Empty)
            throw new ArgumentException("Identidades obrigatórias.");
        if (expiresAt <= createdAt) throw new ArgumentException("Validade deve ser futura.");
        TenantId = tenantId;
        Id = id;
        RequestId = requestId;
        CreatedAt = createdAt;
        CreatedMonth = new DateOnly(createdAt.UtcDateTime.Year, createdAt.UtcDateTime.Month, 1);
        ExpiresAt = expiresAt;
        Recipient = email;
        Content = content;
    }
    public Guid TenantId { get; private set; }
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public DateOnly CreatedMonth { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public string Channel { get; private set; } = "email";
    public string Recipient { get; private set; } = "";
    public string Content { get; private set; } = "{}";
    public ExecutionState State { get; private set; } = ExecutionState.Pending;
    public string DeliveryState { get; private set; } = "unknown";
    public int Generation { get; private set; }
    public int AttemptCount { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public long Version { get; private set; }

    public Guid BeginAttempt(DateTimeOffset now, TimeSpan lease)
    {
        if (State is not (ExecutionState.Pending or ExecutionState.RetryScheduled))
            throw new InvalidOperationException("Dispatch não elegível.");
        if (NextAttemptAt > now || now >= ExpiresAt || lease <= TimeSpan.Zero)
            throw new InvalidOperationException("Fora da elegibilidade.");
        State = ExecutionState.InFlight;
        AttemptCount++;
        LeaseToken = Guid.NewGuid();
        LeaseUntil = now + lease;
        Version++;
        return LeaseToken.Value;
    }

    public void Finish(Guid token, ExecutionState outcome, DateTimeOffset now, DateTimeOffset? retryAt = null)
    {
        if (State != ExecutionState.InFlight || LeaseToken != token || now >= LeaseUntil)
            throw new InvalidOperationException("Reserva inválida.");
        if (outcome is not (ExecutionState.ProviderAccepted or ExecutionState.Failed
            or ExecutionState.OutcomeUnknown or ExecutionState.RetryScheduled))
            throw new InvalidOperationException("Resultado inválido.");
        if (outcome == ExecutionState.RetryScheduled && (retryAt is null || retryAt <= now || retryAt >= ExpiresAt))
            throw new InvalidOperationException("Retry inválido.");
        State = outcome;
        NextAttemptAt = outcome == ExecutionState.RetryScheduled ? retryAt : null;
        if (outcome == ExecutionState.RetryScheduled) Generation++;
        LeaseToken = null;
        LeaseUntil = null;
        Version++;
    }

    public void RecoverExpiredLease(DateTimeOffset now)
    {
        if (State != ExecutionState.InFlight || LeaseUntil > now)
            throw new InvalidOperationException("Reserva ainda válida.");
        State = ExecutionState.OutcomeUnknown;
        LeaseToken = null;
        LeaseUntil = null;
        Version++;
    }

    public void Cancel()
    {
        if (State is not (ExecutionState.Pending or ExecutionState.Scheduled
            or ExecutionState.BlockedByPolicy or ExecutionState.RetryScheduled))
            throw new InvalidOperationException("Cancelamento não permitido.");
        State = ExecutionState.Cancelled;
        Version++;
    }

    public void Expire(DateTimeOffset now)
    {
        if (now < ExpiresAt) throw new InvalidOperationException("Dispatch ainda válido.");
        if (State is ExecutionState.Pending or ExecutionState.Scheduled or ExecutionState.RetryScheduled or ExecutionState.BlockedByPolicy)
        {
            State = ExecutionState.Expired;
            Version++;
        }
    }
}
