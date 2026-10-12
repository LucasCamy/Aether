# ADR-0002 — .NET 10 e política de dependências

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

O workspace dispõe do SDK 10.0.401 e runtimes .NET/ASP.NET Core 10.0.12.
A aplicação precisa de runtime suportado e builds reproduzíveis.

## Decisão

Usar net10.0, nullable, implicit usings, warnings como erros e build determinístico.
Fixar SDK 10.0.401 com latestPatch na feature band e sem prerelease.
Aether.Api usa ASP.NET Core; Aether.Worker usa Generic Host. Nesta fundação,
o Worker utiliza o shared framework ASP.NET Core para evitar pacotes antecipados;
quando a hospedagem for empacotada, avaliar referência Hosting explícita.

Direção para M1: EF Core e Npgsql; para M2: cliente RabbitMQ direto, com protocolo
de outbox explícito. Introduzir versões exatas compatíveis na slice e lock files.
SDKs de provedores entram só nos adapters necessários.

## Alternativas consideradas

Versões anteriores têm menor horizonte de suporte. Framework completo de mensageria
pode reduzir implementação, mas exige reavaliar outbox/ACK e licenciamento.
Não misturar duas implementações de outbox.

## Consequências

A fundação compila sem pacotes NuGet externos. O Worker requer o runtime ASP.NET Core
enquanto usar o framework compartilhado. Atualizações de SDK passam por build/smoke.

## Validação e revisão

Validar Release em SDK fixado. Revisitar framework de mensageria antes de M2 se
a complexidade operacional justificar, por novo ADR que substitua a decisão.
Referência: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core
