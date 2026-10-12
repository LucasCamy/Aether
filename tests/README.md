# Estratégia de testes

A fundação ainda não tem comportamentos de negócio implementados nem suíte de testes.
Build e smoke de hosts são as verificações desta etapa.

- Domain: invariantes e transições, expiração, Unknown e política de fallback.
- Application: autorização, decisões e erros; portas externas substituídas.
- PostgreSQL real: transações, unicidade concorrente, RLS/pooling e leases.
- RabbitMQ real: confirms, unroutable, reentrega, DLX e crash windows.
- Contracts/API: status codes, validação, replay e isolamento entre tenants.
- Providers: fixtures autenticadas de callbacks e classificação de resultado.
- Architecture: impedir dependências de Domain/SDK e acesso cruzado a módulos.
- Resilience: Redis/broker indisponíveis e recuperação com backlog.

Não substituir testes de SQL/RLS por EF InMemory. Não enviar a destinatários reais
durante testes. Fixtures devem usar dados sintéticos.
