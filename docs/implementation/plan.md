# Plano de implementação

## M0 — Fundação (concluído)

Solução Aether.slnx, .NET 10, módulos, hosts mínimos, ADRs e documentação.
Validação: build Release e smoke de liveness/inicialização do Worker.

## M1 — Primeira slice: aceitação durável de E-mail

1. Introduzir contratos de SubmitNotification/GetNotification e erros Problem Details.
2. Implementar autenticação JWT por OIDC configurável; tenant autorizado obrigatório.
   Testes usam emissor/chaves de teste; produção não usa bypass.
3. Implementar domínio: request, dispatch, idempotência e transições mínimas.
4. Introduzir EF Core/Npgsql com versões compatíveis e migrations em Infrastructure.
5. Criar roles runtime/migrator, RLS, chaves compostas e transação de aceitação.
6. Implementar POST imediato e GET por ID. Somente E-mail neste incremento.
7. Validar entrada, template/conteúdo, expiração e limites; sem chamar provedor.
8. Persistir Request + Dispatch + Idempotência + Outbox no mesmo commit.

Aceite: concorrência da mesma chave cria um único recurso; rollback não deixa outbox;
tenant B não consulta nem relaciona dados de A; contexto não vaza no pooling; sem DB
não há 202; replay mantém IDs e corpo da aceitação. Testar em PostgreSQL real.

## M2 — Transporte e processamento controlado

Publisher com lease e SKIP LOCKED; RabbitMQ confirms/mandatory; topology declarada;
inbox/generation/fencing; consumidor com adapter de teste isolado de produção.
Testar crash antes/depois de publish, commit e ACK. Redis ainda não decide aceitação.
Sem cadastro real e autorização, nenhum e-mail externo é enviado.

## M3 — Primeiro provedor real

Escolher E-mail comercial, configurar Secret Manager, callbacks autenticados,
supressões, reconciliação, Token Bucket, métricas e circuitos.
Aceite: timeout ambíguo não duplica por fallback; hard bounce e opt-out impedem
novos envios; nenhum segredo/PII aparece em logs.

## M4 — Operação e expansão

Bulk atômico limitado, agendamentos, cancelamento, templates e próximos canais;
readiness real, runbooks exercitados e capacidade medida.
Adotar partições antes de crescimento que inviabilize a migração planejada.

## Pendências com defaults reversíveis

| Tema | Direção inicial | Antes de produção |
|---|---|---|
| Identidade | OIDC/JWT, sem fornecedor acoplado | issuer, audience, claims e autorização |
| Persistência | EF Core + Npgsql | pacote/patch e roles validados |
| Broker/Redis | RabbitMQ quorum / Redis | versões, licença, sizing e HA |
| Provedor de E-mail | Adapter e sandbox | produto, região, remetente e conta |
| SLO/volume | Medir slices | throughput, bursts, tamanho e recuperação |
| Infra local | Compose após M1 | imagens fixadas e segredos locais |
| Produção | Hosts independentes | plataforma, rede, backups e secret store |
| Retenção/LGPD | Política por finalidade | instruções do controlador e prazos |

Não instalar dependências futuras só para preencher a estrutura.
Cada milestone entrega comportamento verificável e atualiza o README.
