---
type: log
title: Log do Wiki TCMine-Docs
tags: [log]
updated: 2026-06-22
---

# Log

Registro cronológico append-only de tudo que acontece nesta base de
conhecimento: ingestões, lints, sínteses arquivadas e mudanças estruturais.
**Entradas novas vão no topo** (mais recente primeiro).

Cada entrada começa com um cabeçalho parseável:

```
## [YYYY-MM-DD] <tipo> | <Título>
```

`<tipo>` ∈ `ingest` | `batch-ingest` | `lint` | `synthesis` | `meta`.

Estrutura sugerida do corpo:

- **Fonte:** caminho em `raw/` ou path de código vivo lido.
- **Páginas afetadas:** `[[entities/...]]`, `[[concepts/...]]`, `[[sources/...]]`.
- **Resumo:** o que mudou e por quê (PT-BR).
- **Pendências:** o que ficou em aberto.

---

## [2026-06-22] batch-ingest | Leitura inicial da solução TCMine (código vivo)

- **Fonte:** leitura de código vivo de toda a solução em `P:\TCMine\` (branch
  `master`); nota em `raw/code-refs/2026-06-22-leitura-inicial-solucao.md`;
  resumo em [[sources/2026-06-22-leitura-codigo-vivo]].
- **Páginas afetadas:** criadas as entidades [[entities/tcmine-solution]],
  [[entities/tcmine-domain]], [[entities/tcmine-application]],
  [[entities/tcmine-infrastructure]], [[entities/tcmine-design]],
  [[entities/tcmine-server]], [[entities/tcmine-launcher]],
  [[entities/tcmine-icongenerator]]; os conceitos
  [[concepts/clean-architecture]], [[concepts/design-tokens]],
  [[concepts/modside-rules]], [[concepts/modpack-mods-locais]],
  [[concepts/curseforge-proxy]], [[concepts/secrets-data-protection]],
  [[concepts/setup-auth-cookie]], [[concepts/persistence-dual-provider]],
  [[concepts/player-config-sync]]; `index.md` atualizado.
- **Resumo:** primeira ingestão de conteúdo do wiki. A pedido do usuário ("pode
  preencher com o que já temos"), processada como batch-ingest da solução
  existente. Semeadas uma página por projeto + os conceitos transversais que o
  código já consolida (lógica de modpack compartilhada no core, mods servidos
  pelo servidor, proxy CurseForge, secrets cifrados, setup/auth por cookie,
  banco dual-provider, sync de configs). Sem contradições (base estava vazia).
- **Pendências:**
  - Áreas ainda só esboçadas no código: orquestração de instâncias de servidor
    Minecraft, build do launcher + feed Velopack, UI do launcher (Avalonia).
  - Páginas em `status: stub/wip` — aprofundar conforme o código evoluir.
  - Arquivo solto `TCMine-Docs/entities/....md` (vazio, fora de `wiki/`) — provável
    criação acidental; sugiro remover.
  - Nada commitado (regra §5).

## [2026-06-22] meta | Bootstrap da base de conhecimento

- **Páginas afetadas:** estrutura inicial (`raw/`, `wiki/`, `tools/`), `CLAUDE.md`,
  `README.md`, templates (`entity`, `concept`, `source`), `index.md`, `log.md`,
  `tools/wikisearch.py`.
- **Resumo:** Criada a estrutura do sistema de wiki LLM-mantido dentro de
  `TCMine-Docs/`, dentro do mesmo repositório git da solução TCMine. Definidas
  as convenções de ingest (uma fonte por vez, com discussão antes de escrever),
  query e lint; regra de idioma PT-BR para prosa e inglês/as-is para código;
  regra de não commitar automaticamente; leitura de código vivo via notas em
  `raw/code-refs/`.
- **Pendências:** primeira ingestão real de conteúdo (artigo, código ou chat).
