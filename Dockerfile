# Imagem do TCMine Server.
#
# Duas coisas neste arquivo não são convenção e sim necessidade, e estão
# comentadas onde aparecem: o publish é do projeto Web sozinho (as suítes de
# teste não vão para a imagem) e o container precisa alcançar o socket do
# Docker do host — ver docker-compose.yml, que é onde isso se resolve.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Versão desta build. O handshake a devolve ao launcher, e sem ela toda imagem
# publicada se anunciaria como 1.0.0 — o padrão do SDK quando nada é informado.
# O workflow de release passa o número vindo da tag do git.
ARG VERSION=0.0.0-dev

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

# Sem --no-restore de propósito, embora o restore já tenha rodado acima.
#
# Restaurar apenas com os .csproj presentes e publicar com --no-restore
# produz um manifesto de static web assets INCOMPLETO: falta a entrada de
# _framework/blazor.web.js. O App.razor a resolve por @Assets[...], que
# devolve o caminho literal quando a entrada não existe — e aí o navegador
# leva 404, o Blazor interativo nunca inicia, e nenhum diálogo do painel
# abre. A página renderiza mesmo assim, porque é SSR, então tudo parece bem.
#
# O restore desta linha é barato: os pacotes já estão no cache do NuGet da
# camada acima. O que ele refaz é a resolução COM o código presente, que é
# justamente o que faltava.
RUN dotnet publish src/server/TCMine.Server.Web/TCMine.Server.Web.csproj \
    -c Release -o /app \
    -p:Version=${VERSION} \
    -p:InformationalVersion=${VERSION}

# O launcher do MESMO commit, compilado para Windows a partir do Linux.
#
# Vai dentro da imagem do servidor para os dois saírem juntos: um servidor que
# exige launcher novo não tem como ser publicado sem ele. NÃO é empacotado aqui
# (vpk pack): o instalador leva o endereço público da instalação, e esse só o
# container conhece — o empacotamento acontece no arranque (LauncherBundlePublisher).
#
# Estágio separado do build do servidor: o publish self-contained do launcher
# passa de 180 MB e não pode atrasar nem sujar o do servidor.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS launcher
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/ src/

# EnableWindowsTargeting deixa o SDK do Linux compilar um TFM Windows (WPF +
# WebView2). A versão vem de src/launcher/VERSION, pelo Directory.Build.props
# do launcher — nenhum número é passado aqui.
RUN dotnet publish src/launcher/TCMine.Launcher.App/TCMine.Launcher.App.csproj \
        -c Release -r win-x64 --self-contained true \
        -p:EnableWindowsTargeting=true \
        -o /launcher/app \
    && cp src/launcher/VERSION /launcher/VERSION

# O vpk na MESMA versão do pacote Velopack que o launcher referencia: um vpk de
# outra versão pode gravar um formato que a biblioteca embutida não entende, e
# o sintoma só aparece no autoupdate.
RUN VPK_VERSION=$(sed -n 's/.*PackageVersion Include="Velopack" Version="\([^"]*\)".*/\1/p' Directory.Packages.props) \
    && test -n "$VPK_VERSION" \
    && dotnet tool install --tool-path /launcher/vpk vpk --version "$VPK_VERSION" \
    && cd /launcher/vpk/.store/vpk/$VPK_VERSION/vpk/$VPK_VERSION \
    && test -d tools/net10.0 \
    && find tools -mindepth 1 -maxdepth 1 ! -name net10.0 -exec rm -rf {} + \
    && rm -f *.nupkg

# A poda acima não é cosmética: o pacote do vpk traz um build para cada .NET
# (8, 9, 10) e duas cópias de si mesmo em .nupkg — 650 MB, dos quais só o build
# do runtime desta imagem e o vendor/ (de onde sai o Setup.exe) são usados.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl para o health check do compose; zstd é o que o vpk usa para gerar os
# pacotes delta (a atualização pequena) do launcher. A imagem aspnet não traz
# nada além do runtime, de propósito — cada pacote a mais é superfície a manter.
RUN apt-get update \
    && apt-get install --no-install-recommends -y curl zstd \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# Somente leitura para o processo: o empacotamento copia o app para um
# temporário antes de acrescentar o server.json desta instalação.
# /usr/lib e não /opt/tcmine: o DEPLOY.md monta a raiz de dados do host em
# /opt/tcmine, e o volume esconderia o launcher da imagem sem erro nenhum.
COPY --from=launcher /launcher /usr/lib/tcmine/launcher

# Roda como o usuário não-root que a imagem já traz. Note que ele precisa
# pertencer ao grupo dono de /var/run/docker.sock para orquestrar containers —
# o compose resolve isso com group_add, e é a única razão de o assunto aparecer
# aqui.
USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "TCMine.Server.Web.dll"]
