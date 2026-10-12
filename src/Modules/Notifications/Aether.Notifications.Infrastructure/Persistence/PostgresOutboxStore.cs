using Aether.Notifications.Application;
using Npgsql;

namespace Aether.Notifications.Infrastructure.Persistence;

public sealed class PostgresOutboxStore(NpgsqlDataSource source) : IOutboxStore
{
    public async Task<OutboxMessage[]> ClaimAsync(int limit, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid();
        await using var command = source.CreateCommand("SELECT * FROM notifications.claim_outbox($1, $2)");
        command.Parameters.AddWithValue(token);
        command.Parameters.AddWithValue(Math.Clamp(limit, 1, 5));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<OutboxMessage>();
        while (await reader.ReadAsync(cancellationToken))
            result.Add(new OutboxMessage(reader.GetGuid(0), reader.GetGuid(1), token,
                reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        return result.ToArray();
    }
    public async Task<bool> CompleteAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        await using var command = source.CreateCommand("SELECT notifications.complete_outbox($1, $2, $3)");
        command.Parameters.AddWithValue(message.TenantId);
        command.Parameters.AddWithValue(message.Id);
        command.Parameters.AddWithValue(message.LeaseToken);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
    public async Task ReleaseAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        await using var command = source.CreateCommand("SELECT notifications.release_outbox($1, $2, $3)");
        command.Parameters.AddWithValue(message.TenantId);
        command.Parameters.AddWithValue(message.Id);
        command.Parameters.AddWithValue(message.LeaseToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
