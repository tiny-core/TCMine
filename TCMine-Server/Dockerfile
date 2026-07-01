# Imagem AUTOSSUFICIENTE do TCMine-Server: além de rodar o servidor, ela COMPILA o launcher em runtime
# (o painel /admin/releases dispara `dotnet publish` + `vpk` para gerar o feed Velopack). Por isso a
# imagem final é baseada no SDK (não no runtime) e carrega a fonte do launcher + o vpk + o JRE.
#
# Build (contexto = raiz da solução):
#   docker build -t tcmine-server -f TCMine-Server/Dockerfile .
# Ou via compose:  docker compose up --build

# ── Stage 1: build do servidor ─────────────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# Framework-dependent (o runtime vem do SDK da imagem final). SERVER_VERSION vira a versão do assembly.
ARG SERVER_VERSION=0.0.0
RUN dotnet publish TCMine-Server/TCMine-Server.csproj -c Release -o /out \
        -p:UseAppHost=false -p:Version=${SERVER_VERSION}

# ── Stage final: SDK + launcher + vpk + JRE ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Versão embutida (a mesma tag define server e launcher). O server a lê de SERVER_VERSION (ou cai no
# assembly) para o self-update e para compilar o launcher na mesma versão.
ARG SERVER_VERSION=0.0.0
ENV SERVER_VERSION=${SERVER_VERSION}

# Roda como root: o build do launcher em runtime escreve obj/bin na fonte e usa o vpk do global-tools.
# É um container de gestão (já faz Docker-out-of-Docker), então root é aceitável aqui.

# JRE embutido: a MESMA imagem roda as instâncias de servidor Minecraft (Docker-out-of-Docker reutiliza-a)
ENV JAVA_HOME=/opt/java/openjdk
ENV PATH="${JAVA_HOME}/bin:${PATH}"
COPY --from=eclipse-temurin:25-jre $JAVA_HOME $JAVA_HOME

# Velopack CLI (vpk) como ferramenta global — usada pelo LauncherBuildService para empacotar o launcher
ENV PATH="${PATH}:/root/.dotnet/tools"
RUN dotnet tool install -g vpk

# Fonte do launcher (só os projetos necessários) para compilar em runtime. A URL/ClientId NÃO vêm daqui:
# são injetados por build (-p) a partir das settings do painel; Client.props fica de fora (.dockerignore).
WORKDIR /src
COPY Directory.Packages.props Directory.Build.props ./
COPY TCMine-Domain/ ./TCMine-Domain/
COPY TCMine-Application/ ./TCMine-Application/
COPY TCMine-Design/ ./TCMine-Design/
COPY TCMine-Launcher.Infrastructure/ ./TCMine-Launcher.Infrastructure/
COPY TCMine-Launcher/ ./TCMine-Launcher/
# Pré-restaura os pacotes do launcher (win-x64) → o 1º build no painel é rápido e não depende de rede
RUN dotnet restore TCMine-Launcher/TCMine-Launcher.csproj -r win-x64

# App do servidor (publicado no stage 1)
WORKDIR /app
COPY --from=build /out .

# O content root do app é /app; aponta o build do launcher para a fonte copiada em /src
ENV LauncherBuild__ProjectPath=/src/TCMine-Launcher/TCMine-Launcher.csproj

EXPOSE 8080
EXPOSE 8081
ENTRYPOINT ["dotnet", "TCMine-Server.dll"]
