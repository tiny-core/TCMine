#!/usr/bin/env bash
#
# Fumaça da IMAGEM publicável: sobe o container de verdade, contra um PostgreSQL
# de verdade, e confere o que só quebra ali.
#
# Existe porque a suíte e o `dotnet run` são mais permissivos que a imagem, e
# quatro bugs seguidos só apareceram depois do deploy. O caro deles foi este:
# a página abria, mas o `blazor.web.js` respondia 404 e nenhum diálogo funcionava
# — o app publicado servia estáticos de um manifesto que a build tinha produzido
# vazio. Nada no loop local exercia esse caminho.
#
#   ./scripts/smoke-image.sh                  constrói a imagem e testa
#   ./scripts/smoke-image.sh usuário/img:tag  testa uma imagem já existente
#
set -euo pipefail

IMAGEM="${1:-}"
SUFIXO="smoke-$$"
REDE="tcmine-${SUFIXO}"
PG="pg-${SUFIXO}"
APP="app-${SUFIXO}"
PORTA=""
SENHA_PG="smoke"

cd "$(dirname "$0")/.."

vermelho() { printf '\033[31m%s\033[0m\n' "$*"; }
verde()    { printf '\033[32m%s\033[0m\n' "$*"; }

falhas=0
afirmar() {
  local descricao="$1"; shift
  if "$@" >/dev/null 2>&1; then
    verde "  ok   ${descricao}"
  else
    vermelho "  FALHA ${descricao}"
    falhas=$((falhas + 1))
  fi
}

limpar() {
  # Sempre: um container órfão segura a porta e a próxima execução falha por um
  # motivo que não tem nada a ver com o código.
  docker logs "$APP" >/tmp/${APP}.log 2>&1 || true
  docker rm -f "$APP" "$PG" >/dev/null 2>&1 || true
  docker network rm "$REDE" >/dev/null 2>&1 || true
  docker volume rm "dados-${SUFIXO}" >/dev/null 2>&1 || true
}
trap limpar EXIT

if [ -z "$IMAGEM" ]; then
  IMAGEM="tcmine-server:${SUFIXO}"
  echo "Construindo ${IMAGEM}..."
  docker build --quiet --build-arg VERSION=0.0.0-smoke -t "$IMAGEM" . >/dev/null
fi

echo "Imagem: ${IMAGEM}"

docker network create "$REDE" >/dev/null

docker run -d --name "$PG" --network "$REDE" \
  -e POSTGRES_USER=tcmine -e POSTGRES_PASSWORD="$SENHA_PG" -e POSTGRES_DB=tcmine \
  postgres:17-alpine >/dev/null

esperar_postgres() {
  for _ in $(seq 1 30); do
    docker exec "$PG" pg_isready -U tcmine >/dev/null 2>&1 && return 0
    sleep 2
  done
  return 1
}
esperar_postgres || { vermelho "PostgreSQL não subiu."; exit 1; }

# Volume nomeado em vez de bind mount: o teste é da aplicação, não do layout de
# montagem do operador, e um bind mount exigiria que caminho do host e caminho
# do daemon coincidissem — o que não vale no Docker Desktop.
docker volume create "dados-${SUFIXO}" >/dev/null

# O volume nasce do root e o container roda como 1654; sem isto ele morre no
# arranque sem conseguir criar a própria pasta de dados. É o mesmo `chown` que
# o docs/DEPLOY.md pede ao operador, e o smoke não deve ser mais fácil que a
# instalação real.
docker run --rm -v "dados-${SUFIXO}:/dados" alpine:3 chown 1654:1654 /dados >/dev/null

docker run -d --name "$APP" --network "$REDE" \
  -p 0:8080 \
  -v "dados-${SUFIXO}:/dados" \
  -e ASPNETCORE_URLS=http://+:8080 \
  -e Server__PublicUrl=http://localhost:8080   -e Server__AzureClientId=00000000-0000-0000-0000-000000000000 \
  -e Storage__RootPath=/dados \
  -e Storage__SkipMountCheck=true \
  -e Database__Provider=Postgres \
  -e Database__Host="$PG" \
  -e Database__Name=tcmine \
  -e Database__Username=tcmine \
  -e Database__Password="$SENHA_PG" \
  "$IMAGEM" >/dev/null

PORTA="$(docker port "$APP" 8080/tcp | head -1 | sed 's/.*://')"
BASE="http://localhost:${PORTA}"

