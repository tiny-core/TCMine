# tools/

Utilitários locais da base de conhecimento `TCMine-Docs`.

## `wikisearch.py`

Busca por palavra-chave (ranking **BM25**) sobre todos os arquivos `.md` em
`TCMine-Docs/wiki/`. Indexa em memória a cada execução — não há índice
persistente para manter em sincronia. Apenas **Python 3.9+ da stdlib**, sem
dependências externas.

### Setup

Nenhum. Basta ter Python 3 instalado:

```bash
python --version   # >= 3.9
```

(No Windows o comando pode ser `py` em vez de `python`.)

### Uso

```bash
# Busca simples (a partir de qualquer pasta)
python TCMine-Docs/tools/wikisearch.py "design tokens cor laranja"

# Limitar resultados
python TCMine-Docs/tools/wikisearch.py -n 5 "avalonia launcher"

# Filtrar por tag do frontmatter
python TCMine-Docs/tools/wikisearch.py --tag arquitetura "login"

# Saída JSON (para consumo pelo agente)
python TCMine-Docs/tools/wikisearch.py --json "blazor render mode"

# Listar todas as tags em uso
python TCMine-Docs/tools/wikisearch.py --list-tags
```

### Saída padrão

Uma linha por resultado, ordenada por score decrescente:

```
 12.43  wiki/entities/tcmine-server.md  —  TCMine-Server  [entity, blazor, arquitetura]
```

O caminho é relativo à raiz de `TCMine-Docs/`, então pode ser aberto direto.

### Como o agente usa

No **query workflow** (ver `../CLAUDE.md`), o agente roda este script para
localizar páginas relevantes em vez de ler `wiki/index.md` linearmente. O
`index.md` continua sendo o catálogo curado para humanos; o `wikisearch.py` é o
atalho de recuperação para o agente.

### Notas de implementação

- Título (`# H1`) e `tags` do frontmatter recebem peso extra no índice.
- O frontmatter YAML é lido por um parser mínimo embutido (sem PyYAML);
  suporta só `chave: valor`, listas inline `[a, b]` e listas em bloco `- item`.
- Tokenização cobre acentos do PT-BR.
