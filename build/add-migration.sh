#!/usr/bin/env bash
# Cria a mesma migration nos dois projetos de provider.
# É fácil esquecer um dos dois, e aí o schema diverge silenciosamente.
set -euo pipefail

if [ $# -ne 1 ]; then
  echo "Uso: ./build/add-migration.sh <NomeDaMigration>" >&2
  exit 1
fi

NOME="$1"

for PROVIDER in Postgres Sqlite; do
  PROJETO="src/server/TCMine.Server.Infrastructure.${PROVIDER}"
  echo "==> ${PROVIDER}"
  dotnet ef migrations add "$NOME" \
    --project "$PROJETO" \
    --startup-project "$PROJETO" \
    --context TcMineDbContext
done
