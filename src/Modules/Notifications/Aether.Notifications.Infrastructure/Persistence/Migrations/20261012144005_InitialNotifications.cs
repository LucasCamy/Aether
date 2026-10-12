using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aether.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_requests",
                schema: "notifications",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    client_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_requests", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "FK_notification_requests_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalSchema: "notifications",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "api_idempotency",
                schema: "notifications",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_idempotency", x => new { x.tenant_id, x.key });
                    table.ForeignKey(
                        name: "FK_api_idempotency_notification_requests_tenant_id_notificatio~",
                        columns: x => new { x.tenant_id, x.notification_id },
                        principalSchema: "notifications",
                        principalTable: "notification_requests",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_registry",
                schema: "notifications",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_month = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_registry", x => new { x.tenant_id, x.id });
                    table.UniqueConstraint("AK_dispatch_registry_tenant_id_id_created_month", x => new { x.tenant_id, x.id, x.created_month });
                    table.ForeignKey(
                        name: "FK_dispatch_registry_notification_requests_tenant_id_request_id",
                        columns: x => new { x.tenant_id, x.request_id },
                        principalSchema: "notifications",
                        principalTable: "notification_requests",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "dispatches",
                schema: "notifications",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_month = table.Column<DateOnly>(type: "date", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    channel = table.Column<string>(type: "text", nullable: false),
                    recipient = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    state = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    delivery_state = table.Column<string>(type: "text", nullable: false),
                    generation = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatches", x => new { x.tenant_id, x.id, x.created_month });
                    table.ForeignKey(
                        name: "FK_dispatches_dispatch_registry_tenant_id_id_created_month",
                        columns: x => new { x.tenant_id, x.id, x.created_month },
                        principalSchema: "notifications",
                        principalTable: "dispatch_registry",
                        principalColumns: new[] { "tenant_id", "id", "created_month" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "notifications",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dispatch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    routing_key = table.Column<string>(type: "text", nullable: false),
                    exchange = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    available_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "FK_outbox_messages_dispatch_registry_tenant_id_dispatch_id",
                        columns: x => new { x.tenant_id, x.dispatch_id },
                        principalSchema: "notifications",
                        principalTable: "dispatch_registry",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_idempotency_tenant_id_notification_id",
                schema: "notifications",
                table: "api_idempotency",
                columns: new[] { "tenant_id", "notification_id" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_registry_tenant_id_request_id",
                schema: "notifications",
                table: "dispatch_registry",
                columns: new[] { "tenant_id", "request_id" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatches_tenant_id_request_id",
                schema: "notifications",
                table: "dispatches",
                columns: new[] { "tenant_id", "request_id" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_requests_tenant_id_accepted_at_id",
                schema: "notifications",
                table: "notification_requests",
                columns: new[] { "tenant_id", "accepted_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_available_at_created_at",
                schema: "notifications",
                table: "outbox_messages",
                columns: new[] { "available_at", "created_at" },
                filter: "published_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_tenant_id_dispatch_id",
                schema: "notifications",
                table: "outbox_messages",
                columns: new[] { "tenant_id", "dispatch_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_idempotency",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "dispatches",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "dispatch_registry",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_requests",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "notifications");
        }
    }
}
