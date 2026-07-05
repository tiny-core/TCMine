---
type: concept
title: Security headers (CSP + anti-clickjacking)
tags: [concept, segurança, csp, blazor, headers]
status: stable
created: 2026-07-05
updated: 2026-07-05
aliases: [csp, content-security-policy, security headers]
sources:
  - "[[sources/2026-07-05-refactor-p0-proxy-overrides]]"
related:
  - "[[entities/tcmine-server]]"
  - "[[concepts/secrets-data-protection]]"
  - "[[concepts/modpack-admin-editor]]"
---

# Security headers (CSP + anti-clickjacking)

> Todas as respostas do servidor carregam uma **Content-Security-Policy** e cabeçalhos
> anti-clickjacking. Defesa em profundidade contra XSS/injeção no painel admin.

## O que é

`TCMine_Server.Security.SecurityHeaders` (`UseSecurityHeaders()`) — middleware ligado
cedo no pipeline (após o HTTPS redirect, **antes** dos ficheiros estáticos, para os
cobrir). Define numa única passagem, via `Response.OnStarting`:

- **`Content-Security-Policy`** (a política calibrada — ver abaixo)
- **`X-Content-Type-Options: nosniff`**
- **`X-Frame-Options: SAMEORIGIN`** (legado; `frame-ancestors` cobre o moderno)
- **`Referrer-Policy: strict-origin-when-cross-origin`**
- **`Permissions-Policy`** desligando camera/microphone/geolocation/interest-cohort

## A CSP e por que cada afrouxamento

A política é montada para o stack real do painel — **Blazor Server + MudBlazor +
Monaco** — que impõe alguns afrouxamentos inevitáveis:

```
default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'self';
img-src 'self' data: https:; font-src 'self' data:;
style-src 'self' 'unsafe-inline'; script-src 'self' 'wasm-unsafe-eval' blob:;
worker-src 'self' blob:; connect-src 'self'
```

- **`style-src 'unsafe-inline'`** — o `<style>` dos design tokens no `App.razor` e os
  `style=""` inline que o MudBlazor gera (posicionamento de popovers, etc.) não têm
  nonce viável em SSR.
- **`script-src 'self' blob:`** — os scripts do app são externos (`_framework`,
  MudBlazor, BlazorMonaco); o **Monaco** cria web workers via `blob:`.
- **`worker-src 'self' blob:`** — workers do Monaco.
- **`img-src https:`** — thumbnails de mods vêm do CDN do CurseForge (domínios variados).
- **`connect-src 'self'`** — o WebSocket do Blazor (`/_blazor`) e o SSE `/events` são
  mesma-origem.

## Detalhe: header CSP único

O framework Blazor acrescenta o **seu próprio** `Content-Security-Policy: frame-ancestors
'self'` (anti-clickjacking do circuito) durante o render do componente. Para não sair
**dois** headers, o middleware usa `OnStarting` (corre depois do endpoint) + o **indexer**
(`headers["Content-Security-Policy"] = ...`, que substitui) — garante um único header, já
que a nossa política inclui o mesmo `frame-ancestors 'self'`.

## Validação (contra o app rodando)

Verificado com o servidor no ar (2026-07-05): home pública + login (form MudBlazor) +
carga de todos os scripts do Monaco + WebSocket do Blazor conectando — **zero violações
de CSP** no console. Ver [[sources/2026-07-05-refactor-p0-proxy-overrides]].

## Aplicação concreta

- `TCMine-Server/Security/SecurityHeaders.cs`; ligado no `Program.cs` via
  `app.UseSecurityHeaders()`.

## Referências

- [[sources/2026-07-05-refactor-p0-proxy-overrides]]
