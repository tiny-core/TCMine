---
type: log
title: Log do Wiki TCMine-Docs
tags: [log]
updated: 2026-06-23
---

# Log

Registro cronológico **append-only** de tudo que acontece nesta base de
conhecimento: ingestões, lints, sínteses arquivadas e mudanças estruturais.
**Entradas novas vão no topo** (mais recente primeiro).

Cada entrada começa com um cabeçalho parseável:

```
## [YYYY-MM-DD] <tipo> | <Título>
```

`<tipo>` ∈ `setup` | `ingest` | `decisao` | `lint` | `sintese` | `meta`.

Estrutura sugerida do corpo:

- **Fonte:** caminho em `raw/` ou path de código vivo lido.
- **Páginas afetadas:** `[[entities/...]]`, `[[concepts/...]]`, `[[decisions/...]]`, `[[sources/...]]`.
- **Resumo:** o que mudou e por quê (PT-BR).
- **Pendências:** o que ficou em aberto.

---

## [2026-06-24] meta | Nova regra: sem monolitos (dividir em arquivos menores)

- **Fonte:** instrução do usuário nesta sessão.
- **Páginas afetadas:** `CLAUDE.md` (Parte I, nova seção "Tamanho e
  responsabilidade dos arquivos"); aplicada já em [[concepts/modpack-admin-editor]].
- **Resumo:** adicionada regra obrigatória — **nunca criar monolitos; sempre
  dividir em arquivos menores com responsabilidade própria** (componentes Blazor
  por aba/seção, diálogos em arquivos próprios, partials por área, lógica de
  negócio fora da UI). Aplicada na hora ao refatorar a aba de overrides para o
  componente `OverridesPanel` + `OverrideTreeBuilder`.
- **Pendências:** revisitar outros arquivos grandes do servidor conforme forem
  tocados. Nada commitado (regra §11).

## [2026-06-24] ingest | Modpacks: import bloqueante, DataGrid virtualizado, MudTreeView, fix Monaco

- **Fonte:** código vivo escrito nesta sessão (ajustes pedidos pelo usuário).
- **Páginas afetadas:** [[concepts/modpack-admin-editor]] (onde vive, overrides,
  Monaco, BlazorMonaco 3.4.0), [[sources/2026-06-24-modpack-admin-ui]] (a manter).
- **Resumo:** (1) **import de modpack** agora abre `ImportProgressDialog` — modal
  de feedback **bloqueante** (sem fechar/ESC/backdrop) fechado por código ao
  terminar; (2) lista de modpacks migrada de `MudTable` para **`MudDataGrid`
  virtualizado** (`Virtualize`, `FixedHeader`, `Height`); (3) lista de overrides
  virou **`MudTreeView`** (árvore hierárquica via `OverrideTreeBuilder`); (4)
  **corrigido** o Monaco não abrir o arquivo — o `StandaloneCodeEditor` era
  renderizado só após a seleção (ref `null` no `SetValue`); agora fica sempre
  montado e aplica conteúdo pendente no `OnDidInit`. Aba de overrides extraída
  para `OverridesPanel` (regra sem-monolitos). BlazorMonaco bumpado p/ **3.4.0**.
  Build da solução: 0 erros.
- **Pendências:**
  - Mover arquivo/pasta de override na UI (serviço já suporta; árvore ainda só
    seleciona/edita/apaga).
  - UI do feed global de notícias. Nada commitado (regra §11).

## [2026-06-24] decisao | Newsletter por modpack — FK opcional em NewsEntity

- **Fonte:** decisão do usuário nesta sessão (escolha entre FK opcional / FK
  obrigatória / adiar) — optou por **FK opcional**.
- **Páginas afetadas:** [[concepts/modpack-admin-editor]] (seção Newsletter +
  abas + políticas de escrita), [[entities/tcmine-server]] (pendência fechada).
- **Resumo:** resolvido o gatilho §5 deixado em aberto. `NewsEntity.ModpackId`
  agora é `Guid?` (null = notícia global; preenchido = do modpack), com
  relacionamento `OnDelete(Cascade)` e índice em `ModpackId`. Migration
  `NewsModpackFk` gerada e verificada nos **dois** providers (SQLite TEXT /
  Postgres uuid) — contém só coluna + índice + FK (o snapshot já tinha o resto do
  modelo de modpack). Adicionados `ModpackNewsService` (CRUD direto no banco com
  `ContentNotifier.Bump()`), a aba **Novidades** no editor e o `NewsEditDialog`.
  Build da solução: 0 erros. Corrigido também um `continue` colado por engano na
  linha 1 do `ModpackEditor.razor`.
- **Pendências:**
  - UI para o **feed global** de notícias (FK nula) — ainda não existe.
  - Aplicar a migration ao banco de dev no próximo `dotnet run` (auto no boot).
  - Nada commitado (regra §11) — código + docs tocados; usuário decide o commit.

## [2026-06-24] ingest | UI admin de modpacks + BlazorMonaco

- **Fonte:** código vivo escrito nesta sessão ([[sources/2026-06-24-modpack-admin-ui]]);
  base no `ModpackImportService` já existente ([[entities/tcmine-infrastructure]]).
- **Páginas afetadas:** criado [[concepts/modpack-admin-editor]] e
  [[sources/2026-06-24-modpack-admin-ui]]; atualizado [[entities/tcmine-server]]
  (componentes, decisões, pendências, frontmatter); `index.md` atualizado.
- **Resumo:** entregue a camada Blazor de criação/edição de modpacks em
  `TCMine-Server/Components/Pages/Admin/Modpacks/` — lista (`Modpacks.razor`),
  editor em abas (`ModpackEditor` + parcial `.Overrides.cs`) e 4 diálogos. Cobre
  busca/import CurseForge, upload de jar, marcação `Side`/`Target` por mod, e
  edição de overrides com **BlazorMonaco 3.3.0** (adicionado à gestão central de
  pacotes, 3 scripts no `App.razor`). Confirmada a API da v3.3.0 contra a fonte
  oficial (`StandaloneCodeEditor`, `Global.SetModelLanguage(IJSRuntime,...)`).
  Build da solução: 0 erros. Mistura deliberada de duas políticas de escrita:
  rascunho-só-ao-Guardar (metadados/mods/servidores) vs. disco-imediato
  (overrides, com histórico/desfazer).
- **Pendências:**
  - **Newsletter por modpack** (pedida): contradiz o `NewsEntity` global —
    gatilho §5 (contradição + impacto amplo). Adiada à decisão do usuário sobre o
    modelo (FK em `NewsEntity` vs. entidade nova) + migrations nos dois providers.
  - Expor mover arquivo/pasta de override na UI (serviço já suporta).
  - Restringir `Settings` ao `Owner`; criar instância de servidor a partir do modpack.
  - Nada commitado (regra §11) — docs e código tocados; usuário decide o commit.

## [2026-06-23] ingest | Conceitos e decisões transversais (batch)

- **Fonte:** mesma leitura de código vivo ([[sources/2026-06-23-leitura-codigo-vivo]]),
  aprofundada com `Endpoints/{CurseForgeProxy,Modpack,Events,PlayerConfig}Endpoints.cs`,
  `Authentication/{AuthClaims,PersistingAuthenticationStateProvider}.cs`,
  `Infrastructure/Server/{ContentNotifier,ContentCatalog}.cs`,
  `Infrastructure/Minecraft/MinecraftAuthService.cs`,
  `Infrastructure/Identity/{UserService,SetupState}.cs`.
- **Páginas afetadas:** criados 11 conceitos ([[concepts/clean-architecture]],
  [[concepts/shared-domain-logic]], [[concepts/modside-rules]],
  [[concepts/dtos-as-records]], [[concepts/design-tokens]],
  [[concepts/curseforge-proxy]], [[concepts/modpack-mods-locais]],
  [[concepts/sse-content-sync]], [[concepts/setup-auth-cookie]],
  [[concepts/secrets-data-protection]], [[concepts/player-config-sync]]) e 2
  decisões ([[decisions/persistence-dual-provider]],
  [[decisions/central-package-management]]); `index.md` atualizado;
  [[sources/2026-06-23-leitura-codigo-vivo]] corrigida (não estão mais "por criar").
- **Resumo:** fechados os links que as entidades referenciavam. Cada página foi
  verificada contra o código atual — detalhes não-óbvios capturados: proxy `/v1`
  devolve 503 sem key e é passthrough transparente; manifesto filtra por
  `RunsOnClient` e reescreve URLs para `/files/...`; SSE usa canal limitado
  `DropOldest` + keep-alive 25s; `MinecraftAuthService` é **fail-open**; senhas em
  **PBKDF2** (`PasswordHasher`); secrets cifrados com protector
  `TCMine.ServerSettings.v1`, sem fallback de env var.
- **Pendências:**
  - `player-config-sync` em `status: wip`: só o PUT existe; o GET de leitura é
    citado no comentário mas não há rota — confirmar onde/se será servido.
  - Disparo de `ContentNotifier.Bump()` nas mutações depende do CRUD admin (a fazer).
  - Restringir `Admin/Settings` ao papel `Owner`.
  - Nada commitado (regra §11).

## [2026-06-23] ingest | Leitura inicial da solução TCMine (código vivo)

- **Fonte:** leitura de código vivo dos 7 projetos em `P:\TCMine` (branch
  `master`, HEAD `ab18cef`); resumo em [[sources/2026-06-23-leitura-codigo-vivo]].
- **Páginas afetadas:** criadas as entidades [[entities/tcmine-solution]],
  [[entities/tcmine-domain]], [[entities/tcmine-application]],
  [[entities/tcmine-infrastructure]], [[entities/tcmine-design]],
  [[entities/tcmine-server]], [[entities/tcmine-launcher]],
  [[entities/tcmine-icongenerator]]; criada a fonte
  [[sources/2026-06-23-leitura-codigo-vivo]]; `index.md` atualizado.
- **Resumo:** primeira ingestão de conteúdo. Semeada uma página por projeto, a
  partir da leitura do código atual (não da versão antiga commitada). Capturado o
  estado real: servidor com backend + admin (Dashboard, Settings) e settings de
  runtime cifradas (`ServerSettingsService`); launcher ainda **scaffolded**;
  serviços Minecraft e orquestração de `ServerInstance` modelados mas não
  operados. Sem contradições (wiki estava vazia). Também re-adicionada a
  ferramenta `tools/wikisearch.py` (a pedido) e referenciada no `CLAUDE.md`
  (§3 e §8 do schema).
- **Pendências:**
  - Criar as páginas de `concepts/` e `decisions/` referenciadas pelas entidades
    (hoje links não resolvidos): clean-architecture, design-tokens,
    shared-domain-logic, modside-rules, curseforge-proxy, modpack-mods-locais,
    sse-content-sync, setup-auth-cookie, player-config-sync,
    secrets-data-protection, dtos-as-records, persistence-dual-provider,
    central-package-management.
  - Aprofundar entidades em `status: stub/wip` conforme o código evoluir.
  - Nada commitado (regra §11).

## [2026-06-23] setup | Criação do sistema de wiki TCMine-Docs

- **Fonte:** prompt de setup do usuário (sessão de 2026-06-23).
- **Páginas afetadas:** estrutura inicial criada — `CLAUDE.md` (raiz, schema
  mestre), `wiki/index.md`, `wiki/log.md`, templates (`entity`, `concept`,
  `decision`, `source`), pastas `raw/assets/`, `wiki/entities/`,
  `wiki/concepts/`, `wiki/decisions/`, `wiki/sources/`.
- **Resumo:** Criada do zero a base de conhecimento LLM-mantida em
  `TCMine-Docs/`, no mesmo repositório git da solução TCMine. Decisões tomadas
  com o usuário nesta sessão:
  1. **Start fresh** — não restaurar a versão anterior commitada em `252a8c1`
     (17 páginas + ferramenta de busca + vault Obsidian); ela permanece no
     histórico git se um dia for necessária.
  2. **Estrutura em inglês com `decisions/` separado** — categorias
     `entities/concepts/decisions/sources/templates`; decisões viram ADRs
     próprios, distintos dos conceitos.
  3. **Ingest autônomo** por padrão, com confirmação só em três gatilhos
     (contradição / impacto amplo / categorização incerta).
  4. **Health-check automático no boot** de cada sessão (report-only; correções
     além do trivial exigem ok do usuário).
  5. **`CLAUDE.md` único na raiz** com a constituição completa da wiki + o
     básico do projeto (idioma, comandos, arquitetura); sem `CLAUDE.md` próprio
     em `TCMine-Docs/`.
- **Pendências:**
  - Primeira ingestão real de conteúdo (artigo, código vivo ou export de chat) —
    a wiki está vazia.
  - Nada commitado (regra §11). A working tree contém a remoção dos arquivos
    antigos de `TCMine-Docs/` e do `CLAUDE.md` anterior, mais os arquivos novos
    desta sessão — o usuário decide como commitar.
