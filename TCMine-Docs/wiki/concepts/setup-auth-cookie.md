---
type: concept
title: Setup inicial e autenticação por cookie
tags: [concept, auth, setup, roles, segurança]
status: stable
created: 2026-06-23
updated: 2026-06-23
aliases: [setup, auth cookie, primeira execução, Owner, papéis]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-domain]]"
  - "[[concepts/secrets-data-protection]]"
---

# Setup inicial e autenticação por cookie

> Sem usuário, o servidor força `/setup` para criar o `Owner`; depois, login emite
> um **cookie** (`tcmine_auth`) e os componentes Blazor leem a identidade dele.

## O que é

Identidade baseada em **usuários no banco** + **cookie de autenticação**.
Substituiu a antiga senha única `ADMIN_PASSWORD` e o estado de auth em memória
(perdido no F5).

## Por que importa para o TCMine

Permite múltiplos operadores com papéis distintos, sessões persistentes (sobrevivem
ao refresh) e uma porta de entrada segura para gerir conteúdo e segredos.

## Detalhes / Variações

- **Primeira execução:** `SetupState` (singleton com cache `volatile`) checa
  `AnyUsersExistAsync`; enquanto não há usuário, um middleware redireciona tudo
  (exceto assets, `/setup`, `/Error`, `/not-found`) para `/setup`. Após criado,
  `/setup` deixa de existir (volta a `/login`).
- **Papéis** (`UserRole`, [[entities/tcmine-domain]]): `Owner` > `Admin` >
  `Operator` > `Viewer`. `UserService` protege o **último Owner ativo** contra
  remoção/rebaixamento/desativação (`CountActiveOwnersAsync`).
- **Senhas:** hash via `PasswordHasher<UserEntity>` (**PBKDF2**), com rehash
  automático (`SuccessRehashNeeded`). Login é case-insensitive (normalizado para
  minúsculas).
- **Cookie:** `tcmine_auth`, `HttpOnly`, `SameSite=Lax`, expira em 7 dias com
  `SlidingExpiration`. `AuthClaims` monta o `ClaimsPrincipal` (NameIdentifier,
  Name, Role).
- **Blazor:** `PersistingAuthenticationStateProvider` lê `HttpContext.User` no
  prerender e o persiste (`PersistentComponentState`); no circuito interativo,
  restaura a identidade — assim `<AuthorizeView>` funciona em Dashboard/Settings.
  Login/logout fazem reload SSR completo.

## Aplicação concreta

- `TCMine-Infrastructure/Identity/{UserService,SetupState}.cs`;
  `TCMine-Server/Authentication/{AuthClaims,PersistingAuthenticationStateProvider}.cs`;
  `TCMine-Server/Program.cs` (cookie + middleware de primeira execução).

## Contradições / debates conhecidos

- A página `Admin/Settings` (segredos) hoje aceita **qualquer admin autenticado**;
  restringi-la ao `Owner` é pendência registrada no código.

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
