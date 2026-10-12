using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aether.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExecutionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "consumer_inbox",
                schema: "notifications",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_consumer_inbox", x => new { x.tenant_id, x.message_id });
                });

            migrationBuilder.CreateTable(
                name: "dispatch_attempts",
                schema: "notifications",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_month = table.Column<DateOnly>(type: "date", nullable: false),
                    dispatch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "text", nullable: false),
                    provider_message_id = table.Column<string>(type: "text", nullable: true),
                    error_code = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_attempts", x => new { x.tenant_id, x.id, x.created_month });
                    table.ForeignKey(
                        name: "FK_dispatch_attempts_dispatch_registry_tenant_id_dispatch_id_c~",
                        columns: x => new { x.tenant_id, x.dispatch_id, x.created_month },
                        principalSchema: "notifications",
                        principalTable: "dispatch_registry",
                        principalColumns: new[] { "tenant_id", "id", "created_month" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_attempts_tenant_id_dispatch_id_created_month_number",
                schema: "notifications",
                table: "dispatch_attempts",
                columns: new[] { "tenant_id", "dispatch_id", "created_month", "number" },
                unique: true);
            foreach (var table in new[] { "consumer_inbox", "dispatch_attempts" })
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "consumer_inbox",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "dispatch_attempts",
                schema: "notifications");
        }
    }
}
