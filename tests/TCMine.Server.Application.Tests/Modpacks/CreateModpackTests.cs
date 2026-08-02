using NSubstitute;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Tests.Modpacks;

public class CreateModpackTests
{
    private readonly IModpackRepository _repo = Substitute.For<IModpackRepository>();
    private readonly ICurrentUserScope _scope = Substitute.For<ICurrentUserScope>();

    private CreateModpack CriarCasoDeUso()
    {
        _scope.OwnerId.Returns(Guid.CreateVersion7());
        return new CreateModpack(_repo, _scope);
    }

    private static CreateModpackCommand ComandoValido(string slug = "tech-medieval") =>
        new(slug, "Tecnologia Medieval", null, "1.21.1", ModLoader.NeoForge);

    [Fact]
    public async Task Cria_modpack_com_dados_validos()
    {
        _repo.SlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var caso = CriarCasoDeUso();

        var resultado = await caso.HandleAsync(ComandoValido(), TestContext.Current.CancellationToken);

        resultado.Succeeded.ShouldBeTrue();
        await _repo.Received(1).CreateAsync(Arg.Any<Modpack>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejeita_slug_duplicado()
    {
        _repo.SlugExistsAsync("tech-medieval", Arg.Any<CancellationToken>()).Returns(true);
        var caso = CriarCasoDeUso();

        var resultado = await caso.HandleAsync(ComandoValido(), TestContext.Current.CancellationToken);

        resultado.Succeeded.ShouldBeFalse();
        await _repo.DidNotReceive().CreateAsync(Arg.Any<Modpack>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("ab")] // curto demais
    [InlineData("tech_medieval")] // underscore não permitido
    [InlineData("-tech")] // hífen no início
    [InlineData("tech-")] // hífen no fim
    [InlineData("tech--medieval")] // hífen duplo
    public async Task Rejeita_slug_invalido(string slug)
    {
        var caso = CriarCasoDeUso();

        var resultado = await caso.HandleAsync(ComandoValido(slug), TestContext.Current.CancellationToken);

        resultado.Succeeded.ShouldBeFalse();
        await _repo.DidNotReceive().CreateAsync(Arg.Any<Modpack>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Normaliza_espacos_e_maiusculas_no_slug()
    {
        // "Tech Medieval" tem espaço e maiúscula, mas o espaço vira hífen e o
        // resto minúsculo — então normaliza para um slug válido em vez de
        // rejeitar.
        Modpack? capturado = null;
        _repo.When(r => r.CreateAsync(Arg.Any<Modpack>(), Arg.Any<CancellationToken>()))
            .Do(call => capturado = call.Arg<Modpack>());
        _repo.SlugExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var caso = CriarCasoDeUso();

        await caso.HandleAsync(
            new CreateModpackCommand("Tech Medieval", "Nome", null, "1.21.1", ModLoader.NeoForge),
            TestContext.Current.CancellationToken);

        capturado!.Slug.ShouldBe("tech-medieval");
    }
}
