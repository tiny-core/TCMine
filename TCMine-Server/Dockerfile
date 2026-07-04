# Imagem do TCMine-Server. Além de rodar o servidor, ela COMPILA o launcher em runtime (o painel
# /admin/releases dispara `dotnet publish` + `vpk`). Por isso é baseada no SDK (não no runtime) e carrega
# o vpk + o JRE. A FONTE do launcher NÃO é embutida — o servidor a baixa do GitHub na tag `launcher-v*`
# (assim uma release só do launcher não exige reconstruir/reiniciar esta imagem).
#
# Build (contexto = raiz da solução):
#   docker build -t tcmine-server -f TCMine-Server/Dockerfile . --build-arg SERVER_VERSION=1.2.0

# ── Stage 1: build do servidor ─────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
ARG SERVER_VERSION=0.0.0
RUN dotnet publish TCMine-Server/TCMine-Server.csproj -c Release -o /out \
        -p:UseAppHost=false -p:Version=${SERVER_VERSION}

# ── Stage final: SDK + vpk + JRE ────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Versão embutida (faixa server-v*). O server a lê de SERVER_VERSION (ou do assembly) para o self-update.
ARG SERVER_VERSION=0.0.0
ENV SERVER_VERSION=${SERVER_VERSION}

# Roda como root: o build do launcher em runtime escreve na área de staging e usa o vpk (global tool).
# É um container de gestão (já faz Docker-out-of-Docker), então root é aceitável aqui.

# JRE embutido: a MESMA imagem roda as instâncias de servidor Minecraft (Docker-out-of-Docker reutiliza-a)
ENV JAVA_HOME=/opt/java/openjdk
ENV PATH="${JAVA_HOME}/bin:${PATH}"
COPY --from=eclipse-temurin:25-jre $JAVA_HOME $JAVA_HOME

# Velopack CLI (vpk) como ferramenta global — usada pelo LauncherBuildService para empacotar o launcher
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install -g vpk

# App do servidor (publicado no stage 1)
WORKDIR /app
COPY --from=build /out .

EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "TCMine-Server.dll"]
