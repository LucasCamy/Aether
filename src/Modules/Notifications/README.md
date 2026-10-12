# Notifications

Request, Dispatch, estados, aceitação/idempotência, outbox/inbox e execução com PostgreSQL. HTTP/consumers ficam nos hosts. Catálogo de tenant é referencial: chamar tenancy.is_active, sem ampliar ownership.

Domain: invariantes. Application: slices/portas. Infrastructure: persistência/adapters.

Ver docs/implementation/status.md para o escopo verificável.
