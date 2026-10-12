using Aether.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace Aether.Notifications.Infrastructure.Persistence;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<TenantRecord> Tenants => Set<TenantRecord>();
    public DbSet<NotificationRequest> Requests => Set<NotificationRequest>();
    public DbSet<DispatchRegistryRecord> Registry => Set<DispatchRegistryRecord>();
    public DbSet<Dispatch> Dispatches => Set<Dispatch>();
    public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>();
    public DbSet<OutboxRecord> Outbox => Set<OutboxRecord>();
    public DbSet<InboxRecord> Inbox => Set<InboxRecord>();
    public DbSet<AttemptRecord> Attempts => Set<AttemptRecord>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("notifications");
        model.Entity<TenantRecord>(b => {
            // Reference-only mapping for a catalog owned by Tenancy.
            b.ToTable("tenants", "tenancy", table => table.ExcludeFromMigrations()); b.HasKey(x => x.Id);
            b.Property(x => x.Status).HasMaxLength(20);
        });
        model.Entity<NotificationRequest>(b => {
            b.ToTable("notification_requests"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasOne<TenantRecord>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Priority).HasMaxLength(20);
            b.Property(x => x.ClientReference).HasMaxLength(200);
            b.Property(x => x.Metadata).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.AcceptedAt, x.Id });
        });
        model.Entity<DispatchRegistryRecord>(b => {
            b.ToTable("dispatch_registry"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasAlternateKey(x => new { x.TenantId, x.Id, x.CreatedMonth });
            b.HasOne<NotificationRequest>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.RequestId }).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.TenantId, x.RequestId });
        });
        model.Entity<Dispatch>(b => {
            b.ToTable("dispatches"); b.HasKey(x => new { x.TenantId, x.Id, x.CreatedMonth });
            b.HasOne<DispatchRegistryRecord>().WithMany().HasForeignKey(x => new { x.TenantId, x.Id, x.CreatedMonth })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.CreatedMonth }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.State).HasConversion<string>().HasMaxLength(30);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.Property(x => x.Content).HasColumnType("jsonb");
            b.HasIndex(x => new { x.TenantId, x.RequestId });
        });
        model.Entity<IdempotencyRecord>(b => {
            b.ToTable("api_idempotency"); b.HasKey(x => new { x.TenantId, x.Key });
            b.Property(x => x.Key).HasMaxLength(128);
            b.HasOne<NotificationRequest>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.NotificationId }).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<OutboxRecord>(b => {
            b.ToTable("outbox_messages"); b.HasKey(x => new { x.TenantId, x.Id });
            b.HasOne<DispatchRegistryRecord>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.DispatchId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Payload).HasColumnType("jsonb");
            b.HasIndex(x => new { x.AvailableAt, x.CreatedAt }).HasFilter("published_at IS NULL");
        });
        model.Entity<InboxRecord>(b => {
            b.ToTable("consumer_inbox"); b.HasKey(x => new { x.TenantId, x.MessageId });
        });
        model.Entity<AttemptRecord>(b => {
            b.ToTable("dispatch_attempts"); b.HasKey(x => new { x.TenantId, x.Id, x.CreatedMonth });
            b.HasIndex(x => new { x.TenantId, x.DispatchId, x.CreatedMonth, x.Number }).IsUnique();
            b.HasOne<DispatchRegistryRecord>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.DispatchId, x.CreatedMonth })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.CreatedMonth }).OnDelete(DeleteBehavior.Restrict);
        });
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(SnakeCase(property.Name));
    }

    private static string SnakeCase(string value) =>
        string.Concat(value.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
}
