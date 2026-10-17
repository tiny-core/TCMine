using Microsoft.EntityFrameworkCore;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Infrastructure.Persistence;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Consultas do painel da nuvem contra um banco de verdade. As agregações
///     (jogadores distintos, somas por canal) compilam sempre — é na tradução
///     para SQL que quebram.
/// </summary>
public sealed class CloudAdminRepositoryTests
{
    private const string Ana = "069a79f444e94726a5befca90e38aaf5";
    private const string Beto = "11111111222233334444555566667777";

    private const string MotivoDoSkip =
        "Sem PostgreSQL: defina TCMINE_TEST_POSTGRES para rodar (o CI define).";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lista_de_nuvens_conta_servidores_e_jogadores_distintos()
    {
        using var factory = new SqliteTestFactory();
        var cenario = await Semear(factory);
        var repo = new CloudAdminRepository(factory);

        var nuvens = await repo.ListVaultsAsync(cenario.Dono, Ct);

        var n = nuvens.Single();
        n.Servers.ShouldBe(1);
        n.Players.ShouldBe(2, "Ana tem dois canais, mas é um jogador só");
        (await repo.ListVaultsAsync(Guid.CreateVersion7(), Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Jogadores_vem_com_nome_totais_por_canal_e_busca()
    {
        using var factory = new SqliteTestFactory();
        var cenario = await Semear(factory);
        var repo = new CloudAdminRepository(factory);

        var pagina = await repo.ListPlayersAsync(cenario.Nuvem, null, new PageRequest(0, 50), Ct);
        pagina.TotalCount.ShouldBe(2);
        var todos = pagina.Items;
        todos.Count.ShouldBe(2);
        var ana = todos.Single(p => p.PlayerUuid == Ana);
        ana.DisplayName.ShouldBe("Ana");
        ana.Channels.Count.ShouldBe(2);
        var principal = ana.Channels.Single(c => c.Name == "Principal");
        principal.TotalItems.ShouldBe(5_000_000_000L + 7);
        principal.ItemTypes.ShouldBe(2, "o saldo zerado não conta como tipo");
        ana.Channels.Single(c => c.Name == "Minérios").TotalItems.ShouldBe(0);
        ana.HolderServerId.ShouldBe(cenario.Servidor);

        (await Buscar(repo, cenario.Nuvem, "an")).ShouldBe([Ana]);
        (await Buscar(repo, cenario.Nuvem, "1111-2222")).ShouldBe([Beto]);
    }

    [Fact]
    public async Task Jogadores_vem_por_pagina_com_conta_primeiro_e_o_total_da_busca()
    {
        using var factory = new SqliteTestFactory();
        var cenario = await Semear(factory);
        var repo = new CloudAdminRepository(factory);

        var primeira = await repo.ListPlayersAsync(cenario.Nuvem, null, new PageRequest(0, 1), Ct);
        var segunda = await repo.ListPlayersAsync(cenario.Nuvem, null, new PageRequest(1, 1), Ct);
        var alem = await repo.ListPlayersAsync(cenario.Nuvem, null, new PageRequest(2, 1), Ct);

        primeira.TotalCount.ShouldBe(2);
        primeira.Items.Single().PlayerUuid.ShouldBe(Ana, "quem tem conta no TCMine vem antes");
        segunda.Items.Single().PlayerUuid.ShouldBe(Beto);
        segunda.Items.Single().Channels.Single().TotalItems.ShouldBe(1, "a página traz os canais só dos seus jogadores");
        alem.Items.ShouldBeEmpty();
        alem.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Busca_de_jogador_ignora_maiusculas_no_sqlite()
    {
        using var factory = new SqliteTestFactory();
        var cenario = await Semear(factory);

        (await Buscar(new CloudAdminRepository(factory), cenario.Nuvem, "ANA")).ShouldBe([Ana]);
        (await Buscar(new CloudAdminRepository(factory), cenario.Nuvem, "1111-2222-3333-4444-555566667777"))
            .ShouldBe([Beto], ignoreOrder: false, "UUID colado com hífens");
    }

    [Fact]
    public async Task Busca_de_jogador_ignora_maiusculas_no_postgres()
    {
        // O LIKE do PostgreSQL diferencia maiúsculas (o do SQLite também, via
        // instr): sem lower() dos dois lados, "ana" não achava "Ana" em produção.
        Assert.SkipWhen(PostgresTestDatabase.ServerConnectionString is null, MotivoDoSkip);
        await using var postgres = await PostgresTestDatabase.CreateAsync(Ct);
        var cenario = await Semear(postgres);
        var repo = new CloudAdminRepository(postgres);

        (await Buscar(repo, cenario.Nuvem, "aNa")).ShouldBe([Ana]);
        (await repo.ListPlayersAsync(cenario.Nuvem, null, new PageRequest(0, 1), Ct)).Items.Single().PlayerUuid
            .ShouldBe(Ana);
    }

    [Fact]
    public async Task Itens_do_canal_sem_os_zerados_e_do_maior_para_o_menor()
    {
        using var factory = new SqliteTestFactory();
        var cenario = await Semear(factory);
        var repo = new CloudAdminRepository(factory);

        var itens = await repo.ListChannelBalancesAsync(cenario.CanalPrincipalAna, Ct);

        itens.Select(i => i.ItemId).ShouldBe(["minecraft:cobblestone", "minecraft:diamond"]);
    }

    private static async Task<IReadOnlyList<string>> Buscar(CloudAdminRepository repo, Guid nuvem, string busca) =>
        [.. (await repo.ListPlayersAsync(nuvem, busca, new PageRequest(0, 50), Ct)).Items.Select(p => p.PlayerUuid)];

    private static async Task<Cenario> Semear(IDbContextFactory<TcMineDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync(Ct);
        var dono = Guid.CreateVersion7();
        var nuvem = new CloudVault { Name = "N", OwnerId = dono };
        var servidor = new GameServer
        {
            Name = "S",
            ModpackId = Guid.CreateVersion7(),
            ModpackVersionId = Guid.CreateVersion7(),
            ConnectAddress = "localhost",
            RconSecret = "segredo",
            OwnerId = dono
        };
        servidor.AttachToCloudVault(nuvem);
        db.AddRange(nuvem, servidor, new User { DisplayName = "Ana", MinecraftUuid = Ana });
        await db.SaveChangesAsync(Ct);

        var principal = new CloudChannel { VaultId = nuvem.Id, PlayerUuid = Ana, Name = "Principal" };
        var minerios = new CloudChannel { VaultId = nuvem.Id, PlayerUuid = Ana, Name = "Minérios" };
        var beto = new CloudChannel { VaultId = nuvem.Id, PlayerUuid = Beto, Name = "Principal" };
        var diamante = Item("minecraft:diamond", 'd');
        var pedra = Item("minecraft:cobblestone", 'c');
        var ferro = Item("minecraft:iron_ingot", 'f');
        db.AddRange(principal, minerios, beto, diamante, pedra, ferro);
        db.AddRange(Saldo(principal, diamante, 7), Saldo(principal, pedra, 5_000_000_000L),
            Saldo(principal, ferro, 0), Saldo(beto, diamante, 1));
        var lease = new CloudLease { VaultId = nuvem.Id, PlayerUuid = Ana };
        lease.TryAcquire(servidor.Id, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(30));
        db.Add(lease);
        await db.SaveChangesAsync(Ct);
        return new Cenario(dono, nuvem.Id, servidor.Id, principal.Id);
    }

    private static CloudItemType Item(string id, char c) => new()
    {
        Fingerprint = new string(c, 64),
        ItemId = id,
        ModId = "minecraft",
        DisplayName = id,
        Encoded = [1]
    };

    private static CloudBalance Saldo(CloudChannel canal, CloudItemType item, long quantidade)
    {
        var saldo = new CloudBalance { ChannelId = canal.Id, ItemTypeId = item.Id };
        if (quantidade > 0) saldo.Apply(quantidade);
        return saldo;
    }

    private sealed record Cenario(Guid Dono, Guid Nuvem, Guid Servidor, Guid CanalPrincipalAna);
}
