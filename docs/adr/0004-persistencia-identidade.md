# ADR-0004 — PostgreSQL, UUIDv7 e preparação para particionamento

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Dados operacionais precisam de transações e integridade. Histórico cresce, mas volume
não foi especificado. Unicidade em tabelas particionadas depende da chave de partição.

## Decisão

Usar PostgreSQL e UUIDv7 para identidades públicas. Adotar registro compacto
dispatch_registry por (tenant_id, dispatch_id), com bucket mensal UTC imutável.
Detalhe e tentativas podem usar PKs que incluam o bucket; referências duradouras
apontam para registry. Criar registry e detalhe na mesma transação.

Particionamento RANGE mensal é capacidade-alvo; ativação depende de volume e plano
de migração, não é necessária para M1. JSONB fica para conteúdo/capacidades dinâmicas
com schemas; identidade, estado, datas e relações permanecem relacionais.
GIN só para consultas justificadas. Credenciais ficam fora de JSONB.

## Alternativas consideradas

BIGINT é menor, mas exige coordenação. NoSQL pode ser projeção futura para histórico;
não substitui a transação de aceitação. Particionar tudo inicialmente adiciona custo.

## Consequências

Registry permite consulta por ID e arquivo, mas sua existência não garante sozinha
um detalhe: transação e reconciliação completam a regra. Sem volume real não há sizing.
Imutabilidade funcional não impede descarte privilegiado de PII.

## Validação e revisão

Testar unicidade, rollback e FKs antes de particionar. Planejar exportação verificada,
desanexação e retenção por tenant. Revisitar quando histórico afetar operação.
Referência: https://www.postgresql.org/docs/current/ddl-partitioning.html
