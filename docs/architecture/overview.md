# Visão arquitetural do Aether

Data: 2026-10-12. Síntese implementável das Partes 1, 2 e 3 da conversa;
não é transcrição integral. ADRs prevalecem quando refinam escolhas anteriores.

## Containers

```text
Cliente → API ASP.NET Core → PostgreSQL [Request + Dispatch + Outbox]
                                ↑                     |
                                |                     v
Provedores ← Delivery Worker ← RabbitMQ ← Outbox Publisher
    |                           |
    └→ Callback API → Receipt → Normalização → Estado + Outbox

Redis: cache e rate limiting; nunca fonte exclusiva de aceitação.
Secret Manager: credenciais por tenant/configuração, fora do payload.
```

## Ownership

| Módulo | Responsabilidade |
|---|---|
| Tenancy | Tenant autorizado, habilitações e políticas |
| Notifications | Intenção, lote, Dispatch, tentativas, estados e consultas |
| Delivery | Orquestração de execução, seleção, retry e reconciliação |
| Templates | Templates versionados e renderização validada |
| ProviderManagement | Configurações/revisões, capacidades e adapters |

Notifications possui os dados de Dispatch; Delivery solicita mudanças por portas
explícitas, sem acessar tabelas de outro módulo. Criar a porta/contrato de colaboração
na slice concreta. A ingestão compõe validações por portas; não carrega SDKs.

## Garantias

- 202 somente após commit de intenção durável.
- Sem envio exatamente uma vez garantido para serviços externos.
- tenant_id em chaves/referências e RLS com contexto local à transação.
- Outbox publicada apenas após confirm e ausência de retorno sem rota.
- Duplicata pode ocorrer entre confirm e atualização: consumo deve ser idempotente.
- Expiração limita novas tentativas; não desfaz aceitação externa.
- Finalidade, prioridade, execução, entrega e engajamento são dimensões diferentes.

## Refinamentos importantes

1. Particionamento está decidido como capacidade-alvo; sua ativação é posterior
   a medir volume. Registry é previsto antes de criar FKs incompatíveis.
2. Preservar identidade não impede descarte privilegiado de PII.
3. TTL/DLX não funciona como scheduler preciso; PostgreSQL guarda NextAttemptAt.
4. Provedor secundário não é elegível só por estar cadastrado.
5. Os números de retry/retention do chat são defaults propostos, não SLOs contratados.

## Situação implementada

Somente composição vazia dos hosts e projetos. Os mecanismos acima são requisitos
de implementação, não garantias oferecidas pelo bootstrap.
