using System.Security.Cryptography;
using System.Text.Json;
using Aether.Notifications.Application;
using Aether.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aether.Notifications.Infrastructure.Persistence;

public sealed class PostgresNotificationStore(
    IDbContextFactory<NotificationDbContext> factory, TimeProvider clock) : INotificationStore
{
    public async Task<Acceptance> SubmitAsync(Guid tenantId, Guid correlationId, string key,
        NotificationDraft draft, byte[] hash, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SetTenant(db, tenantId, cancellationToken);
        var lockKey = tenantId.ToString("D") + ":" + key;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        var previous = await db.Idempotency.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Key == key, cancellationToken);
        if (previous != null)
        {
            if (!CryptographicOperations.FixedTimeEquals(previous.Hash, hash))
                throw new NotificationFault("idempotency_conflict", 409);
            // Retaining a key is deliberately safer than silently permitting duplicate creation.
            return new Acceptance(previous.NotificationId, previous.AcceptedAt,
                previous.ExpiresAt, previous.DispatchId, true);
        }
        if (!await db.Database.SqlQuery<bool>(
            $"SELECT tenancy.is_active({tenantId}) AS \"Value\"").SingleAsync(cancellationToken))
            throw new NotificationFault("tenant_not_active", 403);
        var now = clock.GetUtcNow();
        if (draft.ExpiresAt <= now || draft.ExpiresAt > now.AddDays(30))
            throw new NotificationFault("invalid_expiration", 422);
        var requestId = Guid.CreateVersion7();
        var dispatchId = Guid.CreateVersion7();
        var outboxId = Guid.CreateVersion7();
        var expires = draft.ExpiresAt.ToUniversalTime();
        var request = new NotificationRequest(tenantId, requestId, correlationId, now,
            expires, draft.Priority, draft.ClientReference, draft.Metadata);
        var dispatch = new Dispatch(tenantId, dispatchId, requestId, now, expires,
            draft.Email, JsonSerializer.Serialize(new { subject = draft.Subject, text = draft.Text, html = draft.Html }));
        db.Requests.Add(request);
        db.Registry.Add(new DispatchRegistryRecord { TenantId = tenantId, Id = dispatchId,
            RequestId = requestId, CreatedMonth = dispatch.CreatedMonth });
        db.Dispatches.Add(dispatch);
        var keyExpires = now.AddDays(7) > expires.AddDays(7) ? now.AddDays(7) : expires.AddDays(7);
        db.Idempotency.Add(new IdempotencyRecord { TenantId = tenantId, Key = key, Hash = hash,
            NotificationId = requestId, DispatchId = dispatchId, AcceptedAt = now, ExpiresAt = keyExpires });
        db.Outbox.Add(new OutboxRecord { TenantId = tenantId, Id = outboxId, DispatchId = dispatchId,
            CreatedAt = now, AvailableAt = now, RoutingKey = "email." + draft.Priority + ".shared-00",
            Payload = JsonSerializer.Serialize(new { schemaVersion = 1, messageId = outboxId,
                tenantId, dispatchId, notificationId = requestId, correlationId, generation = 0 }) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new Acceptance(requestId, now, keyExpires, dispatchId, false);
    }

    public async Task<NotificationView?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await SetTenant(db, tenantId, cancellationToken);
        var request = await db.Requests.AsNoTracking().SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Id == id, cancellationToken);
        if (request == null) return null;
        var dispatches = await db.Dispatches.AsNoTracking().Where(
            x => x.TenantId == tenantId && x.RequestId == id).ToArrayAsync(cancellationToken);
        return new NotificationView(request, dispatches);
    }

    public static async Task SetTenant(NotificationDbContext db, Guid tenantId, CancellationToken cancellationToken)
    {
        var safe = await db.Database.SqlQueryRaw<bool>("""
            SELECT NOT r.rolsuper AND NOT r.rolbypassrls AND NOT EXISTS (
                SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'notifications' AND pg_has_role(current_user, c.relowner, 'MEMBER')
            ) AS "Value" FROM pg_roles r WHERE r.rolname = current_user
            """).SingleAsync(cancellationToken);
        if (!safe) throw new InvalidOperationException("Role de runtime não pode possuir/bypassar tabelas.");
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('aether.tenant_id', {tenantId.ToString("D")}, true)", cancellationToken);
    }
}
