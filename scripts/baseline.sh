#!/usr/bin/env bash
#
# Medições da linha de base (docs/BASELINE.md), feitas do HOST pelo nome do
# container — nada roda dentro dele, e o repositório não precisa estar na
# máquina. Não altera dados: só reinicia o container do TCMine (boot) e lê o
# banco em modo somente leitura.
#
#   ./scripts/baseline.sh boot  <container> [url]   3 boots, tempo até /health/live
#   ./scripts/baseline.sh mem   <container>         memória e CPU em repouso
#   ./scripts/baseline.sh pages <container> [desde] linhas "Abertura de página" (padrão: 30m)
#   ./scripts/baseline.sh db-postgres <container-do-postgres> [usuário] [banco]
#   ./scripts/baseline.sh db-sqlite   <caminho do tcmine.db>
#
# O nome do container sai de: docker ps --format '{{.Names}}'
#
set -euo pipefail

agora_ms() { date +%s%3N; }

exigir() { [ -n "${1:-}" ] || { echo "erro: informe $2" >&2; exit 1; }; }

boot() {
  exigir "${1:-}" "o nome do container"
  local container="$1" url="${2:-http://localhost:8080}"

  for rodada in 1 2 3; do
    # stop + start, e não restart: o restart conta o tempo de PARAR dentro do
    # número, e o que interessa é só o arranque.
    docker stop "$container" >/dev/null
    local inicio; inicio=$(agora_ms)
    docker start "$container" >/dev/null

    # /health/live e não /health: o live responde assim que o Kestrel escuta;
    # o /health só passa com o banco pronto e mediria outra coisa.
    until curl -fs -o /dev/null --max-time 1 "${url}/health/live"; do
      sleep 0.05
    done

    echo "boot ${rodada}: $(( $(agora_ms) - inicio )) ms"
  done
}

mem() {
  exigir "${1:-}" "o nome do container"
  docker stats --no-stream --format 'memória: {{.MemUsage}}  cpu: {{.CPUPerc}}' "$1"
}

pages() {
  exigir "${1:-}" "o nome do container"
  docker logs --since "${2:-30m}" "$1" 2>&1 | grep "Abertura de p" || echo "nenhuma abertura no período"
}

db_postgres() {
  exigir "${1:-}" "o nome do container do Postgres"
  # count(*) exato por tabela (n_live_tup é só estimativa) e tamanho com índices.
  docker exec -i "$1" psql -U "${2:-tcmine}" -d "${3:-tcmine}" <<'SQL'
SELECT pg_size_pretty(pg_database_size(current_database())) AS banco;

SELECT relname AS tabela,
       (xpath('/row/c/text()',
              query_to_xml(format('select count(*) as c from %I.%I', schemaname, relname),
                           false, true, '')))[1]::text::bigint AS linhas,
       pg_size_pretty(pg_total_relation_size(relid)) AS tamanho
FROM pg_stat_user_tables
ORDER BY 2 DESC;

SELECT m."Name" AS modpack,
       count(DISTINCT v."Id") AS versoes,
       count(f."Id") AS arquivos
FROM modpacks m
JOIN modpack_versions v ON v."ModpackId" = m."Id"
LEFT JOIN modpack_files f ON f."ModpackVersionId" = v."Id"
GROUP BY 1
ORDER BY 3 DESC;
SQL
}

db_sqlite() {
  exigir "${1:-}" "o caminho do tcmine.db"
  local arquivo="$1"

  echo "tamanho (db + wal):"
  # O -wal só existe com o banco aberto; sem o "|| true" a falta dele derrubava
  # o script (set -e + pipefail) antes de listar as tabelas.
  { du -ch "$arquivo" "$arquivo-wal" 2>/dev/null || true; } | tail -1

  # Python e não sqlite3: já vem no Ubuntu, e a imagem do servidor não traz
  # nenhum dos dois. mode=ro garante que a medição não escreve no banco em uso.
  python3 - "$arquivo" <<'PY'
import sqlite3, sys
con = sqlite3.connect("file:" + sys.argv[1] + "?mode=ro", uri=True)
tables = [r[0] for r in con.execute(
    "select name from sqlite_master where type='table' and name not like 'sqlite_%' order by name")]
for t in tables:
    print("%-32s %d" % (t, con.execute('select count(*) from "%s"' % t).fetchone()[0]))
PY
}

case "${1:-}" in
  boot)        boot "${2:-}" "${3:-}" ;;
  mem)         mem "${2:-}" ;;
  pages)       pages "${2:-}" "${3:-}" ;;
  db-postgres) db_postgres "${2:-}" "${3:-}" "${4:-}" ;;
  db-sqlite)   db_sqlite "${2:-}" ;;
  *)           sed -n '2,15p' "$0"; exit 1 ;;
esac
