---
type: source
title: Refactor P0 — remoção do proxy CurseForge + split do ModpackImportService
tags: [source, code, refactor, curseforge, segurança, modpack, overrides]
status: ingested
created: 2026-07-05
updated: 2026-07-05
source-type: code
origin: código vivo (sessão de análise + refactor P0)
feeds:
  - "[[concepts/curseforge-proxy]]"
  - "[[concepts/modpack-admin-editor]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-server-infrastructure]]"
related:
  - "[[concepts/modpack-mods-locais]]"
  - "[[concepts/secrets-data-protection]]"
---

# Refactor P0 — remoção do proxy CurseForge + split do ModpackImportService

> Duas mudanças de maior retorno de uma análise completa da solução: remover o
> proxy `/v1` (código morto + buraco de segurança) e quebrar o monolito
> `ModpackImportService`.

## Resumo

Uma análise dos 7 projetos apontou o código como já limpo (Clean Architecture
respeitada, build sem warnings relevantes). Dois pontos "P0" foram tratados nesta
sessão; os demais (testes, dedup de `isAsset`, analisadores) ficaram para P1.

## Pontos-chave

### 1. Proxy CurseForge removido

- O endpoint `/v1/{**path}` era **público, sem autenticação e sem rate limiting**,
  injetando a `x-api-key` do servidor — abusável como proxy CF grátis.
- **Nenhum consumidor de primeira parte o usava:** o launcher baixa jars de
  `/files` ([[concepts/modpack-mods-locais]]) e o catálogo de `/api/modpacks`; o
  admin (Blazor Server) usa o `CurseForgeApiClient` **in-process**, que fala direto
  com `api.curseforge.com`. Era código morto **e** superfície de ataque.
- **Removido:** `CurseForgeProxyEndpoints.cs` (deletado), `MapCurseForgeProxy()` no
  `Program.cs`, e a referência `/v1` em `IsApiPath`. Decisão validada com o usuário
  (revê [[concepts/curseforge-proxy]], agora **descontinuado**).

### 2. `ModpackImportService` dividido (sem monolitos)

- O serviço tinha **1223 linhas** misturando ≥5 responsabilidades, violando a
  própria regra "sem monolitos" do `CLAUDE.md`.
- Extraído `ModpackOverridesService`
  (`TCMine-Server.Infrastructure/Minecraft/ModpackOverridesService.cs`, ~430 linhas):
  toda a **edição interativa de overrides** (listar/ler/gravar/criar/upload/mover/
  apagar arquivos e pastas) + **histórico/desfazer** + guarda de path traversal
  (`SafeOverridePath`). Deps: `AppDbContext`, `ContentNotifier`, `IHostEnvironment`.
- `ModpackImportService` caiu para **794 linhas**, com responsabilidade única
  (import/add/save/update-check/cache de jars). O `ExtractOverrides` (bundle inicial
  do import) ficou com o `SaveAsync`.
- Consumidores migrados de `ModpackImportService` → `ModpackOverridesService`:
  `OverridesPanel.razor.cs`, `OverrideHistoryDialog.razor`, `OverridesTreeSource.cs`
  (+ `IsTextOverride` agora estático no novo serviço). Novo registro DI (scoped) no
  `Program.cs`. Build verde.

## O que alimentou na wiki

- [[concepts/curseforge-proxy]] — marcado **descontinuado**, corpo reescrito.
- [[concepts/modpack-admin-editor]] — nota do split dos dois serviços.
- [[entities/tcmine-server]] / [[entities/tcmine-server-infrastructure]] — a
  atualizar se listarem o endpoint/serviço explicitamente.

## Pendências

- **P1 concluído** (ver log `[2026-07-05] ingest`): analisadores ligados + curados,
  dedup do `isAsset`, e projeto `TCMine-Tests` (xUnit, 39 testes verdes sobre a lógica
  pura do core). Falta cobrir o manifesto de player-config (mais acoplado a I/O).
- **Backlog de analisadores:** CA1001 (dispose em serviços DI), CA1068 (ordem do
  `CancellationToken` em `GameLauncher`/`ModInstaller`), CA1859/CS0618.
- Considerar extrair também `ModpackUpdateService` e `ModFileCacheService` do
  `ModpackImportService` num passo futuro (menor retorno; ficam acoplados ao Save).

## Referências

- `CLAUDE.md` (regra "sem monolitos"; CurseForge via servidor).
