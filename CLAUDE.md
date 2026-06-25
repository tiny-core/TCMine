# CLAUDE.md

Instruções para o Claude Code ao trabalhar neste repositório. Este arquivo é a
**configuração mestre**: governa tanto o trabalho de **código** na solução
TCMine quanto a **base de conhecimento LLM-mantida** em `TCMine-Docs/`. Leia-o
por inteiro antes de começar qualquer tarefa.

> Este arquivo **evolui**. Quando você e o usuário refinarem uma convenção,
> atualize-o aqui e registre a mudança no `TCMine-Docs/wiki/log.md` (tipo `meta`).

---

# Parte I — Projeto TCMine (código)

## Idioma e comentários

**Comentários em PT-BR; todo o resto em inglês.**

- Comentários `//` e `/* */` → PT-BR, explicando o *porquê* além do *o quê*.
- Nomes de variáveis, propriedades, métodos, classes → inglês.
- Strings e labels da UI → inglês (salvo decisão explícita de produto).
- O desenvolvedor está **aprendendo .NET**: explique decisões não-óbvias.
  Comentário de bloco para classes/métodos; inline para linhas não-óbvias.

```csharp
// Desabilita o botão durante a requisição para evitar duplo envio
private bool _loading;

// Singleton: criado uma vez e reutilizado em todo o app (sem estado por requisição)
collection.AddSingleton<SystemMetricsService>();
```

> A mesma regra de idioma vale para a wiki — prosa em PT-BR, código/paths/nomes
> próprios em inglês (ver Parte II §4).

## Tamanho e responsabilidade dos arquivos (sem monolitos)

**Nunca crie monolitos. Sempre divida em arquivos menores, cada um com a sua
própria responsabilidade.**

- Uma classe/componente = **uma responsabilidade clara**. Se um arquivo começa a
  acumular preocupações distintas, **extraia** (componente, serviço, partial).
- **Componentes Blazor:** páginas orquestram; o conteúdo de cada aba/seção/painel
  vai para o **seu próprio componente** (ex.: `OverridesPanel.razor`,
  `ModsPanel.razor`). Diálogos sempre em arquivos próprios.
- **Code-behind:** prefira `partial class` por área (`.razor.cs`,
  `.Overrides.cs`, …) a um único arquivo gigante.
- **Lógica de negócio não vive na UI:** vai para serviços (Infrastructure) ou
  para o core (Domain/Application).
- Regra prática: se você hesita em ler o arquivo inteiro de uma vez, ele já está
  grande demais — quebre antes de continuar.

## Projeto de referência

O backup em `P:\TCMine-Launcher-bk` contém a implementação completa (v1.2.0).
Use como referência de implementação, mas **reescreva de forma limpa** — não
copie e cole. Documentação do backup: `P:\TCMine-Launcher-bk\docs\`.

## Comandos

```bash
# Build da solução inteira (TCMine.slnx)
dotnet build

# Rodar o servidor (dev → https://localhost:7002 / http://localhost:5244)
cd TCMine-Server && dotnet run

# Rodar o launcher (desktop, Avalonia)
cd TCMine-Launcher && dotnet run

# Gerar ícones/assets (launcher + servidor)
cd TCMine-IconGenerator && dotnet run

# Docker (só servidor)
docker compose up --build

