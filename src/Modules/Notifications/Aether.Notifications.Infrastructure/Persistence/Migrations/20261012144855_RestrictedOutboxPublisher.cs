using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aether.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RestrictedOutboxPublisher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'aether_outbox_operator') THEN
                  CREATE ROLE aether_outbox_operator NOLOGIN BYPASSRLS;
                END IF; END $$;
                GRANT USAGE ON SCHEMA notifications TO aether_outbox_operator, aether_publisher;
                GRANT SELECT, UPDATE ON notifications.outbox_messages TO aether_outbox_operator;
                
                CREATE FUNCTION notifications.claim_outbox(p_token uuid, p_limit integer)
                RETURNS TABLE(tenant_id uuid, id uuid, exchange text, routing_key text, payload text)
                LANGUAGE sql SECURITY DEFINER SET search_path = pg_catalog, notifications SET row_security = off AS $$
                  WITH candidates AS (
                    SELECT o.tenant_id, o.id FROM notifications.outbox_messages o
                    WHERE o.published_at IS NULL AND o.available_at <= clock_timestamp()
                      AND (o.lease_until IS NULL OR o.lease_until < clock_timestamp())
                    ORDER BY o.available_at, o.created_at, o.id
                    LIMIT LEAST(GREATEST(p_limit,1),5) FOR UPDATE SKIP LOCKED
                  )
                  UPDATE notifications.outbox_messages o
                  SET lease_token = p_token, lease_until = clock_timestamp() + interval '60 seconds',
                      attempts = o.attempts + 1
                  FROM candidates c WHERE o.tenant_id = c.tenant_id AND o.id = c.id
                  RETURNING o.tenant_id, o.id, o.exchange, o.routing_key, o.payload::text;
                $$;
                
                CREATE FUNCTION notifications.complete_outbox(p_tenant uuid, p_id uuid, p_token uuid)
                RETURNS boolean LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, notifications SET row_security = off AS $$
                DECLARE affected integer;
                BEGIN
                  UPDATE notifications.outbox_messages SET published_at = clock_timestamp(), lease_token = NULL, lease_until = NULL
                  WHERE tenant_id = p_tenant AND id = p_id AND lease_token = p_token
                    AND lease_until > clock_timestamp() AND published_at IS NULL;
                  GET DIAGNOSTICS affected = ROW_COUNT;
                  RETURN affected = 1;
                END $$;
                
                CREATE FUNCTION notifications.release_outbox(p_tenant uuid, p_id uuid, p_token uuid)
                RETURNS void LANGUAGE sql SECURITY DEFINER SET search_path = pg_catalog, notifications SET row_security = off AS $$
                  UPDATE notifications.outbox_messages
                  SET lease_token = NULL, lease_until = NULL,
                      available_at = clock_timestamp() + make_interval(secs => LEAST(300, power(2, LEAST(attempts,8)))::integer)
                  WHERE tenant_id = p_tenant AND id = p_id AND lease_token = p_token AND published_at IS NULL;
                $$;
                ALTER FUNCTION notifications.claim_outbox(uuid,integer) OWNER TO aether_outbox_operator;
                ALTER FUNCTION notifications.complete_outbox(uuid,uuid,uuid) OWNER TO aether_outbox_operator;
                ALTER FUNCTION notifications.release_outbox(uuid,uuid,uuid) OWNER TO aether_outbox_operator;
                REVOKE ALL ON FUNCTION notifications.claim_outbox(uuid,integer) FROM PUBLIC;
                REVOKE ALL ON FUNCTION notifications.complete_outbox(uuid,uuid,uuid) FROM PUBLIC;
                REVOKE ALL ON FUNCTION notifications.release_outbox(uuid,uuid,uuid) FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION notifications.claim_outbox(uuid,integer) TO aether_publisher;
                GRANT EXECUTE ON FUNCTION notifications.complete_outbox(uuid,uuid,uuid) TO aether_publisher;
                GRANT EXECUTE ON FUNCTION notifications.release_outbox(uuid,uuid,uuid) TO aether_publisher;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP FUNCTION notifications.claim_outbox(uuid,integer);
                DROP FUNCTION notifications.complete_outbox(uuid,uuid,uuid);
                DROP FUNCTION notifications.release_outbox(uuid,uuid,uuid);
                REVOKE SELECT, UPDATE ON notifications.outbox_messages FROM aether_outbox_operator;
                """);

        }
    }
}
