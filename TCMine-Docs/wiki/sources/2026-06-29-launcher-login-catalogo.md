---
type: source
title: Launcher — login Microsoft pelo servidor + catálogo (2026-06-29)
tags: [source, code, launcher, auth, avalonia]
status: ingested
created: 2026-06-29
updated: 2026-06-29
source-type: code
origin: sessão de implementação (código vivo TCMine-Server, TCMine-Infrastructure, TCMine-Launcher)
feeds:
  - "[[decisions/server-brokered-microsoft-login]]"
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-server]]"
---

# Launcher — login Microsoft pelo servidor + catálogo (2026-06-29)

Primeiro incremento real do [[entities/tcmine-launcher]]: sai do scaffold e ganha **login + catálogo**.
Restrições do usuário: launcher **dependente do servidor** (sem instâncias locais), **login Microsoft
orquestrado pelo servidor**, **sem código manual**.

## O que foi implementado

**Servidor ([[entities/tcmine-server]] + [[entities/tcmine-server-infrastructure]]):**

- `MicrosoftAuthService` (Infrastructure/Minecraft) — cadeia OAuth com **PKCE**: authorize URL, troca
  de code, refresh, e Xbox Live → XSTS → `login_with_xbox` → perfil. Lê o Azure client/tenant id das
  settings cifradas. Complementa o `MinecraftAuthService` (que só valida token).
- `PlayerAccountEntity` (Domain) + `IPlayerAccountRepository`/`PlayerAccountRepository` + migrações
  `PlayerAccount` (Sqlite e Postgres). Guarda o **refresh token MS cifrado** (Data Protection,
  protector `TCMine.PlayerAuth.v1`) e o **hash** da sessão TCMine.
- `PlayerSessionService` (Infrastructure/Identity) — cifra o refresh, emite/renova a sessão.
- `LoginSessionBroker` (Server/Services) — a "live link" **direcionada** por `loginId` (vs. o
  broadcast do `ContentNotifier`/`/events`). Guarda o verifier PKCE; entrega o resultado por TCS.
- `AuthEndpoints` — `POST /api/auth/microsoft/start`, `GET /api/auth/microsoft/wait/{loginId}` (SSE),
  `GET /auth/microsoft/callback`, `POST /api/auth/session/refresh`, `POST /api/auth/logout`. Rate
  limit `auth` (20/min por IP). Registos no `Program.cs`.

**Launcher ([[entities/tcmine-launcher]]):**

- Referência nova ao `TCMine-Application` para reusar os DTOs `record` do fio (sem duplicar).
- `Services/`: `ServerConfig`, `LauncherPaths`, `PlayerSession`, `SessionStore` (DPAPI no Windows),
  `ApiClient` (catálogo + auth; SSE de espera de login), `AuthService` (start/restore/logout, abre o
  navegador).
- MVVM: `MainWindowViewModel` (shell/gate login↔catálogo), `LoginViewModel`, `ModpacksPageViewModel`;
  views `LoginView`, `ModpacksPageView`, `MainWindow` atualizada. DI via Splat no `Program.cs`.

## Decisão derivada

- [[decisions/server-brokered-microsoft-login]] — o "como" e os trade-offs do login pelo servidor.

## Pendências

- **Setup externo:** registar o redirect URI `{PublicBaseUrl}/auth/microsoft/callback` no Azure.
- **Out of scope deste incremento:** baixar manifesto/jars/overrides, montar instância e lançar o jogo.
- Tema do launcher ainda no Fluent default (a integração `ColorTokens`/`AvaloniaTheme` foi removida num
  commit anterior; re-introduzir é pendência).
- `ServerConfig.BaseUrl` ainda é constante (dev `https://localhost:7002`); falta torná-la configurável.
