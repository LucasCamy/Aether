using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Aether.Notifications.Infrastructure.Persistence;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(
            "Host=localhost;Database=aether;Username=aether_migrator").Options);
}
