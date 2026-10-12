# Aether

API de comunicação multicanal, centralizada e multi-tenant em .NET:
E-mail, WhatsApp, SMS, Telegram, Push e Webhooks.

## Estado atual

Fundação inicial implementada: solução .NET 10, hosts API/Worker e fronteiras de
módulos compiláveis. As decisões das três partes da especificação estão consolidadas
nos [ADRs](docs/adr/README.md).

Ainda não há ingestão de notificações, autenticação, persistência, outbox, consumers,
adapters externos, migrations ou infraestrutura local. O Worker inicia o Generic Host
sem jobs. Nenhuma solicitação de comunicação é aceita ou enviada nesta etapa.

## Executar

Requisito: SDK 10.0.401 ou patch compatível na mesma feature band, conforme global.json.
A fundação usa apenas o runtime compartilhado; não exige PostgreSQL, RabbitMQ ou Redis.

```powershell
dotnet restore Aether.slnx
dotnet build Aether.slnx -c Release --no-restore
dotnet run --project src/Hosts/Aether.Api --urls http://127.0.0.1:5080
```

Em outro terminal:

```powershell
Invoke-RestMethod http://127.0.0.1:5080/health/live
dotnet run --project src/Hosts/Aether.Worker
```

HTTP em loopback é apenas o bootstrap local sem dados de negócio. Produção exige TLS.
A rota /health/live indica apenas que o processo responde: não afirma readiness de
banco, broker, autenticação ou capacidade de envio. Encerrar os hosts com Ctrl+C.

## Organização

- src/Hosts: composição, endpoints e consumers; handlers delegam para Application.
- src/Modules: Tenancy, Notifications, Delivery, Templates e ProviderManagement.
- Cada módulo: Domain → Application → Infrastructure, com dependências para dentro.
- src/Contracts: contratos públicos e de integração.
- docs/architecture: visão consolidada.
- docs/adr: decisões, alternativas e critérios de validação.
- docs/implementation: sequência de execução e pendências.
- tests: estratégia; projetos de testes entram junto aos comportamentos.

## Próxima entrega

[Milestone M1](docs/implementation/plan.md): uma slice real de ingestão de E-mail,
com tenant autenticado, idempotência PostgreSQL, Request/Dispatch/Outbox atômicos
e consulta isolada. Só então retornar 202 Accepted.

As escolhas de provedor comercial, identidade, SLO e infraestrutura de produção
continuam registradas como pendências; não bloqueiam a criação dos módulos.
