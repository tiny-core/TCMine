using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Regressão: apagar um servidor ou um modpack deixava os vínculos, os
///     convites e as novidades dele para trás, e "Ver vínculos" mostrava
///     "(servidor removido)" e "(modpack removido)" para sempre. Agora quem
///     limpa é a chave estrangeira — e é ela que estes testes cobram.
/// </summary>
public sealed class OrphanLinksTests : IDisposable
{
    private readonly SqliteTestFactory _factory = new();
    private readonly RecordingResourceChanges _changes = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Apagar_o_servidor_leva_os_vinculos_e_os_convites_dele()
    {
        var apagado = Servidor("apagado", 25565);
        var outro = Servidor("outro", 25566);
        var ana = Guid.CreateVersion7();

        await using (var db = await _factory.CreateDbContextAsync(Ct))
        {
            db.GameServers.AddRange(apagado, outro);
            db.Memberships.AddRange(
                new Membership { UserId = ana, GameServerId = apagado.Id, Role = ServerRole.Owner },
                new Membership { UserId = ana, GameServerId = outro.Id, Role = ServerRole.Member });
            db.Invites.Add(Convite(apagado.Id, ana));
            await db.SaveChangesAsync(Ct);
        }

        await new ServerRepository(_factory, _changes).RemoveAsync(apagado.Id, Ct);

        await using var check = await _factory.CreateDbContextAsync(Ct);
        (await check.Memberships.SingleAsync(Ct)).GameServerId.ShouldBe(outro.Id);
        (await check.Invites.AnyAsync(Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task Apagar_o_modpack_leva_os_vinculos_e_as_novidades_dele()
    {
        var apagado = Pack("apagado");
        var outro = Pack("outro");
        var ana = Guid.CreateVersion7();

        await using (var db = await _factory.CreateDbContextAsync(Ct))
        {
            db.Modpacks.AddRange(apagado, outro);
            db.ModpackMemberships.AddRange(
                new ModpackMembership { UserId = ana, ModpackId = apagado.Id, Role = ModpackRole.Owner },
                new ModpackMembership { UserId = ana, ModpackId = outro.Id, Role = ModpackRole.Editor });
            db.News.Add(new News { ModpackId = apagado.Id, Title = "Nova versão", Body = "corpo" });
            await db.SaveChangesAsync(Ct);
        }

        await new ModpackRepository(_factory, _changes).RemoveAsync(apagado.Id, Ct);

        await using var check = await _factory.CreateDbContextAsync(Ct);
        (await check.ModpackMemberships.SingleAsync(Ct)).ModpackId.ShouldBe(outro.Id);
        (await check.News.AnyAsync(Ct)).ShouldBeFalse();
    }

    private static GameServer Servidor(string name, int port) => new()
    {
        Name = name,
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = $"jogo:{port}",
        GamePort = port,
        RconSecret = "segredo"
    };

    private static Modpack Pack(string slug) => new()
    {
        Slug = slug, Name = slug, MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
    };

    private static Invite Convite(Guid serverId, Guid createdBy) => new()
    {
        CodeHash = new string('a', 64),
        GameServerId = serverId,
        Role = ServerRole.Member,
        CreatedByUserId = createdBy,
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
    };
}
