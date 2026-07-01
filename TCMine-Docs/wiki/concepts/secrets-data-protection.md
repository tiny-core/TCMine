---
type: concept
title: Segredos cifrados via Data Protection
tags: [concept, segurança, secrets, data-protection]
status: stable
created: 2026-06-23
updated: 2026-06-23
aliases: [secrets, Data Protection, token cifrado, ServerSettingsService]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server-infrastructure]]"
  - "[[entities/tcmine-server]]"
  - "[[concepts/curseforge-proxy]]"
  - "[[concepts/setup-auth-cookie]]"
---

# Segredos cifrados via Data Protection

> Segredos de runtime (token CurseForge) ficam no banco **cifrados** via ASP.NET
> Data Protection; configurados pelo painel, **não** por variáveis de ambiente.

## O que é

`ServerSettingsService` ([[entities/tcmine-server-infrastructure]]) lê/grava a linha única
`ServerSettingEntity` (`Id == 1`). O token do CurseForge é protegido com um
`IDataProtector` (`protector "TCMine.ServerSettings.v1"`); os identificadores
Azure (`AzureClientId`/`AzureTenantId`) e `PublicBaseUrl` são públicos e ficam em
texto.

## Por que importa para o TCMine

O token CF nunca pode estar em texto puro no banco nem vazar para o cliente (casa
com [[concepts/curseforge-proxy]]). E configurá-lo pelo painel (em vez de env
vars) permite trocá-lo em runtime, sem reiniciar nem editar arquivos.

## Detalhes / Variações

- **Chaves de Data Protection** persistidas em `tcmine-data/secrets`
  (sobrevivem a restart/Docker); `SetApplicationName("TCMine-Server")` isola o
  escopo de proteção.
- **Cache quente:** o serviço é singleton com cache (o proxy CF lê a key a cada
  request); como o `AppDbContext` é scoped, abre um escopo curto via
  `IServiceScopeFactory` para tocar o banco. Evento `Changed` avisa a UI após
  gravar.
- **Sem fallback para env vars:** os valores vêm só do banco; antes de o Owner
  preencher, getters devolvem `null` → consumidores tratam como "não configurado".
- **Robustez:** se a chave de proteção foi rotacionada/perdida, `Unprotect`
  trata como vazio em vez de quebrar.

## Aplicação concreta

- `TCMine-Server.Infrastructure/Server/ServerSettingsService.cs`;
  `TCMine-Domain/Entities/ServerSettingEntity.cs`;
  `TCMine-Server/Program.cs` (`AddDataProtection().PersistKeysToFileSystem(...)`).

## Contradições / debates conhecidos

- Bootstrap do banco (provider/connection string) é a **exceção**: fica fora do
  banco (env `DB_PROVIDER`/`DB_CONNECTION` ou `appsettings.local.json`), porque é
  necessário *antes* de o banco existir.

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
