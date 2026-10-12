# ADR-0010 — Governança de envio e limitação distribuída

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Prioridade não autoriza contato. Rate limiting por processo multiplica limite
quando há várias réplicas.

## Decisão

Finalidade e prioridade são independentes. Verificar bloqueio, destino, fundamento,
preferência, janela/validade e capacidade na ingestão e antes do envio.
Guardar evidência e versão; revogação invalida cache. Sem prova atual, adiar.

Redis usa Token Bucket atômico por escopos relevantes e semáforo separado para
concorrência. Considerar slots do Cluster: aquisição conjunta exige mesmo slot;
aquisição sequencial conservadora pode desperdiçar tokens, nunca reembolsar cegamente.
Orçamento financeiro/limite rígido exige ledger ou quota conservadora adicional.

## Alternativas consideradas

Sliding Window serve para outras semânticas, com custo de memória. Permitir tudo
na queda Redis perde controle. Cache negativo sem frescor ignora opt-out recente.

## Consequências

Timezone IANA e calendário por tenant/finalidade. NextAttemptAt guarda adiamentos.
Critical não contorna opt-out/regras externas. Reservar capacidade sem ultrapassar
quota do provedor; marketing tem justiça, burst e backlog limitados.

## Validação e revisão

Testar corrida opt-out/envio, mudança de horário, calendário, recuperação Redis
sem saldo e limite agregado entre réplicas. TTL e números são configurações versionadas.
