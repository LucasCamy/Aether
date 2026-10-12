using Aether.ProviderManagement.Application;

namespace Aether.ProviderManagement.Infrastructure;

public sealed class MountedSecretStore(string directory) : ISecretStore
{
    private readonly string root = Path.GetFullPath(directory);
    public async Task<string?> ReadAsync(string reference, CancellationToken cancellationToken)
    {
        if (reference.Length is < 1 or > 100 || reference.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ArgumentException("Referência de segredo inválida.");
        var path = Path.Combine(root, reference);
        var info = new FileInfo(path);
        if (!info.Exists) return null;
        if (info.LinkTarget != null || info.Length > 8192) throw new InvalidOperationException("Arquivo de segredo inválido.");
        return (await File.ReadAllTextAsync(path, cancellationToken)).TrimEnd('\r', '\n');
    }
}
