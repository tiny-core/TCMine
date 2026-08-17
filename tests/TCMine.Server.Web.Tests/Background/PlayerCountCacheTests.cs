using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Tests.Background;

/// <summary>
///     A contagem de jogadores e o sinal de "mudou".
///     O retorno do Set é o que decide se um evento sai para todo launcher
///     conectado. Errar para mais transforma a coleta de quinze segundos num
///     broadcast periódico que não informa nada; errar para menos congela o
///     número na tela de quem está jogando.
/// </summary>
public sealed class PlayerCountCacheTests
{
    private static readonly Guid ServidorId = Guid.CreateVersion7();

    [Fact]
    public void Primeira_amostragem_conta_como_mudanca()
    {
        var cache = new PlayerCountCache();

        cache.Set(ServidorId, 3).ShouldBeTrue();
        cache.TryGet(ServidorId).ShouldBe(3);
    }

    [Fact]
    public void Mesmo_numero_de_novo_nao_e_mudanca()
    {
        var cache = new PlayerCountCache();
        cache.Set(ServidorId, 3);

        cache.Set(ServidorId, 3).ShouldBeFalse();
    }

    [Fact]
    public void Numero_diferente_e_mudanca()
    {
        var cache = new PlayerCountCache();
        cache.Set(ServidorId, 3);

        cache.Set(ServidorId, 4).ShouldBeTrue();
    }

    [Fact]
    public void Esquecer_volta_para_nao_sei_e_nao_para_zero()
    {
        // Zero diria "servidor vazio". Um servidor desligado não está vazio —
        // não se sabe nada sobre ele.
        var cache = new PlayerCountCache();
        cache.Set(ServidorId, 5);

        cache.Forget(ServidorId);

        cache.TryGet(ServidorId).ShouldBeNull();
    }

    [Fact]
    public void Servidor_nunca_amostrado_responde_nao_sei()
    {
        new PlayerCountCache().TryGet(ServidorId).ShouldBeNull();
    }

    [Fact]
    public void Voltar_a_amostrar_depois_de_esquecer_conta_como_mudanca()
    {
        // Sem isto, um servidor que reinicia com a mesma contagem de antes
        // ficaria sem evento e os launchers exibiriam o traço para sempre.
        var cache = new PlayerCountCache();
        cache.Set(ServidorId, 2);
        cache.Forget(ServidorId);

        cache.Set(ServidorId, 2).ShouldBeTrue();
    }
}
