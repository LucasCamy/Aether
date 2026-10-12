#!/bin/sh
set -eu
psql --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --set=ON_ERROR_STOP=1 <<'SQL'
\getenv runtime_password AETHER_DB_RUNTIME_PASSWORD
\getenv publisher_password AETHER_DB_PUBLISHER_PASSWORD
SELECT format('CREATE ROLE aether_runtime LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS PASSWORD %L', :'runtime_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'aether_runtime') \gexec
SELECT format('CREATE ROLE aether_publisher LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS PASSWORD %L', :'publisher_password')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'aether_publisher') \gexec
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SQL
