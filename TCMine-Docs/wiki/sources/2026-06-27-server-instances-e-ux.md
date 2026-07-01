---
type: source
title: Implementação de instâncias de servidor + remodelagem da UX admin
tags: [source, code, server-instance, docker, ux]
status: ingested
created: 2026-06-27
updated: 2026-06-27
source-type: code
origin: TCMine-Infrastructure/ServerInstances/, TCMine-Server/Components/Pages/Admin/ (código vivo)
feeds: [[[decisions/server-instances-docker]], [[concepts/server-instance-lifecycle]], [[concepts/modpack-server-hub-ux]]]
related: [[[entities/tcmine-server-infrastructure]], [[entities/tcmine-server]], [[entities/tcmine-domain]]]
---

# Implementação de instâncias de servidor + remodelagem da UX admin

> Sessão de implementação que adicionou os servidores Minecraft gerenciados (Docker) e remodelou a UX
> de modpacks/servidores no painel.

## Resumo

Construção, em fases, do recurso de **instâncias de servidor** (provisionamento com cache de loader,
execução em container Docker via DooD, console/logs, reconciliação de status, ping de jogadores) e da
**remodelagem da UX** do painel (hub do modpack, páginas/modais no lugar de abas, ligação
modpack↔servidor, sincronização de desatualização). Inclui correções: timeout do client Docker, bind
via `Mount` no Windows, reuso da imagem do release com JRE 25, resolução de dependências de mod.

## Pontos-chave

- **Domínio**: `ServerInstanceEntity` (ContainerId, ImageTag, PublicAddress, Advertise, ProvisionedAt,
  XmsMb/ExtraJvmArgs), `ServerRuntimeCacheEntity` (cache de loader), `ServerEntryEntity.ServerInstanceId`
  (entrada auto-divulgada). Migrations Sqlite+Postgres.
- **Infra** (`ServerInstances/`): `ServerProvisioner`, `ServerRuntimeInstaller`, `DockerEnvironment`,
  `DockerServerJavaRunner`, `DockerMinecraftManager`, `ServerStatusReconciler`, `MinecraftServerPinger`,
  `ServerInstanceService`, `ILinkStrategy` (symlink/cópia), `ServerConfigWriter`.
- **DooD**: socket do host montado; tradução de path (`DataHostRoot`); `Mount` em vez de `Binds`;
  imagem do release com JRE embutido; timeout infinito do client.
- **UX**: `ModpackHub`, `OverridesPage`, editor-de-mods, diálogos (Details/News/Connections/VersionPicker);
  Monaco para editar configs; console com auto-scroll; selos de desatualização + "aplicar atualização".
- **Mods**: aviso de incompatibilidade MC/loader na adição; resolução transitiva de dependências
  obrigatórias; troca de versão lazy.
- **Build/run**: Dockerfile passa a copiar `./publish` (build no host); script `build-image.ps1` + run
  configs do Rider.

## O que alimentou na wiki

- [[decisions/server-instances-docker]] — a decisão de arquitetura (DooD + reuso de imagem).
- [[concepts/server-instance-lifecycle]] — provisionar → executar → reconciliar → medir.
- [[concepts/modpack-server-hub-ux]] — hub + páginas/modais + sincronização.

## Referências

- Código: `TCMine-Infrastructure/ServerInstances/`, `TCMine-Server/Components/Pages/Admin/`.
