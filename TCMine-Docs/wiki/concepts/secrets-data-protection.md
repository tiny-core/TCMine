---
type: concept
title: Secrets cifrados via Data Protection
tags: [concept, seguranca, secrets, data-protection, settings]
status: wip
created: 2026-06-22
updated: 2026-06-22
aliases: [secrets, Data Protection, settings de runtime, CF token cifrado]
sources:
  - "[[sources/2026-06-22-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[entities/tcmine-infrastructure]]"
  - "[[entities/tcmine-domain]]"
  - "[[concepts/curseforge-proxy]]"
---

# Secrets cifrados via Data Protection

> Os segredos de runtime (token CurseForge) ficam **cifrados em repouso** no banco
> via ASP.NET Data Protection; identificadores públicos (Azure ids, base URL) em texto.

## O que é

`ServerSettingEntity` é a **linha única** (Id==1) de settings de runtime, editável
pelo painel admin. O `CfApiKeyEncrypted` é cifrado; `AzureClientId`/`AzureTenantId`/
`PublicBaseUrl` são identificadores públicos em texto. `ServerSettingsService`
(singleton com cache) cifra/decifra com Data Protection e serve leituras quentes.

## Por que importa para o TCMine

- A key do CurseForge é necessária ao proxy ([[concepts/curseforge-proxy]]) a cada
  requisição — daí o cache — mas **nunca deve estar em texto** no banco.
- As chaves de Data Protection ficam em **disco** (`tcmine-data/secrets`), com
  `SetApplicationName("TCMine-Server")`, para sobreviver a restart e ao Docker
  (imune a corrupção de env vars pelo Compose).
- Decisão de projeto: secrets configurados **pelo painel**, não por env var; sem
  fallback para ambiente. Antes de o Owner preencher, getters devolvem null
  ("não configurado").

## Detalhes / Variações

- Protector nomeado `TCMine.ServerSettings.v1`.
- Como o `AppDbContext` é scoped e o serviço é singleton, abre escopo curto via
  `IServiceScopeFactory`.
- `GetStoredAsync` (valores decifrados) alimenta o formulário admin.

## Aplicação concreta

- `TCMine-Infrastructure/Server/ServerSettingsService.cs`,
  `Program.cs` (`AddDataProtection().PersistKeysToFileSystem(...)`);
  `ServerSettingEntity` em [[entities/tcmine-domain]].

## Contradições / debates conhecidos

- (nenhum até agora)

## Referências

- [[sources/2026-06-22-leitura-codigo-vivo]]
