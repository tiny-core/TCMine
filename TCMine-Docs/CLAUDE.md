# CLAUDE.md — Constitution for the TCMine-Docs knowledge base

This file governs every session that works inside `TCMine-Docs/`. It is the
schema and the rulebook for a **persistent, LLM-maintained wiki**. Read it fully
before doing anything in this folder. It is meant to **evolve**: when you and the
user refine a convention, update this file (and log it).

> **Mental model.** This is *not* retrieval-of-fragments at query time. You
> **compile and continuously maintain** a structured wiki that sits between the
> user and the raw sources. A new source is never just "indexed" — you read it,
> extract what matters, and weave it into existing pages: updating entities,
> revising summaries, flagging contradictions, strengthening synthesis. The wiki
> is a compounding artifact, not re-derived from scratch on each question.

---

## 1. Scope

One knowledge base, two intertwined domains, cross-linked freely:

1. **TCMine ecosystem** — architecture, design decisions, and evolving state of
   the solution that lives **alongside this folder** at the solution root
   (`P:\TCMine\`):
   - `TCMine-Domain/`, `TCMine-Application/`, `TCMine-Infrastructure/` — the
     Clean Architecture "core" layers.
   - `TCMine-Design/` — shared design system (palette, design tokens; see
     `ColorTokens.cs`).
   - `TCMine-Server/` — Blazor (Server) app.
   - `TCMine-Launcher/` — Avalonia desktop app.
   - `TCMine-IconGenerator/` — icon/asset generation.
   - Any microservice projects added later at the solution root.
2. **External research** — articles, papers, and other material the user is
   studying, whether or not directly about TCMine.

Both domains share the same `raw/` and `wiki/`. Cross-link them: an architecture
decision page may cite the external article that motivated it.

---

## 2. Three-layer model (hard separation)

| Layer | Path | Rule |
|---|---|---|
| Immutable sources | `raw/` | Read-only. **Never edit files here.** |
| The wiki | `wiki/` | You own it entirely. The user reads; you write. |
| This constitution | `CLAUDE.md` | Conventions + workflows. Evolve deliberately. |

---

## 3. Directory conventions

```
TCMine-Docs/
├── CLAUDE.md              # this file
├── README.md             # cold-start orientation for humans/agents
├── raw/                  # immutable sources (read-only)
│   ├── articles/         # web clips (Obsidian Web Clipper): markdown + images
│   ├── code-refs/        # SHORT notes about live code you read (never full source)
│   ├── chats/            # Claude conversation exports / transcripts
│   └── assets/           # images, diagrams, mockups, screenshots
├── wiki/                 # the knowledge base (you write)
│   ├── index.md          # curated content catalog — every page is listed here
│   ├── log.md            # append-only chronological record (newest first)
│   ├── entities/         # concrete things (projects, components, artifacts)
│   ├── concepts/         # cross-cutting ideas, patterns, decisions
│   ├── sources/          # one page per ingested source (summary)
│   └── templates/        # entity.md, concept.md, source.md starter templates
└── tools/                # local tooling
    ├── wikisearch.py     # BM25 keyword search over wiki/*.md
    └── README.md
```

Naming: page files are `kebab-case.md`. Source pages are prefixed with the
ingest date: `sources/YYYY-MM-DD-<slug>.md`. `code-refs` notes:
`raw/code-refs/YYYY-MM-DD-<slug>.md`.

### Source types → where they go

- **Web articles/papers** (Obsidian Web Clipper, markdown + downloaded images) →
  `raw/articles/<slug>/` (keep the clipper's images alongside).
- **Live TCMine source code** → **read directly from the real project folders**
  at the solution root (`TCMine-Domain/`, `TCMine-Server/`, etc.). **Do NOT copy
  full source files into `raw/`.** Instead file a short note in `raw/code-refs/`
  capturing: which files/paths you read, the date, and a brief summary of what
  you learned — *not the code itself*. See §7.
- **Claude transcripts** (decisions, design discussions) → `raw/chats/`.
- **Images / diagrams** → `raw/assets/` (see §10 for referencing).

---

## 4. Language rule (strict)

- **All prose** in wiki pages, summaries, and the log: **PT-BR**.
- **Keep in English / as-is, never translate:** technical terms, code
  identifiers, function/variable/class names, file paths, and proper nouns —
  e.g. `TCMine-Core`, `TCMine-Server`, `ColorTokens.cs`, `RenderMode`,
  `Blazor`, `Avalonia`, "design tokens".
- This file (`CLAUDE.md`) and `tools/README.md` are written in English on
  purpose (operational docs for the agent); wiki *content* is PT-BR.

---

## 5. Git rule (strict)

`TCMine-Docs/` lives in the **same git repository** as the whole TCMine
solution. Any commit here is a commit to the solution's history.

- **Never commit automatically.** Make file edits; leave committing to the user.
- You may `git add`/stage changes if it helps, but stop before `git commit`.
- **Do not mix** docs changes and code changes in one commit unless the user
  explicitly asks. If you touched both, point it out and let the user decide.
- This rule binds future sessions too — do not auto-commit "to be helpful".

---

## 6. Ingest workflow — DEFAULT: one source at a time, discuss first

This is the most important convention. The default is **supervised**: never
silently batch-process. The loop:

1. **Point.** The user points you at a new item in `raw/`, or asks you to
   document something from live code in `TCMine-Domain/`, `TCMine-Application/`,
   `TCMine-Infrastructure/`, `TCMine-Design/`, `TCMine-Server/`,
   `TCMine-Launcher/`, `TCMine-IconGenerator/`, or a microservice folder (read
   those directly; never copy full files into `raw/` — see §7).
2. **Read & report.** Read it and report back a **short summary**: what you
   found, and what you think should change in the wiki — which pages would be
   created/updated, which cross-links you'd add, any contradictions with prior
   claims.
3. **Wait.** Wait for the user's go-ahead or corrections **before writing to
   `wiki/`**.
4. **Write.** Once confirmed:
   - Create/update the relevant `entities/` and `concepts/` pages.
   - Create the `sources/YYYY-MM-DD-<slug>.md` summary page (from the `source`
     template).
   - Update `wiki/index.md` (list every new/changed page with a one-line
     summary).
   - Prepend an entry to `wiki/log.md` (see §11 for the header format).
   - Add `[[wikilinks]]` both ways (new page ↔ existing pages).
5. **Don't commit.** Per §5, leave git to the user.

### `batch ingest` mode (opt-in)

The user can explicitly request **batch ingest** for less-supervised processing
of multiple sources. Then:
- Process the named set in one pass, applying the same page/index/log updates.
- Still **do not commit**.
- End with a **consolidated report**: every page touched, every new cross-link,
  and a flagged list of contradictions/uncertainties for review.
- Batch mode changes *supervision*, not the rules. Default remains one-at-a-time.

---

## 7. Live-code-reading convention

When you read TCMine source to understand or document something:

- Read it **live** from the real project folder — do not copy source into `raw/`.
- File a short note `raw/code-refs/YYYY-MM-DD-<slug>.md` with:
  - **Files/paths read** (e.g. `TCMine-Design/ColorTokens.cs`).
  - **Date** of reading.
  - **Brief summary** of what you learned — the takeaways, not the code.
- The note in `code-refs/` is a *source*; the understanding goes into the
  relevant `entities/` / `concepts/` pages, with the `code-refs` note cited as
  the source (and optionally a matching `sources/` page if it's substantial).
- Code moves; re-verify before trusting an old `code-refs` note. If reality has
  diverged, update the note (append, dated) and the pages it fed.

---

## 8. Query workflow

When the user asks a question against the wiki:

1. **Locate.** Run the search tool to find relevant pages:
   `python tools/wikisearch.py "termos da pergunta"` (see §12). Fall back to
   `wiki/index.md` for a curated overview when search is ambiguous.
2. **Read & synthesize.** Read the relevant pages; compose an answer **in
   PT-BR** with **citations/links** back to wiki pages (`[[...]]`) and to the
   original sources in `raw/`.
3. **Offer to persist.** If the answer is substantial (a comparison, an
   analysis, a synthesis worth keeping), **offer** to file it back into the wiki
   as a new page (under `wiki/` — typically a derived/synthesis page listed in
   `index.md`) instead of letting it vanish into chat. Don't auto-file; ask.
4. **Output shape.** Support, *only when actually useful for the question*:
   - a wiki page, a **comparison table**, a **Marp slide deck**, or a **chart**.
   - Do not produce these extra formats by default — plain answer first.

---

## 9. Lint workflow (`lint`)

A periodic, user-invoked audit. Scan the wiki for:

- **Contradictions** between pages.
- **Stale claims** superseded by newer sources.
- **Orphan pages** with no inbound `[[wikilinks]]`.
- **Concepts mentioned repeatedly** but lacking their own page.
- **Missing cross-references** (pages that should link but don't).
- **Data gaps** that a web search could fill.

Output is a **report for the user to review** — *not* automatic silent fixes.
Only after the user approves do you apply changes (then update `index.md` and
log a `lint` entry). Structure the report by category above, each finding with
the page(s) involved and a proposed action.

---

## 10. Obsidian compatibility

The user views/edits this wiki in Obsidian (vault already at `.obsidian/`, with
self-hosted sync). Therefore:

- **Frontmatter (YAML)** on every wiki page, so Dataview-style queries work
  later. Standard keys: `type`, `title`, `tags`, `status`, `created`,
  `updated`, `sources`, `related` (see templates for the full set per type).
  Dates are `YYYY-MM-DD`.
- **Wikilinks** `[[path/slug]]` for all cross-references, so graph view and
  backlinks work. Prefer linking by path (e.g. `[[entities/tcmine-server]]`).
- **Images**: reference local files under `raw/assets/` (or the article's own
  folder under `raw/articles/<slug>/`), **never external URLs**. Example:
  `![[raw/assets/dashboard-mockup.png]]`.

---

## 11. `index.md` and `log.md` formats

- **`wiki/index.md`** — the curated **content catalog**. Sectioned by Entidades /
  Conceitos / Fontes / Sínteses. Every page must appear as
  `- [[path/slug]] — one-line summary (tags)`. Keep it current on every write.
- **`wiki/log.md`** — the append-only **chronological record**, newest first.
  Each entry starts with a parseable header:

  ```
  ## [YYYY-MM-DD] <tipo> | <Title>
  ```

  where `<tipo>` ∈ `ingest` | `batch-ingest` | `lint` | `synthesis` | `meta`.
  Body (PT-BR): **Fonte**, **Páginas afetadas** (`[[...]]`), **Resumo**,
  **Pendências**.

---

## 12. Search tool

`tools/wikisearch.py` — stdlib-only Python 3, BM25 keyword search over
`wiki/**/*.md`. No embeddings, no external services, no persistent index (it
reindexes each run). This is the agent's retrieval shortcut for the query
workflow; `index.md` remains the human-curated catalog.

```bash
python tools/wikisearch.py "design tokens cor laranja"   # basic
python tools/wikisearch.py -n 5 "avalonia launcher"      # limit results
python tools/wikisearch.py --tag arquitetura "login"     # filter by tag
python tools/wikisearch.py --json "blazor render mode"   # machine-readable
python tools/wikisearch.py --list-tags                   # inventory tags
```

Output: `score  wiki/<path>.md  —  Title  [tags]`. Title (`# H1`) and frontmatter
`tags` are weighted higher in the index. See `tools/README.md`. On Windows, use
`py` if `python` is unavailable.

---

## 13. Templates

Starter files live in `wiki/templates/`. Copy and fill (don't edit the templates
in place):

- `templates/entity.md` → `wiki/entities/<slug>.md` — projects, components,
  concrete artifacts.
- `templates/concept.md` → `wiki/concepts/<slug>.md` — cross-cutting ideas,
  patterns, decisions.
- `templates/source.md` → `wiki/sources/YYYY-MM-DD-<slug>.md` — one per ingested
  source.

Always set `created`/`updated`, fill `tags`, and wire `sources`/`related`/
`feeds` with `[[wikilinks]]`.

---

## 14. Operating checklist (per ingest)

- [ ] Read the source (or live code) fully.
- [ ] Report summary + proposed wiki changes; **wait for go-ahead** (unless
      `batch ingest`).
- [ ] Create/update `entities/` & `concepts/` pages (from templates).
- [ ] Create the `sources/` page (and `raw/code-refs/` note if it was live code).
- [ ] Add two-way `[[wikilinks]]`.
- [ ] Update `wiki/index.md`.
- [ ] Prepend a `wiki/log.md` entry with the parseable header.
- [ ] Prose in PT-BR; code/identifiers/paths in English.
- [ ] **Do not commit** — leave git to the user.
