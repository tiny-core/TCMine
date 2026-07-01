---
type: decision
title: Instâncias de servidor Minecraft via Docker-out-of-Docker
tags: [decision, server-instance, docker, minecraft]
status: aceita
created: 2026-06-27
updated: 2026-06-27
deciders: [Jocian]
supersedes: []
superseded-by: []
sources: [[[sources/2026-06-27-server-instances-e-ux]]]
related: [[[entities/tcmine-server-infrastructure]], [[entities/tcmine-server]], [[concepts/server-instance-lifecycle]]]
---

# Instâncias de servidor Minecraft via Docker-out-of-Docker

> Cada servidor Minecraft gerenciado roda num **container Docker dedicado**, criado pelo
> TCMine-Server através do socket do daemon do host (DooD); o TCMine controla todo o ciclo de vida.

## Contexto

O TCMine precisa subir servidores Minecraft a partir dos modpacks cadastrados, com **controle total**
(download do loader, montagem dos mods, geração de configs) e isolamento entre instâncias — inclusive
para rodar packs grandes (500+ mods). As opções consideradas foram: processo Java filho no mesmo
host/container; um container por instância via Docker; imagens de comunidade (ex.: `itzg/minecraft-server`).

## Decisão

- **Um container Docker por instância**, criado e controlado pelo TCMine-Server via **Docker-out-of-
  Docker**: o socket do daemon do host (`/var/run/docker.sock`) é montado no container do TCMine, e a
  orquestração usa o pacote **Docker.DotNet**.
- **Controle total**, sem imagens de comunidade: o TCMine baixa o instalador do loader (NeoForge, do
  Maven oficial), roda `--installServer` num container Java efêmero, monta mods e gera as configs.
- **Reuso da imagem do release**: em vez de uma imagem custom só-Java, a **própria imagem do
  TCMine-Server** passa a embutir um JRE e roda também as instâncias. Garante que a imagem está sempre
  presente no host (o DooD não precisa baixar/construir outra). Em dev (sem container), o default é a
  imagem pública `eclipse-temurin:25-jre` (pull automático). O `Entrypoint` é sobreposto para `java`.
- **Java 25** (NeoForge/MC atuais exigem class file 69.0). Configurável por `ServerInstances:Image`.

## Consequências

- **Isolamento** real por instância; uma instância travada não afeta o painel.
- **Tradução de caminho (DooD)**: o daemon do host monta os bind-mounts, então o caminho precisa ser o
  do **host**, não o de dentro do container do TCMine — resolvido por `ServerInstances:DataHostRoot`.
  Em Windows/Docker Desktop usa-se `Mount` (objeto `Source`/`Target`) em vez de `Binds` (string com
  `:`) por causa do `:` do drive (`P:\…`).
- **Acesso ao socket**: o processo precisa de permissão (rodar como root ou no grupo docker).
- **Um Java major por imagem**: caveat aceito; `ServerInstances:Image` permite apontar outra imagem se
  um pack exigir outro Java.
- **Timeout**: o client Docker usa timeout infinito (o `WaitContainerAsync` do install demora minutos).

## Alternativas consideradas

- **Processo Java no host/container do TCMine** — mais simples, mas sem isolamento e mistura runtimes.
- **`itzg/minecraft-server`** — pronto, mas tira o controle total (download/instalação do loader).
- **Imagem custom só-Java** — descartada em favor do reuso da imagem do release (uma imagem só).

## Referências

- Implementação em [[entities/tcmine-server-infrastructure]] (`ServerInstances/`); ver [[concepts/server-instance-lifecycle]].
- Fonte: [[sources/2026-06-27-server-instances-e-ux]].
