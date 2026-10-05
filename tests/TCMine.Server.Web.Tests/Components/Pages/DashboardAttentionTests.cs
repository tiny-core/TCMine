using TCMine.Contracts.Servers;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Components.Pages;

namespace TCMine.Server.Web.Tests.Components.Pages;

/// <summary>
///     "Precisa da sua atenção" no painel: o que pede decisão do admin, do mais
///     urgente para o resto. Servidor caído e disco cheio vêm antes de pedido de
///     acesso — esperar um jogador não perde nada; esperar um disco cheio perde
///     o mundo.
/// </summary>
public sealed class DashboardAttentionTests
{
    [Fact]
    public void Sem_nada_pendente_a_lista_fica_vazia() =>
        Home.BuildAttention(0, [], [], new Home.DiskUsage(10, 100)).ShouldBeEmpty();

    [Fact]
    public void Queda_e_disco_cheio_vem_antes_dos_pedidos()
    {
        var caido = Servidor("Survival", GameServerStatus.Crashed);

        var itens = Home.BuildAttention(3, [caido], [], new Home.DiskUsage(90, 100));

        itens.Select(i => i.Title).ShouldBe(["Survival caiu", "Disco 90% cheio", "3 pedidos de acesso"]);
        itens[0].Danger.ShouldBeTrue();
        itens[0].Href.ShouldBe($"/admin/modpacks/{caido.ModpackId}/servers");
    }

    [Fact]
    public void Um_pedido_fica_no_singular() =>
        Home.BuildAttention(1, [], [], null).Single().Title.ShouldBe("1 pedido de acesso");

    private static GameServer Servidor(string name, GameServerStatus status) => new()
    {
        Name = name,
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "mc.exemplo:25565",
        RconSecret = "segredo",
        Status = status
    };
}
