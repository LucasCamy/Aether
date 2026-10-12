namespace Aether.ProviderManagement.Application;

public interface ISecretStore
{
    Task<string?> ReadAsync(string reference, CancellationToken cancellationToken);
}
