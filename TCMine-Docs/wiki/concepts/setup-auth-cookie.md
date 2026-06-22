---
type: concept
title: Primeira execução, setup e autenticação por cookie
tags: [concept, auth, setup, cookie, identidade, roles]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [setup, primeira execução, auth cookie, UserRole, login]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-domain]]"
---

# Primeira execução, setup e autenticação por cookie

> Sem usuários, o servidor força `/setup` para criar o **Owner**. Depois, login por
> **cookie** com papéis `Owner/Admin/Operator/Viewer`.

## O que é

Fluxo de identidade do painel admin:

1. **Primeira execução** — `SetupState` (singleton com cache) detecta "existe
   algum usuário?". Enquanto não, um middleware redireciona qualquer rota (exceto
   assets e páginas de framework) para `/setup`. Após inicializado, `/setup` deixa
   de existir (volta a `/login`).
2. **Setup** — cria o primeiro usuário com papel `Owner` (controla usuários e
   secrets). Senha nunca em texto: `PasswordHash` PBKDF2 (`UserService`).
3. **Login/cookie** — valida o hash e emite o cookie `tcmine_auth` (HttpOnly,
   SameSite=Lax, 7 dias, sliding). `AuthClaims` monta o `ClaimsPrincipal`.
4. **Estado no Blazor** — `PersistingAuthenticationStateProvider` lê a identidade
   do cookie no prerender e a persiste para o circuito interativo (substituiu o
   estado em memória, que se perdia no F5).

## Por que importa para o TCMine

Substitui a antiga senha única (`ADMIN_PASSWORD`) por **usuários reais com papéis**.
`UserRole`: `Owner` > `Admin` (conteúdo) > `Operator` (start/stop de servidores) >
`Viewer` (leitura). Regras protetoras: não remover/rebaixar o último Owner ativo.

## Detalhes / Variações

- Login/logout fazem navegação com **reload SSR completo**, reiniciando o circuito
  e relendo o cookie — sem revalidação contínua.
- `AccessDeniedPath = /admin` (o `AdminLayout` mostra a tela 403, sem esquema próprio).
- `/auth/logout` limpa o cookie (GET, por simplicidade).

## Aplicação concreta

- `TCMine-Server/Program.cs` (cookie + middleware de setup),
  `Authentication/` (`AuthClaims`, `PersistingAuthenticationStateProvider`),
  páginas `Login`/`Setup`; `SetupState`/`UserService`/`UserRepository` em
  [[entities/tcmine-infrastructure]]; `UserEntity`/`UserRole` em [[entities/tcmine-domain]].

## Contradições / debates conhecidos

- (nenhum até agora)

## Referências

- [[sources/2026-06-22-leitura-codigo-vivo]]
