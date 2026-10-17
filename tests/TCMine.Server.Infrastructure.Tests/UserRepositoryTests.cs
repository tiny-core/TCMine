using Microsoft.EntityFrameworkCore;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Modpacks;
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

    [Fact]
    public async Task Fusao_leva_as_teclas_da_conta_absorvida()
    {
        // Quase sempre é a conta do LAUNCHER que tem as teclas: perdê-las por ter
        // entrado no painel seria reconfigurar tudo à mão.
        var repo = new UserRepository(_factory);
        var painel = new User { DisplayName = "Ana", MicrosoftObjectId = "oid" };
        var launcher = new User { DisplayName = "ana", MinecraftUuid = "abc" };
        await repo.AddAsync(painel, Ct);
        await repo.AddAsync(launcher, Ct);

        var comum = Pack("comum");
        var soDoLauncher = Pack("so-do-launcher");

        await using (var db = await _factory.CreateDbContextAsync(Ct))
        {
            db.Modpacks.AddRange(comum, soDoLauncher);
            db.PlayerModpackSettings.AddRange(
                Teclas(painel.Id, comum.Id, "key_key.jump:key.keyboard.space", DateTimeOffset.UtcNow.AddDays(-2)),
                Teclas(launcher.Id, comum.Id, "key_key.jump:key.keyboard.j", DateTimeOffset.UtcNow.AddDays(-1)),
                Teclas(launcher.Id, soDoLauncher.Id, "key_key.sneak:key.keyboard.c", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(Ct);
        }

        await repo.MergeAsync(painel.Id, launcher.Id, Ct);

        await using var check = await _factory.CreateDbContextAsync(Ct);
        var dela = await check.PlayerModpackSettings.Where(p => p.UserId == painel.Id).ToListAsync(Ct);

        dela.Count.ShouldBe(2);

        // No pack em que as duas tinham teclas, fica a gravada por último.
        dela.Single(p => p.ModpackId == comum.Id).KeyBindings.ShouldBe("key_key.jump:key.keyboard.j");
        dela.Single(p => p.ModpackId == soDoLauncher.Id).KeyBindings.ShouldBe("key_key.sneak:key.keyboard.c");
    }

    private static Modpack Pack(string slug) => new()
    {
        Slug = slug, Name = slug, MinecraftVersion = "1.21.1", Loader = ModLoader.NeoForge
    };

    private static PlayerModpackSettings Teclas(Guid userId, Guid modpackId, string keys, DateTimeOffset savedAt)
    {
        var settings = new PlayerModpackSettings { UserId = userId, ModpackId = modpackId };
        settings.Replace(keys, null);
        settings.UpdatedAt = savedAt;
        return settings;
    }
}
