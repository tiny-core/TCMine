# TCMine-Docs

Base de conhecimento viva (wiki LLM-mantido) do projeto **TCMine**.

Esta pasta vive **dentro da solução TCMine**, como irmã de `TCMine-Core`
(camadas `TCMine-Domain` / `TCMine-Application` / `TCMine-Infrastructure`),
`TCMine-Design`, `TCMine-Server`, `TCMine-Launcher` e `TCMine-IconGenerator`,
e compartilha o **mesmo repositório git** — não é um repo nem submódulo
separado.

## O que é

Diferente de um RAG tradicional, aqui um agente (Claude) **compila e mantém
continuamente** um wiki estruturado que fica entre você e as fontes brutas. Ao
adicionar uma fonte nova, o agente a lê, extrai o que importa e tece isso no
wiki existente — atualizando páginas, revisando resumos e sinalizando
contradições. O wiki é um artefato que enriquece com o tempo, não algo
re-derivado a cada pergunta.

## Três camadas

| Camada | Pasta | Quem escreve |
|---|---|---|
| Fontes imutáveis | `raw/` | você (clipper, exports, imagens) — o agente só lê |
| Conhecimento | `wiki/` | o agente (resumos, entidades, conceitos, índice, log) |
| Constituição | `CLAUDE.md` | agente + você, evoluindo as convenções |

## Por onde começar

- **Agentes:** leia [`CLAUDE.md`](./CLAUDE.md) primeiro — ele define estrutura,
  workflows (ingest / query / lint), regra de idioma e regra de git.
- **Humanos:** abra esta pasta no Obsidian (vault já configurado em
  `.obsidian/`). Comece por [`wiki/index.md`](./wiki/index.md) e
  [`wiki/log.md`](./wiki/log.md).
- **Busca:** `python tools/wikisearch.py "seus termos"` (ver
  [`tools/README.md`](./tools/README.md)).

## Idioma

Prosa em **PT-BR**; identificadores de código, caminhos e nomes próprios
(ex.: `TCMine-Server`, `ColorTokens.cs`) em **inglês/as-is**.

## Git

`TCMine-Docs/` está no repositório da solução. **Commits não são automáticos** —
ver `CLAUDE.md`. Evite misturar mudanças de docs com mudanças de código no
mesmo commit, salvo pedido explícito.
