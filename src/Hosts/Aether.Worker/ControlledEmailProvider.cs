using Aether.Delivery.Application;

namespace Aether.Worker;

// Only registered in Development with explicit sandbox mode; never contacts an external service.
public sealed class ControlledEmailProvider : IEmailProvider
{
    public Task<ProviderResult> SendAsync(EmailWorkItem item, CancellationToken cancellationToken) =>
        Task.FromResult(new ProviderResult(ProviderOutcome.Accepted, "sandbox-" + item.DispatchId));
}
