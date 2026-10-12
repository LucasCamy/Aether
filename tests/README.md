# Testes

Execute scripts/Initialize-Local.ps1 uma vez e scripts/Test.ps1.
A suíte exige PostgreSQL/RabbitMQ/Redis locais e não ignora integrações silenciosamente.
Scripts fornecem variáveis; dados e credenciais são sintéticos/locais.

- Domain: estados, Unknown, leases, retry e JSON canônico.
- API: JWT, autorização, replay concorrente, conflito e readiness.
- PostgreSQL: RLS/pooling, rollback e roles.
- RabbitMQ: confirms/mandatory, reentrega/ACK e pipeline controlado.
- Redis: Token Bucket concorrente e atomicidade.
- Provider: fixtures HTTP Resend, sem envios externos.
- Governança: circuito local e calendário.

O pipeline pode consumir o backlog local controlado. Não usar produção.
