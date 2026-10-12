using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aether.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in new[] { "notification_requests", "dispatch_registry", "dispatches", "api_idempotency", "outbox_messages" })
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE notifications.{table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE notifications.{table} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON notifications.{table}
                    USING (tenant_id = NULLIF(current_setting('aether.tenant_id', true), '')::uuid)
                    WITH CHECK (tenant_id = NULLIF(current_setting('aether.tenant_id', true), '')::uuid);
                    GRANT SELECT, INSERT, UPDATE ON notifications.{table} TO aether_runtime;
                    """);
            }
            migrationBuilder.Sql("""
                ALTER TABLE notifications.tenants ENABLE ROW LEVEL SECURITY;
                ALTER TABLE notifications.tenants FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON notifications.tenants USING (id = NULLIF(current_setting('aether.tenant_id', true), '')::uuid);
                GRANT USAGE ON SCHEMA notifications TO aether_runtime;
                GRANT SELECT ON notifications.tenants TO aether_runtime;
                ALTER TABLE notifications.notification_requests ADD CONSTRAINT valid_request_expiration CHECK (expires_at > accepted_at);
                ALTER TABLE notifications.notification_requests ADD CONSTRAINT valid_priority CHECK (priority IN ('critical','transactional','operational','marketing'));
                ALTER TABLE notifications.dispatches ADD CONSTRAINT valid_dispatch_expiration CHECK (expires_at > created_at);
                ALTER TABLE notifications.dispatches ADD CONSTRAINT valid_generation CHECK (generation >= 0 AND attempt_count >= 0);
                ALTER TABLE notifications.dispatches ADD CONSTRAINT valid_lease CHECK ((lease_token IS NULL) = (lease_until IS NULL));
                ALTER TABLE notifications.api_idempotency ADD CONSTRAINT valid_hash CHECK (octet_length(hash) = 32);
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE notifications.api_idempotency DROP CONSTRAINT valid_hash;
                ALTER TABLE notifications.dispatches DROP CONSTRAINT valid_lease;
                ALTER TABLE notifications.dispatches DROP CONSTRAINT valid_generation;
                ALTER TABLE notifications.dispatches DROP CONSTRAINT valid_dispatch_expiration;
                ALTER TABLE notifications.notification_requests DROP CONSTRAINT valid_priority;
                ALTER TABLE notifications.notification_requests DROP CONSTRAINT valid_request_expiration;
                """);
            foreach (var table in new[] { "tenants", "notification_requests", "dispatch_registry", "dispatches", "api_idempotency", "outbox_messages" })
            {
                migrationBuilder.Sql($"""
                    DROP POLICY tenant_isolation ON notifications.{table};
                    ALTER TABLE notifications.{table} NO FORCE ROW LEVEL SECURITY;
                    ALTER TABLE notifications.{table} DISABLE ROW LEVEL SECURITY;
                    REVOKE ALL ON notifications.{table} FROM aether_runtime;
                    """);
            }

        }
    }
}
