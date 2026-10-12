# Validação

Data: 2026-10-12. Windows, SDK .NET 10.0.401 e Docker Linux.

- Build Release sem avisos/erros.
- 32 testes, nenhum ignorado, com PostgreSQL/RabbitMQ/Redis reais.
- Oito chamadas concorrentes criam um Request/Dispatch/Outbox.
- JWT ausente/adulterado, tenant diferente e permissão critical.
- RLS/pooling, runtime sem superuser/BYPASSRLS.
- FK composta impede vínculo entre tenants; banco indisponível nunca produz 202.
- Falha de outbox reverte toda aceitação.
- Publisher sem acesso direto à tabela; leases/fencing.
- Confirms/mandatory, reentrega e ACK manual.
- Pipeline completo em modo controlado.
- Tentativa abandonada vira Unknown; geração antiga não reenvia.
- Resend com endpoint fixo, idempotência e timeout conservador.
- Token Bucket concorrente e negação multi-bucket atômica.
- Circuito local e calendário America/Cuiaba.
- Scripts PowerShell parseados e smoke API/Worker/exemplo em loopback.
- EF sem alterações pendentes de modelo; NuGet não reportou vulnerabilidades
  conhecidas nas dependências diretas/transitivas nas fontes consultadas.

O smoke identificou readiness no schema antigo após mover o catálogo. A rota foi
corrigida e ganhou teste de regressão.

Sem envio externo, callbacks reais, HA, carga ou recuperação de produção.
Pendências em status.md. A primeira CI Linux encontrou teste dependente de backlog;
ele foi corrigido para criar seus próprios dados. A execução seguinte será verificada.
