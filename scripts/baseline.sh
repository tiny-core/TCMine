#!/usr/bin/env bash
#
# Medições da linha de base (docs/BASELINE.md). Não altera dados: só reinicia o
# container do TCMine (boot) e lê o banco em modo somente leitura.
#
#   ./scripts/baseline.sh boot [url]        3 boots, tempo até /health/live
#   ./scripts/baseline.sh mem               memória e CPU do container em repouso
#   ./scripts/baseline.sh db-sqlite <arq>   tamanho e linhas por tabela (SQLite)
#   ./scripts/baseline.sh db-postgres       imprime a consulta para rodar no psql
#
set -euo pipefail

cd "$(dirname "$0")/.."

SERVICO="tcmine"

agora_ms() { date +%s%3N; }

boot() {
  local url="${1:-http://localhost:${TCMINE_PORT:-8080}}"

  for rodada in 1 2 3; do
    # stop + start, e não restart: o restart conta o tempo de PARAR dentro do
    # número, e o que interessa é só o arranque.
    docker compose stop "$SERVICO" >/dev/null
    local inicio; inicio=$(agora_ms)
    docker compose start "$SERVICO" >/dev/null

    # /health/live e não /health: o live responde assim que o Kestrel escuta;
    # o /health só passa com o banco pronto e mediria outra coisa.
    until curl -fs -o /dev/null --max-time 1 "${url}/health/live"; do
      sleep 0.05
    done

    echo "boot ${rodada}: $(( $(agora_ms) - inicio )) ms"
  done
}

mem() {
  docker stats --no-stream --format \
    'memória: {{.MemUsage}}  cpu: {{.CPUPerc}}' \
    "$(docker compose ps -q "$SERVICO")"
}

db_sqlite() {
  local arquivo="${1:?informe o caminho do tcmine.db}"

  echo "tamanho (db + wal):"
  du -ch "$arquivo" "$arquivo-wal" 2>/dev/null | tail -1

  # mode=ro: garante que a medição não escreve no banco em uso.
  local uri="file:${arquivo}?mode=ro"

  sqlite3 "$uri" "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;" |
    while read -r tabela; do
      printf '%-32s %s\n' "$tabela" "$(sqlite3 "$uri" "SELECT count(*) FROM \"$tabela\";")"
    done
}

db_postgres() {
  # count(*) exato por tabela (n_live_tup é só estimativa) e tamanho com índices.
  cat <<'SQL'
SELECT relname AS tabela,
       (xpath('/row/c/text()',
              query_to_xml(format('select count(*) as c from %I.%I', schemaname, relname),
                           false, true, '')))[1]::text::bigint AS linhas,
       pg_size_pretty(pg_total_relation_size(relid)) AS tamanho
FROM pg_stat_user_tables
ORDER BY 2 DESC;

SELECT pg_size_pretty(pg_database_size(current_database())) AS banco;
SQL
}

case "${1:-}" in
  boot)        boot "${2:-}" ;;
  mem)         mem ;;
  db-sqlite)   db_sqlite "${2:-}" ;;
  db-postgres) db_postgres ;;
  *)           sed -n '2,11p' "$0"; exit 1 ;;
esac
