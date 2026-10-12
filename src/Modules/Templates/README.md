# Templates

Fronteira conforme docs/architecture/overview.md e ADR-0001.

- Domain: entidades, invariantes e transições sem dependências externas.
- Application: slices, handlers e portas necessárias aos casos de uso.
- Infrastructure: persistência e implementações de portas.
- Endpoints/consumers: apresentação nos hosts, delegando para handlers.

Projetos vazios nesta fundação; adicionar classes apenas ao implementar a slice.
