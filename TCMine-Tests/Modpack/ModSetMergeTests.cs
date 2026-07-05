using TCMine_Application.Modpack;
using Xunit;

namespace TCMine_Tests.Modpack;

// Merge PURO por chave: novo ⇒ adiciona, existente ⇒ atualiza no lugar, ausente no incoming ⇒ mantém.
// A ordem importa (o manifesto do launcher preserva a ordem de instalação dos mods).
public class ModSetMergeTests
{
    private static (int Added, int Updated) MergeTags(
        IEnumerable<Mod> current, IEnumerable<Mod> incoming, out List<Mod> items)
    {
        var r = ModSetMerge.Merge(current, incoming, m => m.Id);
        items = r.Items;
        return (r.Added, r.Updated);
    }

    [Fact]
    public void Mod_novo_e_adicionado_no_fim()
    {
        var (added, updated) = MergeTags(
            [new Mod(1, "a")], [new Mod(2, "b")], out var items);

        Assert.Equal(1, added);
        Assert.Equal(0, updated);
        Assert.Equal([1, 2], items.Select(m => m.Id));
    }

    [Fact]
    public void Mod_existente_e_atualizado_no_lugar()
    {
        var (added, updated) = MergeTags(
            [new Mod(1, "old"), new Mod(2, "keep")],
            [new Mod(1, "new")], out var items);

        Assert.Equal(0, added);
        Assert.Equal(1, updated);
        // Mesma posição, payload substituído
        Assert.Equal([1, 2], items.Select(m => m.Id));
        Assert.Equal("new", items[0].Tag);
        Assert.Equal("keep", items[1].Tag);
    }

    [Fact]
    public void Mod_atual_ausente_no_incoming_e_mantido()
    {
        var (added, updated) = MergeTags(
            [new Mod(1, "a"), new Mod(2, "b")],
            [new Mod(1, "a2")], out var items);

        Assert.Equal(0, added);
        Assert.Equal(1, updated);
        Assert.Contains(items, m => m.Id == 2); // não foi removido
    }

    [Fact]
    public void Incoming_vazio_preserva_current_intacto()
    {
        var (added, updated) = MergeTags(
            [new Mod(1, "a"), new Mod(2, "b")], [], out var items);

        Assert.Equal((0, 0), (added, updated));
        Assert.Equal([1, 2], items.Select(m => m.Id));
    }

    [Fact]
    public void Current_vazio_adiciona_tudo_na_ordem_do_incoming()
    {
        var (added, updated) = MergeTags(
            [], [new Mod(3, "c"), new Mod(1, "a")], out var items);

        Assert.Equal(2, added);
        Assert.Equal(0, updated);
        Assert.Equal([3, 1], items.Select(m => m.Id));
    }

    [Fact]
    public void Incoming_com_novo_e_update_conta_os_dois()
    {
        var (added, updated) = MergeTags(
            [new Mod(1, "a")],
            [new Mod(1, "a2"), new Mod(2, "b")], out var items);

        Assert.Equal(1, added);
        Assert.Equal(1, updated);
        Assert.Equal([1, 2], items.Select(m => m.Id));
        Assert.Equal("a2", items[0].Tag);
    }

    // Item mínimo com chave (id) e um payload para distinguir "atualizado"
    private sealed record Mod(long Id, string Tag);
}