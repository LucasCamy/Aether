# Aether — instruções de implementação

- Ler docs/adr/README.md e o ADR específico antes de alterar uma decisão.
- Comunicação e documentação em português; nomes técnicos em inglês.
- .NET 10; compilar com dotnet build Aether.slnx -c Release.
- Domínio não referencia infraestrutura, SDKs, hosts ou contratos de transporte.
- Application depende apenas do domínio do módulo; portas ficam no consumidor.
- Infrastructure implementa portas. Hosts compõem módulos e concentram apresentação.
- Evitar dependências diretas entre módulos; usar contratos explícitos.
- Nenhum endpoint de negócio deve operar sem autenticação e tenant autorizado.
- tenant_id precisa acompanhar consultas e FKs. RLS usa contexto local à transação.
- PostgreSQL decide idempotência; Redis nunca é a única garantia.
- Estado de negócio e intenção de publicação devem ser persistidos na mesma transação.
- Resultado externo desconhecido exige reconciliação; não habilitar fallback cego.
- Não registrar payloads, PII ou segredos em logs. Não versionar credenciais.
- Distinguir documentação aceita, planejada e implementada. Não simular 202 durável.
- Testar invariantes, concorrência, isolamento e janelas de falha quando implementados.
- SDKs e dependências externas só entram quando necessários, com versão explícita.
