using Microsoft.EntityFrameworkCore;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Jogador duplicado: o índice único segura a segunda linha, e a fusão
///     junta as duas contas de uma pessoa sem perder acesso nenhum.
/// </summary>
public sealed class UserRepositoryTests : IDisposable
{
    private readonly SqliteTestFactory _factory = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Segundo_insert_do_mesmo_uuid_perde_a_corrida_sem_lancar()
    {
        var repo = new UserRepository(_factory);

        (await repo.TryAddAsync(new User { DisplayName = "ana", MinecraftUuid = "abc" }, Ct)).ShouldBeTrue();
        (await repo.TryAddAsync(new User { DisplayName = "ana", MinecraftUuid = "abc" }, Ct)).ShouldBeFalse();

        await using var db = await _factory.CreateDbContextAsync(Ct);
        (await db.Users.CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Fusao_move_os_acessos_e_apaga_a_conta_absorvida()
    {
        var repo = new UserRepository(_factory);
        var painel = new User { DisplayName = "Ana", MicrosoftObjectId = "oid" };
        var launcher = new User { DisplayName = "ana", MinecraftUuid = "abc" };
        await repo.AddAsync(painel, Ct);
        await repo.AddAsync(launcher, Ct);

        var servidorComum = Guid.CreateVersion7();
        var soDoLauncher = Guid.CreateVersion7();

        await using (var db = await _factory.CreateDbContextAsync(Ct))
        {
            db.Memberships.AddRange(
                new Membership { UserId = painel.Id, GameServerId = servidorComum, Role = ServerRole.Member },
                new Membership { UserId = launcher.Id, GameServerId = servidorComum, Role = ServerRole.Admin },
                new Membership { UserId = launcher.Id, GameServerId = soDoLauncher, Role = ServerRole.Member });
            await db.SaveChangesAsync(Ct);
        }

        await repo.MergeAsync(painel.Id, launcher.Id, Ct);

        await using var check = await _factory.CreateDbContextAsync(Ct);
        (await check.Users.AnyAsync(u => u.Id == launcher.Id, Ct)).ShouldBeFalse();

        var vinculos = await check.Memberships.Where(m => m.UserId == painel.Id).ToListAsync(Ct);
        vinculos.Count.ShouldBe(2);

        // No servidor em que as duas contas estavam, fica o papel MAIOR.
        vinculos.Single(m => m.GameServerId == servidorComum).Role.ShouldBe(ServerRole.Admin);
        (await check.Memberships.AnyAsync(m => m.UserId == launcher.Id, Ct)).ShouldBeFalse();
    }
}
