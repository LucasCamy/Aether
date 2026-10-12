# Aether

API de comunicação multicanal, centralizada e multi-tenant em .NET 10.
Destino arquitetural: E-mail, WhatsApp, SMS, Telegram, Push e Webhooks.

## Estado atual

Implementada a primeira slice de **E-mail imediato**: JWT/OIDC, tenant autorizado,
RLS PostgreSQL, idempotência concorrente, commit de Request/Dispatch/Outbox e GET
isolado. Publisher RabbitMQ com confirms/mandatory e consumo controlado com inbox,
tentativas, geração, leases e retry atômico.

Adapter Resend, Token Bucket Redis, circuito local, calendário e store de segredos
montados estão implementados/testados, mas não conectados ao envio de produção.
O consumer atual é sintético, permitido apenas em Development.
Não há envio externo, callbacks, bulk, agenda, templates ou outros canais habilitados.

Os [16 ADRs](docs/adr/README.md) estão registrados.
A [matriz de implementação](docs/implementation/status.md) distingue decisões,
componentes e pendências. A aplicação ainda não está pronta para produção.

## Requisitos e testes

SDK .NET 10.0.401, Docker Linux com Compose e PowerShell 7.
Dependências fixadas com lock files; NuGet.Config usa apenas nuget.org.

```powershell
dotnet tool restore
dotnet restore Aether.slnx --locked-mode
dotnet build Aether.slnx -c Release --no-restore
./scripts/Initialize-Local.ps1
./scripts/Test.ps1
```

Initialize-Local gera credenciais aleatórias em .env e não sobrescreve arquivo existente.
Test usa PostgreSQL, RabbitMQ e Redis reais e dados sintéticos. Não usar produção.
Compose tem um nó por serviço, portas em loopback e nenhuma garantia de HA.

## Executar localmente

```powershell
./scripts/Run-Api.ps1
```

O script aplica migrations com role administrativa, provisiona tenant local e inicia
a API com role restrita. JWT local tem assinatura RS256, issuer, audience, expiração
e claims verificadas. Chave privada fica em memória; token de uma hora fica em
.local/access-token. Não compartilhar. Produção exige Auth:Authority HTTPS/OIDC.

Em outro terminal:

```powershell
./scripts/Submit-Sample.ps1
./scripts/Run-Worker.ps1 -ControlledConsumer
```

O exemplo usa synthetic@example.com. O Worker controlado **não envia E-mail**:
registra aceitação sintética. Não usar esse modo para mensagens reais.
Run-Worker sem a flag só publica a outbox. Encerrar os hosts com Ctrl+C.

## Rotas implementadas

| Rota | Comportamento |
|---|---|
| GET /health/live | Vida do processo |
| GET /health/ready | Conexão, catálogo e role PostgreSQL segura |
| POST /v1/notifications | Aceitação durável de um E-mail explícito |
| GET /v1/notifications/{id} | Status isolado por tenant |
| GET /v1/notifications/{id}/dispatches | Disparos da solicitação |

Headers: Authorization, X-Tenant-ID, X-Correlation-ID e X-Idempotency-Key no POST.
Claims: tenant_id e permission notifications:send/read; critical também exige
notifications:critical. Erros de domínio usam Problem Details; autenticação/binding
possuem respostas padrão ASP.NET Core.

## Organização

- src/Hosts: composição, endpoints, consumers e jobs.
- src/Modules: Tenancy, Notifications, Delivery, Templates, ProviderManagement.
- Domain → Application → Infrastructure; dependências para dentro.
- src/Contracts: contratos públicos e de integração.
- infra: serviços locais; segredos fora do Git.
- docs/adr: decisões e referências.
- docs/implementation: milestones e estado verificável.
- docs/operations: runbook inicial.
- tests/Aether.Tests: domínio, segurança e integrações.

## E-mail de baixo custo

Resend priorizado para menos de 500/mês. A franquia gratuita atende o cenário sujeito
a limites diários/domínios. Não cobre hospedagem ou crescimento.
[Decisão e referências](docs/adr/0015-provedor-email-inicial.md).

Faltam governança/consentimento conectados, configuração versionada e callbacks
antes de envio externo; depois bulk, agenda, templates e outros canais.
[Plano](docs/implementation/plan.md), [estado](docs/implementation/status.md),
[runbook](docs/operations/runbook.md).
