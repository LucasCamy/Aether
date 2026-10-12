# ADR-0001 — Monólito modular e fronteiras de módulos

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Aether compartilha tenant, templates, autorização e rastreabilidade entre seis canais.
Microsserviços iniciais aumentariam coordenação sem fronteiras ou equipes independentes comprovadas.

## Decisão

Adotar monólito modular com API e Worker em processos separados. Tenancy,
Notifications, Delivery, Templates e ProviderManagement possuem Domain, Application
e Infrastructure próprios. Usar Vertical Slices em Application. Hosts concentram
endpoints/consumers e composição; regras permanecem nos módulos.

Domain não referencia frameworks externos. Application depende do Domain do módulo.
Infrastructure implementa portas. Proibir acesso direto às tabelas de outro módulo.
Notifications possui Request/Dispatch/Attempt; Delivery orquestra por contratos
explícitos, sem duplicar ownership. Contratos públicos ficam em Aether.Contracts.

## Alternativas consideradas

Microsserviços favorecem autonomia, mas exigem ownership de dados, contratos e operação.
Um único projeto seria mais simples, porém dificultaria enforcement das fronteiras.

## Consequências

Há mais projetos vazios na fundação, mas dependências já podem ser verificadas.
Módulos não são serviços independentes. Extrair apenas com motivo mensurável.

## Validação e revisão

Verificar referências e ausência de ciclos. Revisitar quando equipe, SLO ou escala
exigir deployment e dados independentes de um módulo.
