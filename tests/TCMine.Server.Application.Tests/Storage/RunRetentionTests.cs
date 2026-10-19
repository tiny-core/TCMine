using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Storage;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Common;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Tests.Storage;

/// <summary>
///     As regras da retenção (5.4), sem banco: prazo de cada nuvem, zero = para
///     sempre, e as proteções do histórico de jogo — incidente aberto e backup
///     mais antigo do servidor.
/// </summary>
public sealed class RunRetentionTests
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Cada_nuvem_corta_no_proprio_prazo_e_zero_nao_apaga_nada()
    {
        var curta = Guid.CreateVersion7();
        var eterna = Guid.CreateVersion7();
        var servidor = Guid.CreateVersion7();
        var repo = new FakeRetention();
        repo.Vaults.AddRange([new CloudVaultRetention(curta, 90), new CloudVaultRetention(eterna, 0)]);
        repo.Servers[curta] = [new CloudServerHistory(servidor, false, null)];
        repo.Servers[eterna] = [new CloudServerHistory(servidor, false, null)];

        await Rodar(repo);

        repo.ServerPurges.ShouldBe([(curta, servidor, Agora.AddDays(-90))]);
        repo.ListedVaults.ShouldBe([curta], ignoreOrder: false, "a nuvem eterna nem é consultada");
    }

    [Fact]
    public async Task Servidor_com_incidente_aberto_nao_perde_nada()
    {
        // A prévia do estorno ainda vai ler os lotes posteriores ao checkpoint.
        var nuvem = Guid.CreateVersion7();
        var repo = new FakeRetention();
        repo.Vaults.Add(new CloudVaultRetention(nuvem, 90));
        repo.Servers[nuvem] = [new CloudServerHistory(Guid.CreateVersion7(), true, null)];

        var report = await Rodar(repo);

        repo.ServerPurges.ShouldBeEmpty();
        report.ServersProtected.ShouldBe(1);
    }

    [Fact]
    public async Task Backup_mais_antigo_puxa_o_corte_para_tras_e_o_mais_novo_nao_muda_nada()
    {
        // Restaurar um backup de 120 dias abre um incidente cujo estorno precisa
        // dos lotes desde ele: o prazo de 90 não pode tê-los levado.
        var nuvem = Guid.CreateVersion7();
        var antigo = Guid.CreateVersion7();
        var recente = Guid.CreateVersion7();
        var repo = new FakeRetention();
        repo.Vaults.Add(new CloudVaultRetention(nuvem, 90));
        repo.Servers[nuvem] =
        [
            new CloudServerHistory(antigo, false, Agora.AddDays(-120)),
            new CloudServerHistory(recente, false, Agora.AddDays(-10))
        ];

        await Rodar(repo);

        repo.ServerPurges.ShouldBe(
        [
            (nuvem, antigo, Agora.AddDays(-120)),
            (nuvem, recente, Agora.AddDays(-90))
        ], ignoreOrder: true);
    }

    [Fact]
    public async Task Feed_de_atividade_segue_a_instalacao_e_a_limpeza_domestica_e_fixa()
    {
        var repo = new FakeRetention();

        await Rodar(repo, new InstallationSettings { ActivityRetentionDays = 30 });
        await Rodar(repo, new InstallationSettings { ActivityRetentionDays = 0 });

        repo.ActivityPurges.ShouldBe([Agora.AddDays(-30)], ignoreOrder: false, "zero = para sempre: a segunda rodada não apaga");
        repo.HousekeepingPurges.ShouldBe(
            [Agora.AddDays(-RunRetention.HousekeepingDays), Agora.AddDays(-RunRetention.HousekeepingDays)]);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(7, true)]
    [InlineData(3650, true)]
    [InlineData(6, false)]
    [InlineData(-1, false)]
    [InlineData(3651, false)]
    public void Prazo_valido_e_zero_ou_de_uma_semana_a_dez_anos(int dias, bool valido) =>
        RetentionPeriod.IsValid(dias).ShouldBe(valido);

    private static Task<RetentionReport> Rodar(FakeRetention repo, InstallationSettings? config = null) =>
        new RunRetention(repo, new InMemorySettings(config), new RelogioFixo(Agora),
            NullLogger<RunRetention>.Instance).HandleAsync(Ct);

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    private sealed class FakeRetention : IRetentionRepository
    {
        public List<CloudVaultRetention> Vaults { get; } = [];
        public Dictionary<Guid, List<CloudServerHistory>> Servers { get; } = [];
        public List<Guid> ListedVaults { get; } = [];
        public List<(Guid Vault, Guid Server, DateTimeOffset Before)> ServerPurges { get; } = [];
        public List<DateTimeOffset> ActivityPurges { get; } = [];
        public List<DateTimeOffset> HousekeepingPurges { get; } = [];

        public Task<IReadOnlyList<CloudVaultRetention>> ListVaultRetentionAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CloudVaultRetention>>(Vaults);

        public Task<IReadOnlyList<CloudServerHistory>> ListServersWithGameHistoryAsync(Guid vaultId,
            DateTimeOffset before, CancellationToken ct)
        {
            ListedVaults.Add(vaultId);
            return Task.FromResult<IReadOnlyList<CloudServerHistory>>(Servers.GetValueOrDefault(vaultId) ?? []);
        }

        public Task<GameHistoryPurged> PurgeServerGameHistoryAsync(Guid vaultId, Guid serverId,
            DateTimeOffset before, CancellationToken ct)
        {
            ServerPurges.Add((vaultId, serverId, before));
            return Task.FromResult(new GameHistoryPurged(1, 1));
        }

        public Task<int> PurgeActivityAsync(DateTimeOffset before, CancellationToken ct)
        {
            ActivityPurges.Add(before);
            return Task.FromResult(0);
        }

        public Task<HousekeepingPurged> PurgeHousekeepingAsync(DateTimeOffset before, DateTimeOffset now,
            CancellationToken ct)
        {
            HousekeepingPurges.Add(before);
            return Task.FromResult(new HousekeepingPurged(0, 0));
        }
    }
}
