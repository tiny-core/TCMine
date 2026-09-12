using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Identity;

/// <summary>
///     O que responde enquanto o MSAL não existe.
///     Não é um bypass: não autentica ninguém, não fabrica token e não abre
///     brecha. Diz, com todas as letras, que esta build não sabe entrar — o que é
///     melhor que a alternativa, que seria a tela de login estourar com "nenhum
///     serviço registrado para IMicrosoftTokenProvider" na cara do jogador.
///     Desceu um degrau quando a cadeia foi dividida: antes ocupava o lugar do
///     autenticador inteiro, e com isso a cadeia Xbox/XSTS/Minecraft também
///     ficava por escrever. Hoje ela existe e é testada; o que falta é só o
///     primeiro salto.
/// </summary>
public sealed class PendingMicrosoftTokenProvider : IMicrosoftTokenProvider
{
    private const string Mensagem =
        "Esta versão do launcher ainda não faz login com a Microsoft. "
        + "Atualize assim que a próxima versão estiver disponível.";

    // Silencioso: no arranque não há o que dizer, e uma mensagem aqui apareceria
    // toda vez que o launcher abrisse.
    public Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
        Task.FromResult(AuthResult.NoStoredCredentials());

    public Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
        Task.FromResult(AuthResult.Unavailable(Mensagem));

    public Task SignOutAsync(CancellationToken ct) => Task.CompletedTask;
}
