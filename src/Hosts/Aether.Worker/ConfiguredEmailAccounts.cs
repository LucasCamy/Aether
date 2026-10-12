using Aether.Delivery.Application;
using Aether.ProviderManagement.Application;
using Microsoft.Extensions.Configuration;

namespace Aether.Worker;

public sealed class ConfiguredEmailAccounts(IConfiguration configuration, ISecretStore secrets) : IEmailAccountResolver
{
    public async Task<EmailAccount?> ResolveAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var section = configuration.GetSection("Email:Accounts:" + tenantId.ToString("D"));
        var sender = section["Sender"];
        var reference = section["ApiKeySecret"];
        if (string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(reference)) return null;
        var key = await secrets.ReadAsync(reference, cancellationToken);
        if (string.IsNullOrWhiteSpace(key)) return null;
        return new EmailAccount(key, sender);
    }
}
