#!/usr/bin/env python3
"""wikisearch.py — busca local por palavra-chave (BM25) sobre o wiki em markdown.

Ferramenta deliberadamente simples: indexa em memória todos os ``.md`` em
``TCMine-Docs/wiki/`` a cada execução (escala bem até alguns milhares de
páginas) e ordena por BM25. Sem dependências externas — apenas a stdlib do
Python 3.

Uso:
    python wikisearch.py "design tokens cor laranja"
    python wikisearch.py --tag arquitetura "login"
    python wikisearch.py --json "avalonia launcher" -n 5
    python wikisearch.py --list-tags

Saída (padrão): uma linha por resultado:
    <score>  <caminho-relativo>  —  <título>  [tags]

O agente normalmente invoca este script no início do query workflow em vez de
ler ``wiki/index.md`` linearmente. Veja o CLAUDE.md na raiz do repositório.
"""
from __future__ import annotations

import argparse
import json
import math
import re
import sys
from collections import Counter
from pathlib import Path

WIKI_DIR = (Path(__file__).resolve().parent.parent / "wiki").resolve()

# Parâmetros BM25 padrão.
K1 = 1.5
B = 0.75

_TOKEN_RE = re.compile(r"[a-z0-9_áàâãéêíóôõúç]+", re.IGNORECASE)
_FRONTMATTER_RE = re.compile(r"^---\s*\n(.*?)\n---\s*\n", re.DOTALL)
_H1_RE = re.compile(r"^#\s+(.+)$", re.MULTILINE)


def tokenize(text: str) -> list[str]:
    return [t.lower() for t in _TOKEN_RE.findall(text)]


def parse_frontmatter(text: str) -> tuple[dict, str]:
    """Extrai um frontmatter YAML simples (sem dependência de PyYAML).

    Suporta apenas o subconjunto que usamos: ``chave: valor`` e listas inline
    ``chave: [a, b]`` ou listas em bloco com ``- item``. Suficiente para tags.
    """
    m = _FRONTMATTER_RE.match(text)
    if not m:
        return {}, text
    body = text[m.end():]
    meta: dict[str, object] = {}
    current_key: str | None = None
    for line in m.group(1).splitlines():
        if not line.strip():
            continue
        if re.match(r"^\s*-\s+", line) and current_key:
            meta.setdefault(current_key, [])
            val = line.split("-", 1)[1].strip().strip("'\"")
            if isinstance(meta[current_key], list):
                meta[current_key].append(val)  # type: ignore[union-attr]
            continue
        if ":" in line:
            key, _, val = line.partition(":")
            key = key.strip()
            val = val.strip()
            current_key = key
            if val.startswith("[") and val.endswith("]"):
                meta[key] = [v.strip().strip("'\"") for v in val[1:-1].split(",") if v.strip()]
            elif val:
                meta[key] = val.strip("'\"")
            else:
                meta[key] = []
    return meta, body


class Doc:
    __slots__ = ("path", "title", "tags", "tokens", "tf", "length")

    def __init__(self, path: Path, raw: str):
        self.path = path
        meta, body = parse_frontmatter(raw)
        tags = meta.get("tags", [])
        self.tags = [tags] if isinstance(tags, str) else list(tags or [])
        h1 = _H1_RE.search(body)
        self.title = (h1.group(1).strip() if h1 else path.stem)
        # Título e tags pesam mais: repetimos no corpo de indexação.
        indexed = " ".join([self.title, self.title, " ".join(self.tags), body])
        self.tokens = tokenize(indexed)
        self.tf = Counter(self.tokens)
        self.length = len(self.tokens)


def load_docs(wiki_dir: Path) -> list[Doc]:
    docs: list[Doc] = []
    for p in sorted(wiki_dir.rglob("*.md")):
        if p.name.startswith("."):
            continue
        try:
            docs.append(Doc(p, p.read_text(encoding="utf-8")))
        except (OSError, UnicodeDecodeError) as exc:
            print(f"warn: skipping {p}: {exc}", file=sys.stderr)
    return docs


def bm25_search(docs: list[Doc], query: str, tag: str | None) -> list[tuple[float, Doc]]:
    if tag:
        docs = [d for d in docs if tag in d.tags]
    if not docs:
        return []
    q_terms = tokenize(query)
    N = len(docs)
    avgdl = sum(d.length for d in docs) / N
    df = Counter()
    for term in set(q_terms):
        df[term] = sum(1 for d in docs if d.tf.get(term))

    scored: list[tuple[float, Doc]] = []
    for d in docs:
        score = 0.0
        for term in q_terms:
            f = d.tf.get(term, 0)
            if not f:
                continue
            idf = math.log(1 + (N - df[term] + 0.5) / (df[term] + 0.5))
            denom = f + K1 * (1 - B + B * d.length / avgdl)
            score += idf * (f * (K1 + 1)) / denom
        if score > 0:
            scored.append((score, d))
    scored.sort(key=lambda x: x[0], reverse=True)
    return scored


def main(argv: list[str] | None = None) -> int:
    # No Windows o stdout pode não ser UTF-8 (manglaria acentos PT-BR e "—"). Força UTF-8
    # quando possível — best-effort, pois nem todo stream suporta reconfigure.
    try:
        sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[union-attr]
    except (AttributeError, ValueError):
        pass

    parser = argparse.ArgumentParser(description="Busca BM25 local sobre o wiki TCMine-Docs.")
    parser.add_argument("query", nargs="*", help="termos de busca")
    parser.add_argument("-n", "--limit", type=int, default=10, help="número máximo de resultados")
    parser.add_argument("--tag", help="filtra por uma tag exata no frontmatter")
    parser.add_argument("--json", action="store_true", help="saída em JSON")
    parser.add_argument("--list-tags", action="store_true", help="lista todas as tags e sai")
    parser.add_argument("--wiki", type=Path, default=WIKI_DIR, help="caminho do diretório wiki/")
    args = parser.parse_args(argv)

    wiki_dir = args.wiki.resolve()
    if not wiki_dir.is_dir():
        print(f"erro: diretório wiki não encontrado: {wiki_dir}", file=sys.stderr)
        return 2

    docs = load_docs(wiki_dir)

    if args.list_tags:
        tags = Counter(t for d in docs for t in d.tags)
        for tag, count in sorted(tags.items(), key=lambda x: (-x[1], x[0])):
            print(f"{count:4d}  {tag}")
        return 0

    if not args.query:
        parser.error("informe ao menos um termo de busca (ou use --list-tags)")

    query = " ".join(args.query)
    results = bm25_search(docs, query, args.tag)[: args.limit]

    if args.json:
        out = [
            {
                "score": round(score, 4),
                "path": str(d.path.relative_to(wiki_dir.parent).as_posix()),
                "title": d.title,
                "tags": d.tags,
            }
            for score, d in results
        ]
        print(json.dumps(out, ensure_ascii=False, indent=2))
        return 0

    if not results:
        print("(sem resultados)")
        return 0

    for score, d in results:
        rel = d.path.relative_to(wiki_dir.parent).as_posix()
        tags = f"  [{', '.join(d.tags)}]" if d.tags else ""
        print(f"{score:6.2f}  {rel}  —  {d.title}{tags}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
