---
type: concept
title: Clean Architecture (no TCMine)
tags: [concept, arquitetura, clean-architecture, dotnet]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [Clean Architecture, camadas, arquitetura limpa]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-application]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-server]]"
---

# Clean Architecture (no TCMine)

> O TCMine separa o core em **Domain → Application → Infrastructure**, com
> dependências apontando para dentro; o servidor é só a camada de entrega.

## O que é

Padrão de organização em camadas concêntricas: o **domínio** (regras e entidades)
não conhece nada de fora; a **aplicação** define portas (interfaces) e casos de
uso; a **infraestrutura** implementa as portas com frameworks concretos; e a
camada de entrega (web/desktop) só orquestra.

## Por que importa para o TCMine

Permite **compartilhar a lógica de domínio** entre servidor e launcher sem
acoplar a frameworks. Mapeamento concreto na solução:

- [[entities/tcmine-domain]] — entidades, enums e regras puras (sem EF/ASP.NET).
- [[entities/tcmine-application]] — portas (`ICurseForgeApi`, `IUserRepository`,
  `IPlayerConfigRepository`, `IServerSettingsStore`), DTOs e lógica pura
  (`CurseForgeImporter`, `ModSetMerge`).
- [[entities/tcmine-infrastructure]] — EF Core, CurseForge, filesystem, serviços.
- [[entities/tcmine-server]] — entrega (Blazor + Minimal API), só compõe as camadas.

## Detalhes / Variações

- O DI resolve a porta para a implementação concreta (ex.: `ICurseForgeApi` →
  `CurseForgeApiClient`; `AppDbContext` abstrato → subclasse por provider).
- Lógica que **os dois lados** precisam idêntica (filtro de `ModSide`, parse de
  loader, merge de mods) fica no core, não duplicada — ver [[concepts/modside-rules]].

## Aplicação concreta

- Onde está: estrutura de pastas/projetos da solução; `Program.cs` do servidor
  faz o wiring; `AddTcMineDatabase` resolve a infraestrutura de dados.

## Contradições / debates conhecidos

- (nenhum até agora)

## Referências

- [[sources/2026-06-22-leitura-codigo-vivo]]
