using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     O endereço que o servidor TCMine devolve é o texto do "Adicionar
///     servidor" do jogo; o Minecraft o quer em host e porta separados.
/// </summary>
public class ServerAddressTests
{
    [Theory]
    [InlineData("mc.exemplo.com", "mc.exemplo.com", null)]
    [InlineData("mc.exemplo.com:25570", "mc.exemplo.com", 25570)]
    [InlineData(" 192.168.0.10:25565 ", "192.168.0.10", 25565)]
    [InlineData("[2001:db8::1]:25565", "2001:db8::1", 25565)]
    [InlineData("2001:db8::1", "2001:db8::1", null)] // IPv6 sem porta: não corta no último ':'
    public void Separa_host_e_porta(string text, string host, int? port) =>
        ServerAddress.Parse(text).ShouldBe(new ServerAddress(host, port));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("mc.exemplo.com:abc")]
    [InlineData("mc.exemplo.com:70000")]
    [InlineData(":25565")]
    public void Endereco_invalido_e_nulo(string? text) => ServerAddress.Parse(text).ShouldBeNull();
}
