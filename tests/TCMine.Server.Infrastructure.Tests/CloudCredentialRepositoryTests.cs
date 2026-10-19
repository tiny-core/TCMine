using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     A chave do servidor no banco de verdade: uma linha por servidor, a
///     rotação sobrescreve, e as gravações do hello e da revogação mexem só nas
///     colunas delas.
/// </summary>
public sealed class CloudCredentialRepositoryTests : IDisposable
{
    private readonly SqliteTestFactory _factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Cada_start_sobrescreve_a_mesma_linha()
    {
        var (servidor, nuvem) = await SemearAsync();
        var repo = new CloudCredentialRepository(_factory);

        var primeira = await CloudKeyRotation.RotateAsync(repo, TimeProvider.System, servidor, nuvem, Ct);
        var segunda = await CloudKeyRotation.RotateAsync(repo, TimeProvider.System, servidor, nuvem, Ct);

        await using var db = await _factory.CreateDbContextAsync(Ct);
        var linha = await db.CloudServerCredentials.SingleAsync(Ct);
        CloudServerKey.Matches(segunda, linha.KeyHash).ShouldBeTrue();
        (await repo.FindKeyAsync(CloudServerKey.PrefixOf(primeira)!, Ct)).ShouldBeNull(
            "a chave anterior some junto com o prefixo dela");
    }

    [Fact]
    public async Task A_chave_vem_com_a_nuvem_que_o_servidor_tem_agora()
    {
        // A autenticação do mod lê chave e servidor numa consulta só (5.5), e
        // é ela que faz desligar da nuvem e revogar valerem na hora: não há
        // cache entre o painel e a próxima requisição.
        var (servidor, nuvem) = await SemearAsync();
        var repo = new CloudCredentialRepository(_factory);
        var chave = await CloudKeyRotation.RotateAsync(repo, TimeProvider.System, servidor, nuvem, Ct);
        var prefixo = CloudServerKey.PrefixOf(chave)!;

        var ligado = (await repo.FindKeyAsync(prefixo, Ct)).ShouldNotBeNull();
        ligado.GameServerId.ShouldBe(servidor);
        ligado.ServerVaultId.ShouldBe(nuvem);
        ligado.IsActive.ShouldBeTrue();
        CloudServerKey.Matches(chave, ligado.KeyHash).ShouldBeTrue();

        await using (var db = await _factory.CreateDbContextAsync(Ct))
        {
            var linha = await db.GameServers.SingleAsync(s => s.Id == servidor, Ct);
            linha.DetachFromCloudVault();
            await db.SaveChangesAsync(Ct);
        }

        (await repo.FindKeyAsync(prefixo, Ct))!.ServerVaultId.ShouldBeNull("desligado no painel");

        await repo.RevokeAsync(servidor, DateTimeOffset.UtcNow, Ct);
        (await repo.FindKeyAsync(prefixo, Ct))!.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task O_indice_recusa_uma_segunda_linha_para_o_mesmo_servidor()
    {
        var (servidor, nuvem) = await SemearAsync();
        var repo = new CloudCredentialRepository(_factory);
        await repo.AddAsync(CloudServerCredential.Issue(servidor, nuvem, "aaaaaaaaaaaa", "h1"), Ct);

        var outra = () => repo.AddAsync(CloudServerCredential.Issue(servidor, nuvem, "bbbbbbbbbbbb", "h2"), Ct);

        await outra.ShouldThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Contato_sem_versao_nem_mundo_mantem_os_anteriores()
    {
        var (servidor, nuvem) = await SemearAsync();
        var repo = new CloudCredentialRepository(_factory);
        var credencial = CloudServerCredential.Issue(servidor, nuvem, "aaaaaaaaaaaa", "h");
        await repo.AddAsync(credencial, Ct);
        var mundo = Guid.CreateVersion7();

        await repo.RecordContactAsync(credencial.Id, DateTimeOffset.UtcNow, new string('9', 40), mundo, Ct);
        await repo.RecordContactAsync(credencial.Id, DateTimeOffset.UtcNow, null, null, Ct);

        var lida = (await repo.FindByIdAsync(credencial.Id, Ct))!;
        lida.ModVersion.ShouldBe(new string('9', 32), "a coluna guarda até 32 caracteres");
        lida.WorldId.ShouldBe(mundo);
        lida.LastSeenAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Revogar_nao_desfaz_a_chave_reemitida_por_cima()
    {
        // A revogação grava só RevokedAt: com a linha inteira, uma cópia lida
        // antes da reemissão devolveria o hash da chave velha.
        var (servidor, nuvem) = await SemearAsync();
        var repo = new CloudCredentialRepository(_factory);
        await CloudKeyRotation.RotateAsync(repo, TimeProvider.System, servidor, nuvem, Ct);
        var copiaVelha = (await repo.FindByServerAsync(servidor, Ct))!;
        var nova = await CloudKeyRotation.RotateAsync(repo, TimeProvider.System, servidor, nuvem, Ct);

        await repo.RevokeAsync(servidor, DateTimeOffset.UtcNow, Ct);

        var lida = (await repo.FindByServerAsync(servidor, Ct))!;
        lida.IsActive.ShouldBeFalse();
        lida.KeyHash.ShouldNotBe(copiaVelha.KeyHash);
        CloudServerKey.Matches(nova, lida.KeyHash).ShouldBeTrue();
    }

    private async Task<(Guid Servidor, Guid Nuvem)> SemearAsync()
    {
        var dono = Guid.CreateVersion7();
        var nuvem = new CloudVault { Name = "Nuvem", OwnerId = dono };
        var servidor = new GameServer
        {
            Name = "A",
            ModpackId = Guid.CreateVersion7(),
            ModpackVersionId = Guid.CreateVersion7(),
            ConnectAddress = "jogo:25565",
            RconSecret = "segredo",
            OwnerId = dono
        };
        servidor.AttachToCloudVault(nuvem);

        await using var db = await _factory.CreateDbContextAsync(Ct);
        db.CloudVaults.Add(nuvem);
        db.GameServers.Add(servidor);
        await db.SaveChangesAsync(Ct);
        return (servidor.Id, nuvem.Id);
    }
}