# Migrations EF Core (cada provider tem o seu conjunto)
dotnet ef migrations add <Nome> --project TCMine-Infrastructure --context SqliteAppDbContext
dotnet ef migrations add <Nome> --project TCMine-Infrastructure --context PostgresAppDbContext
```

### Configuração

- **Bootstrap do banco** (fora do banco): env vars `DB_PROVIDER`
  (`Sqlite`/`Postgres`) e `DB_CONNECTION`, ou a seção `Database` do
  `appsettings`. SQLite é o padrão. Ver `appsettings.local.json` (fora do git).
- **Secrets de runtime** (token CurseForge, Azure client/tenant id,
  `PublicBaseUrl`): configurados **pelo painel admin** e guardados no banco — o
  token CF fica **cifrado** via Data Protection. **Não** há mais
  `CF_API_KEY`/`ADMIN_PASSWORD` por env var.
- **Diretórios de dados**: centralizados em `ServerPaths` sob `tcmine-data/`
  (`updates`, `secrets`, `servers`, `modpacks`, `mods`) — criados no boot.

## Arquitetura

Solução **.NET 10** (`TCMine.slnx`) em **Clean Architecture**, 7 projetos. Cada
projeto ganhará uma página em `TCMine-Docs/wiki/entities/` conforme for
documentado — consulte a wiki para o detalhe vivo de cada um.

**Core (dependências apontando para dentro):**

- **TCMine-Domain** — entidades, enums e regras puras de domínio (sem EF/ASP.NET).
- **TCMine-Application** — portas (interfaces), contratos (DTOs `record`) e
  lógica pura de modpack (`CurseForgeImporter`, `ModSetMerge`).
- **TCMine-Infrastructure** — EF Core (SQLite/Postgres), CurseForge, filesystem,
  identidade e serviços de servidor/Minecraft.

**Entrega e suporte:**

- **TCMine-Design** — design system compartilhado (`ColorTokens`), fonte única
  de cor para CSS/Blazor, MudBlazor e Avalonia.
- **TCMine-Server** — ASP.NET Core (Minimal API + Blazor Server): backend do
  launcher + painel admin (MudBlazor). Proxy CurseForge `/v1/*`, manifestos,
  serving de jars, SSE `/events`, feed Velopack `/updates`.
- **TCMine-Launcher** — app Avalonia (WinExe), MVVM + ReactiveUI. "A Steam do
  TCMine".
- **TCMine-IconGenerator** — console SkiaSharp que gera ícones/favicon/og-image.

### Decisões arquiteturais chave (orientação; cada uma merece página em `wiki/decisions/`)

- **Central Package Management** — versões de NuGet centralizadas em
  `Directory.Packages.props`; props comuns em `Directory.Build.props`.
- **Lógica de domínio compartilhada no core** — filtro `ModSide`/`ModSideRules`,
  parse de loader, merge de mods e import do CurseForge vivem em
  Domain/Application; não duplicados entre servidor e launcher.
- **DTOs são `record` C#** imutáveis (`TCMine-Application/Contracts`).
- **Banco dual-provider** — `AppDbContext` abstrato; `SqliteAppDbContext` e
  `PostgresAppDbContext` concretos com migrations próprias; o DI resolve a
  concreta pelo provider configurado.
- **CurseForge sempre via proxy do servidor** (`/v1/*`); a `x-api-key` nunca sai
  do servidor.
- **Mods servidos pelo próprio servidor** — manifesto reescreve URLs para
  `/files/{fileId}/{fileName}`; launcher baixa do servidor (cache em
  `tcmine-data/mods`).
- **SSE para sync de conteúdo** — servidor empurra mudanças via `/events`.
- **Identidade por usuários + cookie** — sem usuário, `/setup` cria o `Owner`
  (papéis `Owner/Admin/Operator/Viewer`); senha em PBKDF2.
- **Configs do jogador** chaveadas por `(uuid, modpackId)`, sync em
  `/players/{uuid}/configs/{modpackId}`.
- **Velopack** para auto-update do launcher; servidor hospeda o feed como
  estáticos em `/updates`.

---

# Parte II — Base de conhecimento (`TCMine-Docs/`)

## Regra fundamental (não negociável)

- **Antes de qualquer implementação**, consulte a wiki (`TCMine-Docs/wiki/`,
  começando por `index.md`) para entender decisões e contexto já registrados
  sobre o componente/área em que vai mexer.
- **Após qualquer decisão de arquitetura, implementação relevante ou
  mudança de rumo**, escreva isso de volta na wiki (página de
  entity/concept/decision), atualize o `index.md` e adicione uma entrada no
  `log.md`.
- **A wiki é a memória de longo prazo deste projeto.** Toda memória, decisão,
  aprendizado ou nota relevante vai aqui — **não** no sistema de auto-memória do
  Claude (`~/.claude/.../memory/`). Preferência explícita do usuário.
- **Você é o único mantenedor da wiki.** O usuário não a edita à mão: ele **cura
  fontes** em `raw/`, **faz perguntas** e **direciona**. Você lê, processa e
  integra.

## 1. Modelo mental

Isto **não** é RAG tradicional (recuperar fragmentos na hora da pergunta). Você
**compila e mantém continuamente** uma wiki estruturada que fica entre o usuário
e as fontes brutas. Uma fonte nova nunca é só "indexada": você a lê, extrai o que
importa e a **tece** nas páginas existentes — atualizando entidades, revisando
resumos, sinalizando contradições, fortalecendo a síntese. A wiki é um artefato
**cumulativo**, não re-derivado do zero a cada pergunta.

## 2. Modelo de três camadas (separação rígida)

| Camada           | Caminho                    | Regra                                           |
|------------------|----------------------------|-------------------------------------------------|
| Fontes imutáveis | `raw/`                     | Read-only. **Nunca edite arquivos aqui.**       |
| A wiki           | `wiki/`                    | Você é o dono. O usuário lê; você escreve.      |
| O schema         | `CLAUDE.md` (este arquivo) | Convenções + workflows. Evolua deliberadamente. |

## 3. Estrutura de diretórios

```
TCMine-Docs/
├── raw/                  # fontes imutáveis (read-only)
│   └── assets/           # imagens, diagramas, screenshots referenciados pelas fontes
├── wiki/                 # a base de conhecimento (você escreve)
│   ├── index.md          # catálogo curado — TODA página é listada aqui
│   ├── log.md            # registro cronológico append-only (mais recente no topo)
│   ├── entities/         # componentes/projetos/artefatos concretos
│   ├── concepts/         # ideias, padrões e convenções transversais
│   ├── decisions/        # registros de decisão (ADR): contexto → decisão → consequências
│   ├── sources/          # uma página-resumo por fonte ingerida
│   └── templates/        # entity / concept / decision / source (starters)
└── tools/                # utilitários locais
    └── wikisearch.py     # busca BM25 (stdlib) sobre wiki/**/*.md — atalho do query workflow
