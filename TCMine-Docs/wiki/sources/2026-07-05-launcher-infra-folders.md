---
type: source
title: Reorganização do TCMine-Launcher.Infrastructure em pastas por área de domínio
tags: [source, code, launcher, infrastructure, refactor, organizacao]
status: ingested
created: 2026-07-05
updated: 2026-07-05
source-type: code
origin: "código vivo — TCMine-Launcher.Infrastructure (+ 2 consumidores no TCMine-Launcher)"
feeds:
  - "[[entities/tcmine-launcher-infrastructure]]"
related:
  - "[[decisions/launcher-clean-architecture]]"
  - "[[sources/2026-06-29-launcher-clean-architecture]]"
---

# Reorganização do TCMine-Launcher.Infrastructure em pastas por área de domínio

A pedido do usuário: os ~21 arquivos da infra do launcher estavam **todos na raiz** do projeto; passaram
a ser agrupados em **pastas por área de domínio**, espelhando o `TCMine-Server.Infrastructure` (que usa
pastas com **namespace casando**).

## O que mudou

- **9 pastas** criadas, com o namespace file-scoped de cada arquivo reescrito para
  `TCMine_Launcher.Infrastructure.<Pasta>` (ver a lista em [[entities/tcmine-launcher-infrastructure]]):
  `Auth/`, `Configuration/`, `Content/`, `FileSystem/`, `Launch/`, `Networking/`, `Persistence/`,
  `Platform/`, `Updates/`.
- Arquivos movidos com **`git mv`** (preserva histórico). Cross-references intra-projeto resolvidos com
  `using` explícitos por arquivo (mesmo padrão do server infra), computados por análise de dependências.
- **Consumidores** ajustados: `TCMine-Launcher/Program.cs` (composition root — troca o único
  `using TCMine_Launcher.Infrastructure;` pelos 8 usings de pasta) e `TCMine-Launcher/Behaviors/ImageLoader.cs`
  (`Networking` + `FileSystem`). Eram os **únicos** dois pontos que importavam o namespace da infra.

## Notas / armadilhas

- **`Platform/`, não `System/`:** um namespace `TCMine_Launcher.Infrastructure.System` faria referências a
  `System.*` resolverem primeiro contra o sub-namespace local (colisão) — por isso `SystemInfo` foi para
  `Platform/`.
- **Ordem de binding do Roslyn:** ao mover, o build primeiro só acusou `CS0246` de tipos em **assinaturas**
  (parâmetros de primary constructor: `ServerConfig`/`AuthService`); os `CS0103` de referências em **corpos
  de método** (`LauncherPaths`, `HttpClientProvider`, `AppConfig`) só apareceram depois de resolver as
  assinaturas — o compilador não liga os corpos enquanto as assinaturas falham. Confirma a regra: namespaces
  **irmãos** não são visíveis sem `using` (só os ancestrais são).
- **Sem `.editorconfig`** e sem `TreatWarningsAsErrors` no projeto; cabeçalhos de `using` normalizados
  manualmente (contíguos + 1 linha em branco antes do `namespace`), como o server infra.

## Verificação

- Refactor **puramente estrutural** (nenhuma mudança de comportamento). Solução inteira compila **0 erro**
  (`dotnet build TCMine.slnx --no-incremental`).
