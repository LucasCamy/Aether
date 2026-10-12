# ADR-0003 — Isolamento multi-tenant e identidade

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Header de tenant e UUID não constituem autorização. Pooling e Workers podem reutilizar
conexões e processar tenants diferentes.

## Decisão

Identidade autenticada por OIDC/JWT valida issuer, audience, assinatura e autorização
de tenant. X-Tenant-ID seleciona somente um tenant autorizado. Nenhum endpoint de
negócio opera sem identidade; callbacks usam autenticação própria.

PostgreSQL usa tenant_id em chaves e FKs, RLS e FORCE RLS; ausência de contexto nega.
Contexto é local à transação. Role runtime não é owner/superuser/BYPASSRLS.
Migrator e tarefas globais têm credenciais e escopos separados. Não conceder acesso
direto a partições que contorne as políticas da tabela lógica.

## Alternativas consideradas

Banco por tenant oferece isolamento maior, com custo operacional. Filtros apenas
na aplicação não protegem consultas esquecidas.

## Consequências

RLS é defesa adicional, não protege contra credencial privilegiada comprometida
ou contexto adulterado pela própria aplicação. Roles e caminhos globais exigem
revisão explícita; não compartilhar credencial administrativa com a API.

## Validação e revisão

Testar tenant A/B, FKs cruzadas, ausência de contexto e reutilização de conexão
em PostgreSQL real. Provedor OIDC e claims finais permanecem configuráveis.
Referência: https://www.postgresql.org/docs/current/ddl-rowsecurity.html
