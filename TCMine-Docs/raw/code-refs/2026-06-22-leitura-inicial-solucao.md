# Leitura inicial da solução TCMine

- **Data:** 2026-06-22
- **Tipo:** code-ref (leitura de código vivo)

## Arquivos/paths lidos

Solução inteira em `P:\TCMine\` (branch `master`), exceto `bin/`/`obj/`:

- Raiz: `TCMine.slnx`, `Directory.Build.props`, `Directory.Packages.props`, `compose.yaml`.
- `TCMine-Domain/` — `Entities/*.cs`, `Identity/UserRole.cs`, `Modpack/ModLoaders.cs`, `Modpack/ModSideRules.cs`.
- `TCMine-Application/` — `Abstractions/*.cs`, `Contracts/*.cs`, `Identity/UserInfo.cs`, `Modpack/CurseForgeImporter.cs`, `Modpack/ModSetMerge.cs`.
- `TCMine-Infrastructure/` — `Persistence/*` (`AppDbContext`, `DatabaseOptions`, `DatabaseServiceCollectionExtensions`, sub/contexts, repositories), `FileSystem/ServerPaths.cs`, `Identity/SetupState.cs`, `Server/ServerSettingsService.cs`, `Minecraft/MinecraftAuthService.cs` (e demais serviços por nome).
- `TCMine-Design/ColorTokens.cs`.
- `TCMine-Server/` — `Program.cs`, `Endpoints/*.cs`, `Authentication/*.cs`, `Theme/MudThemeFactory.cs`, listagem de `Components/` (layouts, páginas Admin/Login/Setup/Error, widgets do dashboard, shared).
- `TCMine-Launcher/` — `Program.cs`, `App.axaml.cs`, `Theme/AvaloniaTheme.cs`, csproj.
- `TCMine-IconGenerator/` — `Program.cs`, `Utility.cs` (por nome).

## Takeaways (sem o código)

- A solução é uma **Clean Architecture .NET 10** com camada core (`Domain`/`Application`/`Infrastructure`), um design system compartilhado (`TCMine-Design`), o backend Blazor Server + Minimal API (`TCMine-Server`), o launcher desktop Avalonia (`TCMine-Launcher`) e um gerador de ícones (`TCMine-IconGenerator`).
- **Central Package Management** (`Directory.Packages.props`): uma versão por pacote em toda a solução.
- Princípio recorrente: **lógica pura de modpack compartilhada** entre servidor e launcher vive no core (`CurseForgeImporter`, `ModSetMerge`, `ModSideRules`, `ModLoaders`), para os dois lados decidirem igual.
- O **servidor serve os jars** dos mods (`/files/{fileId}/{fileName}`); o launcher baixa do servidor, nunca do CurseForge.
- **CurseForge só via proxy do servidor** (`/v1/*`): a `x-api-key` nunca sai do servidor.
- **Secrets cifrados** no banco via Data Protection; config de bootstrap do banco (provider/connection) fica fora do banco.
- **Primeira execução** força `/setup` do usuário Owner; auth por cookie; papéis `Owner/Admin/Operator/Viewer`.
- Banco com **dois providers** (SQLite default / Postgres), cada um com `AppDbContext` concreto e migrations próprias.

> Código muda — reverificar antes de confiar nesta nota. Alimenta as páginas em `wiki/entities/` e `wiki/concepts/`.