```

Naming: arquivos de página em `kebab-case.md`. Páginas de fonte são prefixadas
com a data de ingestão: `sources/YYYY-MM-DD-<slug>.md`.

### Onde vai cada coisa (granularidade)

- **`entities/`** — uma coisa **concreta**: um projeto, componente, serviço ou
  artefato (ex.: `tcmine-server`, `tcmine-launcher`). Regra prática: **uma página
  por projeto/componente**.
- **`concepts/`** — uma **ideia transversal**, padrão ou convenção que cruza
  componentes (ex.: `design-tokens`, `clean-architecture`). Uma página por
  conceito durável.
- **`decisions/`** — uma **escolha de arquitetura com trade-offs**, no estilo ADR
  (contexto → decisão → consequências), **datada e versionável** (status
  `proposta`/`aceita`/`substituída`/`descontinuada`). Uma página por decisão.
  Decisões **referenciam** os `concepts`/`entities` que afetam; quando uma
  decisão substitui outra, use `supersedes`/`superseded-by`.
- **`sources/`** — resumo de **uma** fonte ingerida de `raw/` (ou de leitura de
  código). Aponta para as páginas que alimentou.
- **`raw/assets/`** — imagens e anexos (ver §6).

> Em dúvida sobre **onde** uma informação se encaixa, isso dispara um gatilho de
> confirmação (§5).

## 4. Regra de idioma (estrita)

- **Toda prosa** em páginas da wiki, resumos e no log: **PT-BR**.
- **Mantenha em inglês / as-is, nunca traduza:** termos técnicos, identificadores
  de código, nomes de função/variável/classe, caminhos de arquivo e nomes
  próprios — ex.: `TCMine-Server`, `ColorTokens.cs`, `RenderMode`, `Blazor`,
  `Avalonia`, "design tokens".
- Os nomes de **pastas** da wiki são em inglês de propósito (são caminhos):
  `entities/`, `concepts/`, `decisions/`, `sources/`.

## 5. Ingest workflow — DEFAULT: autônomo

O usuário aponta uma fonte nova em `raw/` (ou pede para documentar algo do código
vivo dos projetos). Por **padrão, você processa e integra de forma autônoma**:
lê, decide as páginas afetadas, escreve, atualiza `index.md` e `log.md`.

**Pare e peça confirmação ANTES de escrever quando (qualquer um dos casos):**

1. **Contradição** — a nova informação **contradiz** algo já registrado na wiki.
2. **Impacto amplo** — a decisão/mudança **afeta múltiplos componentes ou
   projetos** do TCMine.
3. **Categorização incerta** — você **não tem certeza** de como categorizar a
   informação (qual página/categoria) ou em qual página integrá-la.

Fora desses três casos, **siga em frente sem perguntar**.

Ao integrar (uma única fonte pode tocar **várias** páginas — isso é esperado e
correto):

- Crie/atualize as páginas de `entities/`, `concepts/` e/ou `decisions/`
  relevantes (a partir dos templates).
- Crie a página `sources/YYYY-MM-DD-<slug>.md`.
- Atualize `index.md` (liste toda página nova/alterada com resumo de uma linha).
- Adicione uma entrada **no topo** do `log.md` (formato §9).
- Adicione `[[wikilinks]]` nos **dois sentidos** (página nova ↔ páginas
  existentes).
- **Não commite** (§11).

## 6. Imagens e anexos

- Imagens/anexos referenciados por fontes vão em **`raw/assets/`**.
- Referencie nas páginas da wiki com **embed do Obsidian** e caminho relativo à
  raiz do vault: `![[raw/assets/nome-do-arquivo.png]]`. **Nunca** use URLs
  externas.
- **Lembrete operacional:** você (LLM) **não** lê markdown com imagens inline numa
  única passada. Primeiro leia o texto; depois, quando precisar do contexto
  visual, **abra a imagem separadamente** (tool de leitura de imagem) apontando
  para o arquivo em `raw/assets/`.

## 7. Health-check / lint automático (no boot da sessão)

No **início de toda sessão** de trabalho neste projeto, **antes** de qualquer
tarefa de implementação, rode um health-check **rápido** da wiki. **É automático
— não peça permissão, apenas faça**, como parte do boot.

Verifique:

- **páginas órfãs** (sem backlinks de entrada);
- **contradições** entre páginas;
- **claims desatualizados** que fontes mais recentes já substituíram;
- **conceitos** mencionados em várias páginas mas **sem página própria**;
- **referências cruzadas faltando**.

Mantenha **leve** (ler `index.md`, checar os links, `grep` no `log.md`). Para uma
wiki vazia, o check é no-op — registre mentalmente "wiki vazia" e siga.

- Registre achados relevantes no `log.md` (entrada tipo `lint`).
- Se encontrar algo que precise de **decisão do usuário**, **avise antes de
  prosseguir** com a tarefa principal.
- **Não** faça correções estruturais grandes silenciosamente: para qualquer
  coisa além do trivial, **proponha e espere o ok**; só então aplique e registre
  o `lint` no log.

## 8. Query workflow

> **Economia de tokens (regra geral):** ao **mexer na wiki** — responder, ingerir
> ou fazer o health-check — **prefira `tools/wikisearch.py`** para localizar as
> páginas relevantes em vez de ler `index.md` por inteiro ou abrir várias páginas
> "no escuro". A busca BM25 aponta os arquivos certos; só então abra os que
> importam. Vale para **todo** workflow desta Parte II, não só o query.

Ao responder perguntas contra a wiki:

1. **Localize.** Rode `python TCMine-Docs/tools/wikisearch.py "termos da pergunta"`
   (BM25 sobre `wiki/**/*.md`) para achar as páginas relevantes; caia no
   `index.md` (catálogo curado) quando a busca for ambígua. No Windows, use `py`
   se `python` não estiver disponível.
2. **Leia-as e sintetize** a resposta em **PT-BR**, com citações/links `[[...]]`
   de volta às páginas e às fontes em `raw/`.
3. **Ofereça persistir.** Se a resposta gerar conteúdo valioso (uma comparação,
   uma análise nova, uma síntese que vale guardar), **ofereça arquivá-la** de
   volta na wiki como nova página (tipicamente em `concepts/` ou uma síntese
   listada no `index.md`), em vez de deixá-la sumir no chat. Não arquive à força;
   ofereça.

## 9. `index.md` e `log.md`

- **`index.md`** — o **catálogo curado**. Seções: **Entidades / Conceitos /
  Decisões / Fontes / Sínteses**. Toda página aparece como
  `- [[path/slug]] — resumo de uma linha (tags)`. Mantenha atualizado a **cada
  escrita**. É o primeiro arquivo lido ao responder ou antes de implementar.
- **`log.md`** — o registro **append-only**, **mais recente no topo**. Cada
  entrada começa com um cabeçalho **parseável**:

  ```
  ## [YYYY-MM-DD] <tipo> | <Título>
  ```

  `<tipo>` ∈ `setup` | `ingest` | `decisao` | `lint` | `sintese` | `meta`.
  Corpo (PT-BR): **Fonte**, **Páginas afetadas** (`[[...]]`), **Resumo**,
  **Pendências**. Permite `grep "^## \[" log.md | tail -5` para ver as últimas
  entradas.

## 10. Obsidian e frontmatter

O usuário vê/edita a wiki no **Obsidian** (o vault é a pasta `TCMine-Docs/`).
Portanto:

- **Frontmatter YAML** em **toda** página (para queries Dataview no futuro).
  Chaves padrão: `type`, `title`, `tags`, `status`, `created`, `updated`; e por
  tipo: `aliases`/`sources`/`related` (entity, concept),
  `deciders`/`supersedes`/`superseded-by` (decision),
  `source-type`/`origin`/`feeds` (source). Datas em `YYYY-MM-DD`.
    - `status`: `stub` | `wip` | `stable` (entity/concept);
      `proposta` | `aceita` | `substituída` | `descontinuada` (decision);
      `ingested` (source).
- **Wikilinks** `[[path/slug]]` para todas as referências cruzadas (graph view +
  backlinks). Prefira linkar **por caminho**: `[[entities/tcmine-server]]`.
- **Imagens**: `![[raw/assets/...]]` (ver §6).

## 11. Regra de git (estrita)

`TCMine-Docs/` vive no **mesmo repositório git** da solução. Qualquer commit aqui
entra no histórico da solução.

- **Nunca commite automaticamente.** Faça as edições; deixe o commit para o
  usuário.
- **Não misture** mudanças de docs e de código no mesmo commit, salvo pedido
  explícito. Se tocou nos dois, aponte isso e deixe o usuário decidir.
- Esta regra vale para sessões futuras — não auto-commite "para ajudar".

## 12. Templates

Em `wiki/templates/`. **Copie e preencha** (não edite os templates no lugar):
`entity.md`, `concept.md`, `decision.md`, `source.md`. Sempre preencha
`created`/`updated`, as `tags`, e amarre `sources`/`related` com `[[wikilinks]]`.

## 13. Checklist por ingest

- [ ] Ler a fonte (ou o código vivo) por inteiro.
- [ ] Avaliar os gatilhos de confirmação (§5); se algum disparar, **perguntar
  antes de escrever**.
- [ ] Criar/atualizar páginas de `entities/`, `concepts/`, `decisions/`.
- [ ] Criar a página `sources/`.
- [ ] Adicionar `[[wikilinks]]` bidirecionais.
- [ ] Atualizar `index.md`.
- [ ] Inserir entrada no topo do `log.md` (cabeçalho parseável).
- [ ] Prosa em PT-BR; código/identificadores/paths em inglês.
- [ ] **Não commitar** — deixe o git para o usuário.
