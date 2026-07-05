---
type: source
title: Tela admin de configs dos jogadores + endurecimento da API de sync
tags: [source, code, player-config, admin, seguranca, blazor]
status: ingested
created: 2026-07-05
updated: 2026-07-05
source-type: code
origin: "código vivo — TCMine-Server + TCMine-Server.Infrastructure + TCMine-Launcher.Infrastructure"
feeds:
  - "[[concepts/player-config-sync]]"
  - "[[entities/tcmine-server]]"
related:
  - "[[sources/2026-07-03-player-config-sync-completo]]"
  - "[[concepts/setup-auth-cookie]]"
---

# Tela admin de configs dos jogadores + endurecimento da API de sync

A pedido do usuário: **(1)** uma tela para gerir as configs dos players; **(2)** fechar as brechas da API
de sync que ele identificou ("o server aceita arquivos sem checar identidade? qualquer um lota o servidor").

## Contexto — o que já era seguro e o que não era

Investigação de [[concepts/player-config-sync]] revelou: a **escrita** (`PUT …/push`) **já** era autenticada
(token Minecraft do próprio UUID via `MinecraftAuthService`), com teto de 256 MB/pedido, zip-slip guard e
rate limit por IP. As brechas reais eram:

1. **Leituras abertas** — `GET …/manifest` e `POST …/bundle` sem auth: qualquer um com um `(uuid, modpackId)`
   (UUID é público) baixava configs alheias e podia usar o `bundle` como amplificador de banda.
2. **Sem cota de disco** — nada limitava o total; um token válido (ou o fail-open da validação Mojang)
   podia empurrar 256 MB repetidamente e encher o disco.

Decisão do usuário: tela com **ver + apagar + uso por jogador** e endurecimento **completo** (fechar reads +
cota).

## O que mudou

### Endurecimento da API (server)
- **`TCMine-Server/Endpoints/PlayerConfigEndpoints.cs`**:
  - `GET /manifest` e `POST /bundle` agora **autenticados** — novo helper `AuthorizeReadAsync` (401 sem
    token, 403 se não pertence ao UUID), reutilizando o `MinecraftAuthService` do push.
  - **Cota por conjunto** no `PUT /push`: soma os `Size` do manifesto recebido e rejeita com `413` se
    exceder `PlayerConfigs:MaxSetMb` (lido de `IConfiguration` no map; default **1 GB**).

### Launcher (consumidor dos reads)
- **`TCMine-Launcher.Infrastructure/PlayerConfigSync.cs`**: `PullAsync` ganhou o parâmetro `accessToken` e
  manda `Authorization: Bearer` no manifest e no bundle (helper `Authorized`); o GET interno do `PushAsync`
  também. Guarda: sem uuid/token, o pull é no-op.
- **`TCMine-Launcher.Infrastructure/LaunchOrchestrator.cs`**: passa `session.AccessToken` ao `PullAsync`.

### Tela admin (server)
- **`TCMine-Server.Infrastructure/PlayerConfigs/PlayerConfigAdminService.cs`** (novo, scoped): `ListAsync`
  varre `player-configs/`, mede tamanho/contagem por `(uuid, modpackId)` (iterativo, pula reparse points,
  em `Task.Run`), lê o `UpdatedAt` do manifesto e resolve `modpackId`→nome pela BD; `DeleteSetAsync` /
  `DeletePlayerAsync` apagam com guarda anti path-traversal. DTOs `PlayerConfigSetDto` /
  `PlayerConfigOverviewDto` (em `TCMine-Application/Contracts`).
- **`TCMine-Server/Components/Pages/Admin/Players/PlayerConfigs.razor(.cs)`** (novo): rota `/admin/players`
  (Owner/Admin). `MudDataGrid` **agrupado por jogador** (`GroupTemplate` com total e botão de apagar tudo),
  colunas modpack/ficheiros/tamanho/último sync, apagar por conjunto; total em disco no cabeçalho.
  `BusyOverlay` + confirmação (`ShowMessageBoxAsync`). Nav link em `AdminLayout.razor`
  (`Icons.Material.Filled.FolderShared`). Registro `AddScoped<PlayerConfigAdminService>()` no `Program.cs`.

## Notas / armadilhas

- **Fail-open agora também afeta leitura:** numa indisponibilidade da Mojang, o `MinecraftAuthService`
  autoriza — logo os GETs reabrem temporariamente. Aceite (settings de jogo, sem segredos); a cota limita o
  lado da escrita.
- **Cota vs. cache de mapa:** 1 GB por conjunto foi escolhido por cobrir o cache de mapa (dezenas/centenas
  de MB) sem virar vetor de flood. Configurável via `PlayerConfigs:MaxSetMb`.
- **`MudDataGrid` grouping:** o `GroupTemplate` recebe `GroupDefinition<T>`; usa-se `context.Grouping.Key`
  (uuid) e `context.Grouping` (itens do grupo) para o total e o botão de apagar-tudo.

## Verificação

- Solução compila **0 erro** (3 warnings pré-existentes só no `TCMine-IconGenerator`, SkiaSharp obsoleto).
- `TCMine-Server` **sobe** (boot smoke test: "Now listening"/"Application started") — DI, mapeamento de
  endpoints (incl. leitura de config no map) e migrations OK.
- Tela admin **não** verificada visualmente (atrás de login). Segue o padrão `MudDataGrid`/`BusyOverlay` já
  validado.
