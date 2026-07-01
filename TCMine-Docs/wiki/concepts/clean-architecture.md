---
type: concept
title: Clean Architecture
tags: [concept, arquitetura, clean-architecture]
status: stable
created: 2026-06-23
updated: 2026-06-23
aliases: [clean architecture, camadas, arquitetura limpa]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-application]]"
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[concepts/shared-domain-logic]]"
---

# Clean Architecture

> O TCMine organiza o core em três camadas com **dependências apontando para
> dentro**: Domain ← Application ← Infrastructure.

## O que é

Padrão arquitetural em que as regras de negócio (centro) não dependem de
detalhes de infraestrutura (borda). As setas de dependência apontam sempre para
o centro: a borda conhece o centro, nunca o contrário.

## Por que importa para o TCMine

Dois apps muito diferentes — [[entities/tcmine-server]] (web) e
[[entities/tcmine-launcher]] (desktop) — precisam decidir várias coisas **igual**
(filtro por lado, parse de loader, merge de mods, import do CurseForge). Pondo
essa lógica no core, ela não é duplicada nem diverge entre os dois lados (ver
[[concepts/shared-domain-logic]]).

## Detalhes / Variações

- **[[entities/tcmine-domain]]** — entidades, enums e regras puras. Sem EF/ASP.NET.
- **[[entities/tcmine-application]]** — **portas** (interfaces) e **contratos**
  (DTOs `record`) + lógica pura. Depende só de Domain.
- **[[entities/tcmine-server-infrastructure]]** — implementações concretas (EF Core,
  CurseForge, filesystem). Depende de Domain + Application.
- **Entrega** ([[entities/tcmine-server]], [[entities/tcmine-launcher]]) compõe a
  raiz: registra a Infrastructure no DI e expõe a UI/endpoints.

A inversão de dependência aparece concretamente nas portas: `ICurseForgeApi`,
`IUserRepository`, `IPlayerConfigRepository`, `IServerSettingsStore` vivem na
Application; as implementações, na Infrastructure; o DI faz a ligação.

## Aplicação concreta

- O DI mapeia portas → implementações (ex.: `AddTcMineDatabase` registra
  `IUserRepository → UserRepository`). Serviços dependem da abstração.

## Contradições / debates conhecidos

- As `Entities/` do Domain são também as entidades EF (POCOs), com o mapeamento
  em `AppDbContext` (Infrastructure). Pragmatismo aceito: o Domain não referencia
  EF, mas as classes são compartilhadas como modelo de persistência.

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
