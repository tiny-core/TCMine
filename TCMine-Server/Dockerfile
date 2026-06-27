# Imagem de runtime com a app JÁ COMPILADA (publicada no host/CI) — sem SDK nem build no container.
# Mais rápido de montar; o custo é um passo de publish antes do build.
#
# Publique ANTES (framework-dependent / portável — roda no runtime Linux da imagem mesmo a partir do
# Windows; NÃO use -r):
#   dotnet publish TCMine-Server/TCMine-Server.csproj -c Release -o publish -p:UseAppHost=false
# Depois:
#   docker compose up --build      (ou  docker build -t tcmine-server -f TCMine-Server/Dockerfile .)
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# JRE embutido: a MESMA imagem roda as instâncias de servidor Minecraft (Docker-out-of-Docker
# reutiliza-a — sem imagem custom separada). O entrypoint Java é definido por container na criação.
ENV JAVA_HOME=/opt/java/openjdk
ENV PATH="${JAVA_HOME}/bin:${PATH}"
COPY --from=eclipse-temurin:25-jre $JAVA_HOME $JAVA_HOME

USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Saída do publish (gerada no host) — nada de restore/build aqui dentro
COPY publish/ .
ENTRYPOINT ["dotnet", "TCMine-Server.dll"]
