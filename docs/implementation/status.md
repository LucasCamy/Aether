# Estado da implementação

Data: 2026-10-12. Aceitar um ADR não significa implementar todos os seus cenários.

| ADR | Implementado e verificado | Ainda necessário |
|---|---|---|
| 0001 | Fronteiras de projetos, API e Worker separados | Slices completas dos demais módulos |
| 0002 | .NET 10, versões exatas, lock files, fontes NuGet explícitas | Imagens/patches de produção e política de atualização |
| 0003 | JWT RS256 local ou OIDC HTTPS, autorização e RLS, roles separadas | Provedor OIDC real e provisionamento administrativo |
| 0004 | PostgreSQL, UUIDv7, registry e bucket mensal | Partições físicas, arquivos e retenção |
| 0005 | Commit atômico, leases, publisher, inbox e fencing | CDC opcional e exercícios adicionais de falha |
| 0006 | Quorum, confirms/mandatory, ACK/NACK, retry TTL/DLX e poison | Lanes dedicadas, escala/QoS e HA de três nós |
| 0007 | POST imediato de E-mail e GET isolado, replay/conflito | Bulk, agendamento e cancelamento REST |
| 0008 | Estados, tentativa, Unknown e retry atômico | Reconciliação real, entrega e engajamento |
| 0009 | Strategy/ports, adapter Resend e classificação, circuito local | Circuito conectado, fallback e outros adapters |
| 0010 | Token Bucket real Redis e calendário/timezone testados | Conectar governança à execução, opt-out/consentimento persistidos |
| 0011 | Segredos fora do Git, store montado, erros sanitizados | Criptografia/retention, direitos e Secret Manager de produção |
| 0012 | Contratos especificados | Recepção autenticada e persistência de callbacks |
| 0013 | Liveness/readiness DB, contadores publisher, CI e runbook inicial | Exportação OpenTelemetry, alertas, demais métricas |
| 0014 | M0/M1 e pipeline controlado M2 com testes reais | M3/M4 completas |
| 0015 | Resend priorizado para menos de 500/mês; adapter testado | Conta/domínio/segredo e callback reais |
| 0016 | Catálogo tenancy e funções restritas de publisher | Administração completa do tenant e produção |

## Escopo seguro atual

Ingestão aceita somente E-mail explícito, um destinatário e fan_out de um canal.
Não há envio externo habilitado. O consumer controlado funciona somente em Development.
O adapter Resend e os componentes de governança existem, mas não estão conectados
ao consumer de produção. Não habilitar envio real até concluir os controles M3.

A inbox atual tem uma única responsabilidade (email-dispatch); ao adicionar outros
consumers, introduzir consumer_name na chave. Topologia e concorrência são baseline
local; sem HA ou garantia de isolamento de infraestrutura.

A função tenancy.is_active é o contrato de admissão. O mapping de catálogo em
Notifications é referencial; não usá-lo para novos casos de administração.

Os testes usam dados sintéticos e podem consumir o backlog local controlado.
Não apontar scripts de teste para banco ou broker de produção.