# Um proxy reverso terminando TLS é requisito de produção: sem
# X-Forwarded-Proto o cookie de sessão não pode ser emitido e toda página
# responde 500. O smoke se comporta como esse proxy.
CABECALHO=(-H "X-Forwarded-Proto: https")

echo "Esperando o arranque em ${BASE}..."
pronto=0
for _ in $(seq 1 45); do
  if curl -fsS "${BASE}/health/live" >/dev/null 2>&1; then pronto=1; break; fi
  sleep 2
done
[ "$pronto" = 1 ] || { vermelho "A aplicação não respondeu."; docker logs "$APP" 2>&1 | tail -30; exit 1; }

echo
echo "Verificando:"

# /health/ready toca o banco: só passa se as migrations tiverem rodado. É o que
# separa "o processo subiu" de "a instalação funciona".
afirmar "/health/ready responde (migrations aplicadas)" \
  bash -c "for _ in \$(seq 1 30); do curl -fsS '${BASE}/health/ready' >/dev/null 2>&1 && exit 0; sleep 2; done; exit 1"

html="$(curl -fsS "${CABECALHO[@]}" "${BASE}/setup" 2>/dev/null || true)"

afirmar "/setup renderiza" bash -c "[ -n '${html:0:1}' ]"

# O ponto do teste. A página pré-renderiza no servidor mesmo com o script
# quebrado, então "abriu" não prova nada: o que prova é o navegador conseguir
# buscar o runtime do Blazor. Sem ele nada interativo funciona — nenhum diálogo
# abre — e a página continua parecendo saudável.
script="$(printf '%s' "$html" | grep -o '_framework/blazor[^"]*\.js' | head -1)"

afirmar "a página referencia o runtime do Blazor" bash -c "[ -n '${script}' ]"

if [ -n "$script" ]; then
  cabecalhos="$(curl -fsS -D - -o /dev/null "${CABECALHO[@]}" "${BASE}/${script}" 2>/dev/null || true)"
  afirmar "${script} é servido" bash -c "printf '%s' \"\$1\" | head -1 | grep -q ' 200'" _ "$cabecalhos"
  afirmar "${script} vem como JavaScript" \
    bash -c "printf '%s' \"\$1\" | grep -qi 'content-type:.*javascript'" _ "$cabecalhos"
fi

# As migrations que a IMAGEM carrega, e não as do repositório: é a diferença
# entre "o fix está no código" e "o fix está no que foi publicado".
# Os nomes de TABELA são snake_case, mas os de COLUNA saíram PascalCase — a
# convenção do projeto só chegou até as tabelas. Consultado como está no banco,
# não como deveria estar.
largura() {
  docker exec "$PG" psql -U tcmine -d tcmine -tAc     "select coalesce(character_maximum_length::text, 'ilimitado')
     from information_schema.columns
     where table_name = '$1' and column_name = '$2'" 2>/dev/null | tr -d '[:space:]'
}

largura_slug="$(largura modpack_files ProjectSlug)"
largura_path="$(largura modpack_files Path)"

# O slug de um override é o caminho MAIS um prefixo, então tem de ser maior. Os
# dois tinham o mesmo limite, e um caminho no tamanho máximo gerava um slug que
# não cabia — por aritmética, não por azar. Aqui isso é verificado no banco que
# a imagem migrou.
afirmar "ProjectSlug (${largura_slug:-?}) cabe um Path (${largura_path:-?}) inteiro"   bash -c '[ -n "$1" ] && [ -n "$2" ] && [ "$1" -gt "$2" ]' _ "$largura_slug" "$largura_path"

# O snapshot da origem guarda um par projeto/arquivo e o nome de cada mod, então
# cresce com o pack: qualquer número aqui é o número errado. A configuração
# dizia "sem limite" e a coluna saía varchar(512) assim mesmo — um Property()
# sem nada configurado não desfaz a convenção global. Um pack de trezentos mods
# batia nisso e a importação morria.
snapshot_col="$(largura modpack_versions UpstreamSnapshotJson)"

afirmar "UpstreamSnapshotJson é ilimitado (${snapshot_col:-?})"   bash -c '[ "$1" = "ilimitado" ]' _ "$snapshot_col"

echo
if [ "$falhas" -eq 0 ]; then
  verde "SMOKE OK"
else
  vermelho "SMOKE FALHOU (${falhas})"
  echo "Log do container:"
  docker logs "$APP" 2>&1 | tail -40
  exit 1
fi
