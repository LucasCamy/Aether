# ADR-0011 — Segredos, PII e ciclo de vida

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Mensagens, destinatários e callbacks contêm dados pessoais; SDKs podem expor tokens
em URL e respostas de erro.

## Decisão

Secret Manager por tenant/ambiente/configuração; banco guarda referências.
Workload identity e privilégio mínimo. TLS validado em serviços externos.
Logs/traces não capturam corpo, variáveis, destinatários, headers secretos ou URLs
com credenciais. HMAC de destino é pseudônimo, não anonimização.

Eliminar dados por finalidade e retenção, inclusive caches, arquivos e exports.
Backups têm prazo e registro de eliminações reaplicado antes de restaurar produção.
Arquivamento precisa permitir descarte individual; uma chave por mês não basta.

## Alternativas consideradas

Criptografia em disco protege mídia, não leitura autorizada. Hash simples permite
enumeração. Imutabilidade histórica absoluta conflita com descarte de PII.

## Consequências

Tenant/controlador define instruções e fundamento; não afirmar conformidade apenas
por usar criptografia. Supressão pseudonimizada tem finalidade própria. Considerar
suboperadores, região de dados e transferência internacional.

## Validação e revisão

Testar ausência de PII em telemetria, revogação/rotação e restauração com eliminações.
Prazos jurídicos e produto Secret Manager dependem da produção.
Referências: https://planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709compilado.htm
e https://www.gov.br/anpd/pt-br/assuntos/incidente-de-seguranca
