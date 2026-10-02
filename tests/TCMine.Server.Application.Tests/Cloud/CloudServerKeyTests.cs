using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Application.Tests.Cloud;

public sealed class CloudServerKeyTests
{
    [Fact]
    public void Chave_gerada_tem_o_prefixo_e_bate_com_o_proprio_hash()
    {
        var (key, prefix, hash) = CloudServerKey.Generate();

        prefix.Length.ShouldBe(CloudServerCredential.PrefixLength);
        CloudServerKey.PrefixOf(key).ShouldBe(prefix);
        CloudServerKey.Matches(key, hash).ShouldBeTrue();
        CloudServerKey.Matches(key + "x", hash).ShouldBeFalse();
    }

    [Fact]
    public void Duas_chaves_nunca_sao_iguais()
    {
        CloudServerKey.Generate().Key.ShouldNotBe(CloudServerKey.Generate().Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer qualquer")]
    [InlineData("tcs_curto_segredo")]
    [InlineData("xyz_abcdefghijkm_segredo")]
    [InlineData("tcs_abcdefghijkm_")]
    public void Formato_estranho_nao_tem_prefixo(string? key)
    {
        CloudServerKey.PrefixOf(key).ShouldBeNull();
    }

    [Fact]
    public void Segredo_com_sublinhado_continua_valido()
    {
        // base64url usa "_": o parse corta só nos dois primeiros.
        CloudServerKey.PrefixOf("tcs_abcdefghijkm_a_b_c").ShouldBe("abcdefghijkm");
    }

    [Fact]
    public void Hash_do_lote_ignora_a_ordem_das_operacoes_e_as_definicoes()
    {
        var canal = Guid.CreateVersion7();
        var a = new CloudBatchRequest("p", 1, 2,
            [new(canal, "a", 1), new(canal, "b", -1)], [new(canal, "a", 1), new(canal, "b", 0)], null);
        var b = a with
        {
            Ops = [.. a.Ops.Reverse()],
            Definitions = [new CloudItemDto("a", "m:a", "A", "AQ==")]
        };
        var c = a with { Seq = 3 };

        CloudBatchHash.Of(a).ShouldBe(CloudBatchHash.Of(b));
        CloudBatchHash.Of(a).ShouldNotBe(CloudBatchHash.Of(c));
    }
}
