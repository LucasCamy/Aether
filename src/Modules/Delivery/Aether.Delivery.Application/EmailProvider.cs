namespace Aether.Delivery.Application;

public enum ProviderOutcome { Accepted, PermanentFailure, Throttled, Unknown, ConfigurationError }
public sealed record EmailWorkItem(Guid TenantId, Guid DispatchId, string Recipient,
    string Subject, string? Text, string? Html);
public sealed record ProviderResult(ProviderOutcome Outcome, string? MessageId = null,
    string? ErrorCode = null, TimeSpan? RetryAfter = null);
public sealed class EmailAccount(string apiKey, string sender)
{
    public string ApiKey { get; } = apiKey;
    public string Sender { get; } = sender;
    public override string ToString() => "EmailAccount(redacted)";
}
public interface IEmailAccountResolver
{
    Task<EmailAccount?> ResolveAsync(Guid tenantId, CancellationToken cancellationToken);
}
public interface IEmailProvider
{
    Task<ProviderResult> SendAsync(EmailWorkItem item, CancellationToken cancellationToken);
}
