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
