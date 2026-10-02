using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Tests.Domain;

/// <summary>
///     O lease é o que impede o mesmo item de sair em dois servidores. Cada teste
///     é um cenário do plano (docs/CLOUD-STORAGE.md §4).
/// </summary>
public sealed class CloudLeaseTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);
    private static readonly Guid ServidorA = Guid.CreateVersion7();
    private static readonly Guid ServidorB = Guid.CreateVersion7();

    private static CloudLease NovoLease() => new() { VaultId = Guid.CreateVersion7(), PlayerUuid = new string('a', 32) };

    [Fact]
    public void Primeiro_acquire_comeca_na_epoca_1()
    {
        var lease = NovoLease();

        lease.TryAcquire(ServidorA, T0, Ttl).ShouldBeTrue();

        lease.Epoch.ShouldBe(1);
        lease.LastSeq.ShouldBe(0);
        lease.IsHeldBy(ServidorA, T0).ShouldBeTrue();
    }

    [Fact]
    public void Outro_servidor_nao_pega_enquanto_o_lease_vale()
    {
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);

        lease.TryAcquire(ServidorB, T0.AddMinutes(10), Ttl).ShouldBeFalse();

        lease.Epoch.ShouldBe(1);
    }

    [Fact]
    public void Lease_vencido_pode_ser_tomado_e_a_epoca_sobe()
    {
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);

        lease.TryAcquire(ServidorB, T0 + Ttl, Ttl).ShouldBeTrue();

        lease.Epoch.ShouldBe(2);
        lease.HolderServerId.ShouldBe(ServidorB);
    }

    [Fact]
    public void Mesmo_servidor_pega_de_novo_com_epoca_nova()
    {
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);
        lease.Advance(1);

        lease.TryAcquire(ServidorA, T0.AddMinutes(1), Ttl).ShouldBeTrue();

        lease.Epoch.ShouldBe(2);
        lease.LastSeq.ShouldBe(0);
    }

    [Fact]
    public void Lote_da_epoca_velha_e_recusado_depois_que_outro_tomou()
    {
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);
        lease.TryAcquire(ServidorB, T0 + Ttl, Ttl);

        lease.Check(ServidorA, epoch: 1, seq: 1).ShouldBe(CloudBatchCheck.StaleEpoch);
    }

    [Fact]
    public void Lote_da_epoca_atual_de_outro_servidor_e_recusado()
    {
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);

        lease.Check(ServidorB, epoch: 1, seq: 1).ShouldBe(CloudBatchCheck.NotHolder);
    }

    [Fact]
    public void Sequencia_aceita_duplicado_e_buraco()
    {
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);

        lease.Check(ServidorA, 1, 1).ShouldBe(CloudBatchCheck.Accept);
        lease.Advance(1);
        lease.Check(ServidorA, 1, 1).ShouldBe(CloudBatchCheck.Duplicate);
        lease.Check(ServidorA, 1, 3).ShouldBe(CloudBatchCheck.Gap);
        lease.Check(ServidorA, 2, 1).ShouldBe(CloudBatchCheck.UnknownEpoch);
    }

    [Fact]
    public void Lote_atrasado_ainda_e_aceito_com_o_lease_vencido_se_ninguem_tomou()
    {
        // O servidor gravou no diário quando era dono; o TCMine ficou fora e o
        // lote chegou depois do TTL. Sem ninguém ter tomado, nada conflita.
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);

        lease.Check(ServidorA, 1, 1).ShouldBe(CloudBatchCheck.Accept);
    }

    [Fact]
    public void Advance_fora_de_ordem_falha()
    {
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);

        Should.Throw<InvalidOperationException>(() => lease.Advance(2));
    }

    [Fact]
    public void Release_so_libera_com_todos_os_lotes_aplicados()
    {
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);
        lease.Advance(1);

        lease.Release(ServidorA, 1, lastSeq: 2).ShouldBeFalse("o lote 2 ainda não chegou");
        lease.Release(ServidorA, 1, lastSeq: 1).ShouldBeTrue();
        lease.IsFree(T0).ShouldBeTrue();
    }

    [Fact]
    public void Heartbeat_de_epoca_antiga_nao_renova()
    {
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);
        lease.TryAcquire(ServidorA, T0.AddMinutes(1), Ttl); // época 2

        lease.Renew(ServidorA, epoch: 1, T0.AddMinutes(2), Ttl).ShouldBeFalse();
        lease.Renew(ServidorA, epoch: 2, T0.AddMinutes(2), Ttl).ShouldBeTrue();
        lease.ExpiresAt.ShouldBe(T0.AddMinutes(2) + Ttl);
    }

    [Fact]
    public void Queda_do_tcmine_estende_o_lease_e_nao_encurta()
    {
        var lease = NovoLease();
        lease.TryAcquire(ServidorA, T0, Ttl);

        lease.ExtendAfterOutage(T0 + Ttl + TimeSpan.FromMinutes(5), Ttl);
        lease.IsHeldBy(ServidorA, T0 + Ttl + TimeSpan.FromMinutes(10)).ShouldBeTrue();

        var antes = lease.ExpiresAt;
        lease.ExtendAfterOutage(T0, Ttl);
        lease.ExpiresAt.ShouldBe(antes);
    }

    [Fact]
    public void Toda_mudanca_aumenta_a_versao_de_concorrencia()
    {
        var lease = NovoLease();
        var v0 = lease.Version;
        lease.TryAcquire(ServidorA, T0, Ttl);
        lease.Advance(1);

        lease.Version.ShouldBe(v0 + 2);
    }
}
