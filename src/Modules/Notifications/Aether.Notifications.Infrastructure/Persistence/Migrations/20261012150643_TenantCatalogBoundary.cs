using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aether.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantCatalogBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE SCHEMA IF NOT EXISTS tenancy;
                ALTER TABLE notifications.tenants SET SCHEMA tenancy;
                GRANT USAGE ON SCHEMA tenancy TO aether_runtime;
                CREATE FUNCTION tenancy.is_active(p_tenant uuid) RETURNS boolean
                LANGUAGE sql STABLE SECURITY INVOKER SET search_path = pg_catalog, tenancy AS $$
                  SELECT EXISTS (SELECT 1 FROM tenancy.tenants WHERE id = p_tenant AND status = 'active');
                $$;
                REVOKE ALL ON FUNCTION tenancy.is_active(uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION tenancy.is_active(uuid) TO aether_runtime;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION tenancy.is_active(uuid);
                ALTER TABLE tenancy.tenants SET SCHEMA notifications;
                """);

        }
    }
}
