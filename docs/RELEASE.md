# Lançar uma versão

O repositório abriga dois produtos, então a tag diz de qual se trata:

| Tag | O que dispara |
|---|---|
| `server-v0.2.0` | Constrói a imagem e publica no Docker Hub |
| `launcher-v0.1.0` | Reservado para o launcher — ainda sem workflow |

O prefixo não é cosmético: sem ele, publicar o launcher reconstruiria o servidor
e vice-versa.

## Configurar uma vez

Em **Settings → Secrets and variables → Actions** do repositório:

| Nome | Tipo | Valor |
|---|---|---|
| `DOCKERHUB_USERNAME` | secret | Seu usuário do Docker Hub |
| `DOCKERHUB_TOKEN` | secret | Access token com escrita (Docker Hub → Account Settings → Personal access tokens). **Não** a senha da conta. |
| `DOCKERHUB_IMAGE` | variable | Nome do repositório da imagem, ex.: `jocian/tcmine-server` |

`DOCKERHUB_IMAGE` é variável e não segredo porque aparece no log de qualquer
forma — marcar como segredo só produziria `***` nas mensagens e dificultaria
entender o que foi publicado.

## Lançar o servidor

```bash
git tag server-v0.1.0
git push origin server-v0.1.0
```

O workflow roda as suítes de teste **antes** de publicar. Uma imagem publicada é
imutável na prática — alguém pode tê-la baixado no minuto seguinte —, então não
vale confiar num CI que passou numa versão anterior do código.

Tags geradas para `server-v0.1.0`:

- `0.1.0` — a versão exata
- `0.1` — acompanha os patches dessa linha
- `latest`

Um pré-lançamento (`server-v0.2.0-beta.1`) recebe **só** a versão exata. Nem
`latest` nem a tag curta: quem pede "a versão atual" não está pedindo um beta.

## A versão dentro da imagem

O número da tag entra na build e vira a versão do assembly, que é o que o
handshake devolve ao launcher em `serverVersion`. Sem isso toda imagem se
anunciaria como `1.0.0` — o padrão do SDK quando nada é informado.

Para conferir depois de publicar:

```bash
curl -s https://seu-dominio/api/handshake | jq .serverVersion
```

## Testar o workflow sem lançar

O workflow aceita disparo manual (**Actions → Publicar imagem do servidor → Run
workflow**). Nesse caso a imagem sai como `0.0.0-manual` e não recebe `latest`,
para um teste não virar a versão que os outros baixam.

## Sobre o launcher

`launcher-v*` está reservada, sem workflow ainda. Quando existir, ela não vai
publicar imagem: o launcher é distribuído pelo **Velopack**, e o feed é servido
pelo próprio TCMine Server em `/updates/launcher/{canal}/`.

Vale lembrar de uma decisão já tomada, porque ela muda o formato desse workflow:
o canal do Velopack deriva do **protocolo**, não da versão do produto — hoje
`win-x64-p1`. É o que permite publicar launcher 1.6, 1.7 e 1.8 sem release
nenhuma do servidor. Ver `Protocol.cs` e `HandshakeEndpoints.cs`.
