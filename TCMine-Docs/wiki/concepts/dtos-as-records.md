---
type: concept
title: DTOs são records imutáveis
tags: [concept, convenção, dtos, contratos]
status: stable
created: 2026-06-23
updated: 2026-06-23
aliases: [DTOs as records, record DTOs, contratos imutáveis]
sources:
  - "[[sources/2026-06-23-leitura-codigo-vivo]]"
related:
  - "[[entities/tcmine-application]]"
  - "[[concepts/clean-architecture]]"
---

# DTOs são records imutáveis

> Convenção: os DTOs de fio (contratos entre camadas e com o launcher) são `record`
> C# imutáveis, vivendo em `TCMine-Application/Contracts`. **Nunca** classes mutáveis.

## O que é

Os contratos da [[entities/tcmine-application]] são declarados como `record`
(posicional, imutável). Exemplos verificados: `ModDto`, `ModpackManifestDto`,
`ImportedModpackDto`/`ImportedModDto`, `MergeResultDto<T>`, `ModpackAdminRowDto`,
`SaveProgressDto`, `OverrideFileDto`, `DraftImportDto<T>`, `VersionOptionDto`.

## Por que importa para o TCMine

- **Imutabilidade** evita estado compartilhado mutável atravessando camadas/rede.
- **Semântica de valor** (igualdade estrutural) facilita comparação e testes.
- **Concisão** — o contrato é a assinatura; menos boilerplate.

## Detalhes / Variações

- Distinguir do **modelo de persistência**: as `Entities/` do
  [[entities/tcmine-domain]] são classes mutáveis (POCOs EF). Os `record` são os
  **DTOs** de transporte, não as entidades.
- DTOs do formulário admin (ex.: `SettingsForm`) podem ser classes mutáveis
  locais à UI — a convenção é sobre os **contratos de fio**, não sobre view-models
  internos.

## Aplicação concreta

- `TCMine-Application/Contracts/*.cs` (ex.: `Modpack.cs`).

## Contradições / debates conhecidos

- Nenhuma conhecida. É uma regra estável do projeto; mudar um DTO para classe
  seria uma regressão a sinalizar.

## Referências

- [[sources/2026-06-23-leitura-codigo-vivo]]
