---
type: source
title: Leitura de código vivo da solução TCMine (2026-06-23)
tags: [source, code, arquitetura]
status: ingested
created: 2026-06-23
updated: 2026-06-23
source-type: code
origin: "código vivo da solução em P:\\TCMine (branch master, HEAD ab18cef)"
feeds:
  - "[[entities/tcmine-solution]]"
  - "[[entities/tcmine-domain]]"
  - "[[entities/tcmine-application]]"
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[entities/tcmine-design]]"
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-launcher]]"
  - "[[entities/tcmine-icongenerator]]"
related:
  - "[[entities/tcmine-solution]]"
---

# Leitura de código vivo da solução TCMine (2026-06-23)

> Primeira ingestão de conteúdo do wiki: leitura direta dos 7 projetos da
> solução, na raiz `P:\TCMine` (branch `master`, HEAD `ab18cef`).

## Resumo

Leitura para semear as páginas de entidade. Foram lidos os arquivos
estruturantes de cada projeto (build/props, `Program.cs`, contexto EF, portas,
lógica de modpack, design tokens, temas, settings). A wiki estava vazia — sem
contradições a tratar.

## Pontos-chave

- **Stack atual:** .NET 10, Central Package Management. EF Core 10.0.9
  (SQLite + Npgsql 10.0.2), MudBlazor 9.5.0, Avalonia 12.0.4 +
  ReactiveUI.Avalonia 11.3.8, SkiaSharp 3.119.4, FluentValidation 12.1.1,
  Blazilla 2.4.0.
- **Novidades vs. estado anterior** (commit `ab18cef`): `ServerSettingEntity` +
  `IServerSettingsStore` + `ServerSettingsService` + página `Admin/Settings`
  (config de runtime: token CF cifrado, Azure ids, `PublicBaseUrl`); serviços
  Minecraft (`MinecraftAuthService`, `MinecraftVersionService`,
  `ModpackImportService`); `NewsEntity`, `ServerInstanceEntity`,
  `OverrideHistoryEntry`.
- **Banco dual-provider:** `AppDbContext` abstrato + `SqliteAppDbContext`/
  `PostgresAppDbContext` + migrations por provider; `AddTcMineDatabase` resolve
  por env/config (default SQLite `data-server/tcmine.db`).
- **Launcher ainda scaffolded:** `MainWindowViewModel` é o "Welcome to Avalonia!"
  do template; só o tema (`AvaloniaTheme`) está ligado.
- **Server admin:** Dashboard (widgets) + Settings; demais CRUD pendentes.

## O que alimentou na wiki

- Criadas as 8 entidades: [[entities/tcmine-solution]], [[entities/tcmine-domain]],
  [[entities/tcmine-application]], [[entities/tcmine-server-infrastructure]],
  [[entities/tcmine-design]], [[entities/tcmine-server]],
  [[entities/tcmine-launcher]], [[entities/tcmine-icongenerator]].
- Conceitos/decisões referenciados pelas entidades — **criados na batch seguinte
  (mesmo dia)**: [[concepts/clean-architecture]], [[concepts/design-tokens]],
  [[concepts/shared-domain-logic]], [[concepts/modside-rules]],
  [[concepts/curseforge-proxy]], [[concepts/modpack-mods-locais]],
  [[concepts/sse-content-sync]], [[concepts/setup-auth-cookie]],
  [[concepts/player-config-sync]], [[concepts/secrets-data-protection]],
  [[concepts/dtos-as-records]], [[decisions/persistence-dual-provider]],
  [[decisions/central-package-management]].

## Referências

- Arquivos lidos (amostra): `Directory.{Build,Packages}.props`,
  `TCMine-Design/ColorTokens.cs`, `TCMine-Server/Program.cs`,
  `TCMine-Server/Theme/MudThemeFactory.cs`,
  `TCMine-Server/Components/Pages/Admin/Settings.razor.cs`,
  `TCMine-Infrastructure/Persistence/{AppDbContext,DatabaseServiceCollectionExtensions}.cs`,
  `TCMine-Infrastructure/CurseForge/CurseForgeApiClient.cs`,
  `TCMine-Infrastructure/FileSystem/ServerPaths.cs`,
  `TCMine-Infrastructure/Server/ServerSettingsService.cs`,
  `TCMine-Application/Abstractions/CurseForgeApi.cs`,
  `TCMine-Application/Modpack/{ModSetMerge,CurseForgeImporter}.cs`,
  `TCMine-Application/Contracts/Modpack.cs`,
  `TCMine-Domain/Modpack/{ModSideRules,ModLoaders}.cs`,
  `TCMine-Domain/Identity/UserRole.cs`,
  `TCMine-Domain/Entities/{ModpackEntity,ServerSettingEntity}.cs`,
  `TCMine-Launcher/{Program.cs,Theme/AvaloniaTheme.cs,ViewModels/MainWindowViewModel.cs}`,
  `TCMine-IconGenerator/Program.cs`.
