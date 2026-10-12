# Plano de implementação

## M0 — Fundação: concluído

Solução, hosts, módulos, ADRs e documentação.

## M1 — Aceitação durável de E-mail: implementado e validado

JWT/OIDC, autorização, RLS, idempotência concorrente, Request/Dispatch/Outbox atômicos
e GET. Apenas E-mail explícito, um destinatário, uma mensagem e fan_out.
Migrations e scripts locais sem credenciais versionadas.

## M2 — Transporte controlado: implementado no baseline local

Publisher com leases/SKIP LOCKED, credential separada, confirms/mandatory, quorum,
retry TTL/DLX, inbox, geração, fencing e tentativas antes da chamada.
Consumer sintético só em Development. Teste de ponta a ponta com serviços reais.
Expandir crash/recovery, QoS, réplicas e HA antes de produção.

## M3 — Resend e governança: em andamento

Adapter, Token Bucket Redis, circuito local, calendário e segredos montados testados.
Ainda não conectados ao envio externo. Falta configuração versionada,
consentimento/supressões persistidos, callbacks autenticados/duráveis, reconciliação,
governança integrada, circuitos compartilhados, Secret Manager/cripto e telemetria.
Conta, domínio e segredos externos não foram provisionados.

## M4 — Operação e expansão: planejado

Bulk, agendamentos/cancelamento, templates, outros canais, partições/arquivos,
retenção/eliminação, administração e SLO/capacidade medidos.

| Pendência | Direção | Para concluir |
|---|---|---|
| Identidade | OIDC HTTPS | issuer, audience e claims de produção |
| E-mail | Resend, menos de 500/mês | conta/domínio/segredos e callbacks |
| Broker/Redis | Quorum e Token Bucket | HA, TLS, sizing e patches |
| Governança | Finalidade separada de prioridade | evidências, preferências e políticas conectadas |
| LGPD | Minimização/segredos externos | retenção e infraestrutura |
| SLO | Não presumido | carga, bursts e recuperação |
| Produção | Hosts separados | plataforma, rede e backups |

[Estado por ADR](status.md). Aceite dos ADRs não significa conclusão das funcionalidades.
