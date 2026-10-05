#!/usr/bin/env bash
# Confere se o launcher mudou desde a última release do servidor sem que
# src/launcher/VERSION tenha subido.
#
# Por que existe: o launcher viaja DENTRO da imagem do servidor e é publicado no
# feed a cada arranque. O Velopack só oferece atualização a quem tem versão
# MENOR — então um launcher alterado com o mesmo número chega aos instaladores
# novos e nunca aos jogadores que já o têm. Esquecer o número é a falha
# silenciosa que este script torna barulhenta.
#
# Uso:
#   scripts/check-launcher-version.sh            # falha (exit 1) se esqueceu
#   scripts/check-launcher-version.sh --warn     # só avisa (CI de PR/master)
#
# Exige o histórico e as tags (checkout com fetch-depth: 0).
set -euo pipefail

MODO="${1:-}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

VERSAO_ARQ="src/launcher/VERSION"

# Tudo o que entra no binário do launcher: o código dele, o que é partilhado com
# o servidor (Contracts, UI.Shared, MinecraftAuth) e as versões de pacote e de
# build. Mudança de comentário também conta — o custo de subir um patch é
# nenhum, e distinguir "só comentário" aqui seria apostar contra o compilador.
CAMINHOS=(src/launcher src/shared Directory.Packages.props Directory.Build.props)

# A release anterior é a tag server-v* mais próxima ANTES deste commit. Com
# HEAD^ e não HEAD: no job de release o próprio HEAD já carrega a tag nova.
anterior="$(git describe --tags --abbrev=0 --match 'server-v*' HEAD^ 2>/dev/null || true)"
if [ -z "$anterior" ]; then
  echo "Nenhuma release server-v* anterior: nada a comparar."
  exit 0
fi

atual="$(tr -d '[:space:]' < "$VERSAO_ARQ")"
antes="$(git show "${anterior}:${VERSAO_ARQ}" 2>/dev/null | tr -d '[:space:]' || true)"
if [ -z "$antes" ]; then
  echo "${anterior} é anterior a ${VERSAO_ARQ}: nada a comparar (launcher ${atual})."
  exit 0
fi

mudou="$(git diff --name-only "$anterior" HEAD -- "${CAMINHOS[@]}" ":(exclude)${VERSAO_ARQ}")"
if [ -z "$mudou" ]; then
  echo "Launcher sem mudanças desde ${anterior} (versão ${atual})."
  exit 0
fi

# Maior, e não só diferente: um número que DESCE não chega a ninguém (o
# servidor mantém o mais novo do feed, e o Velopack não rebaixa).
maior="$(printf '%s\n%s\n' "$antes" "$atual" | sort -V | tail -1)"
if [ "$atual" != "$antes" ] && [ "$maior" = "$atual" ]; then
  echo "Launcher ${antes} → ${atual} desde ${anterior}."
  exit 0
fi

msg="O launcher mudou desde ${anterior}, mas ${VERSAO_ARQ} continua em ${atual} (era ${antes}). Suba o número, senão os jogadores que já o instalaram nunca recebem a atualização."
echo "Arquivos alterados:"
echo "$mudou" | sed 's/^/  /' | head -30

if [ "$MODO" = "--warn" ]; then
  # Anotação do GitHub: aparece no resumo do PR sem pintá-lo de vermelho — o
  # número pode subir em qualquer commit até a release.
  echo "::warning file=${VERSAO_ARQ}::${msg}"
  exit 0
fi

echo "::error file=${VERSAO_ARQ}::${msg}"
exit 1
