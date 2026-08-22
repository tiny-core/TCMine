# Imagem do TCMine Server.
#
# Duas coisas neste arquivo não são convenção e sim necessidade, e estão
# comentadas onde aparecem: o publish é do projeto Web sozinho (as suítes de
# teste não vão para a imagem) e o container precisa alcançar o socket do
# Docker do host — ver docker-compose.yml, que é onde isso se resolve.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Manifestos primeiro, código depois: o restore só refaz quando uma dependência
# muda, e não a cada linha editada. Sem isto toda build baixa o mundo de novo.
# O global.json fica DE FORA de propósito. Ele existe para alinhar a versão do
# SDK entre as máquinas do time; dentro da imagem quem escolhe o SDK é a tag
# acima. Copiá-lo faria a build quebrar sempre que a máquina de quem
# desenvolve estivesse um patch à frente da imagem oficial — que foi
# exatamente o que aconteceu (302 na máquina, 301 na imagem).
COPY Directory.Build.props Directory.Packages.props ./
COPY src/server/TCMine.Server.Domain/*.csproj src/server/TCMine.Server.Domain/
COPY src/server/TCMine.Server.Application/*.csproj src/server/TCMine.Server.Application/
COPY src/server/TCMine.Server.Infrastructure/*.csproj src/server/TCMine.Server.Infrastructure/
COPY src/server/TCMine.Server.Infrastructure.Sqlite/*.csproj src/server/TCMine.Server.Infrastructure.Sqlite/
COPY src/server/TCMine.Server.Infrastructure.Postgres/*.csproj src/server/TCMine.Server.Infrastructure.Postgres/
COPY src/server/TCMine.Server.Web/*.csproj src/server/TCMine.Server.Web/
COPY src/shared/TCMine.Contracts/*.csproj src/shared/TCMine.Contracts/
COPY src/shared/TCMine.UI.Shared/*.csproj src/shared/TCMine.UI.Shared/

RUN dotnet restore src/server/TCMine.Server.Web/TCMine.Server.Web.csproj

COPY src/ src/

RUN dotnet publish src/server/TCMine.Server.Web/TCMine.Server.Web.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl para o health check do compose. A imagem aspnet não traz nada além do
# runtime, de propósito — cada pacote a mais é superfície a manter.
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# Roda como o usuário não-root que a imagem já traz. Note que ele precisa
# pertencer ao grupo dono de /var/run/docker.sock para orquestrar containers —
# o compose resolve isso com group_add, e é a única razão de o assunto aparecer
# aqui.
USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "TCMine.Server.Web.dll"]
