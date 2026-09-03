using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Identity;

/// <summary>
///     O que responde enquanto o fluxo real da Microsoft não existe.
///     Não é um bypass: ele não autentica ninguém, não fabrica token e não abre
///     brecha. Ele diz, com todas as letras, que esta build não sabe entrar — o
///     que é melhor que a alternativa, que seria a tela de login estourar com
///     "nenhum serviço registrado para IMinecraftAuthenticator" na cara do
///     jogador.
///     Sai de cena na fatia do MSAL.
/// </summary>
public sealed class PendingMinecraftAuthenticator : IMinecraftAuthenticator
{
    private const string Mensagem =
        "Esta versão do launcher ainda não faz login com a Microsoft. "
        + "Atualize assim que a próxima versão estiver disponível.";

    // Silencioso: no arranque não há o que dizer, e uma mensagem aqui apareceria
    // toda vez que o launcher abrisse.
    public Task<MinecraftAuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
        Task.FromResult(MinecraftAuthResult.NoStoredCredentials());

    public Task<MinecraftAuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
        Task.FromResult(MinecraftAuthResult.Unavailable(Mensagem));

    public Task SignOutAsync(CancellationToken ct) => Task.CompletedTask;
}
