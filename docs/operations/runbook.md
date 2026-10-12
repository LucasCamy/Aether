# Runbook inicial

Estes procedimentos são direcionados à implementação atual. Não equivalem a um
exercício completo de recuperação de produção.

## Broker indisponível

Publisher mantém outbox não confirmada e tenta com backoff. Verificar conexão, quorum
e alarmes de disco/memória. Não marcar PublishedAt manualmente nem apagar filas.
Após recuperar, acompanhar idade da outbox e quotas antes de drenar.
A ingestão atual ainda não tem orçamento de backlog: bloquear admissão no ingresso
se a indisponibilidade persistir, até conectar esse controle na aplicação.

## Redis indisponível

Idempotência continua PostgreSQL. Token Bucket retorna falha, sem liberação permissiva.
O limiter ainda não está conectado ao consumer; não habilitar envio real sem isso.
Não substituir quota global por limite local multiplicado pelo número de Workers.

## Tentativa abandonada

Reentrega aguarda lease válida; após expiração, resultado passa a Unknown.
Não alterar para Pending nem reenviar por outro provedor sem evidência/reconciliação.

## Credenciais e callback

Não colar token em issues, logs ou commits. Segredos montados têm nomes permitidos,
sem travessia de diretórios. Adapter Resend usa endpoint HTTPS fixo.
Callbacks ainda não são recebidos: não anunciar entrega confirmada.

## Ambiente local

Parar containers mantendo dados:
```powershell
docker compose --env-file .env -f infra/compose.yaml stop
```

.env e .local são locais e ignorados pelo Git. O Compose usa um nó por serviço,
portas em loopback e não representa topologia de produção.
