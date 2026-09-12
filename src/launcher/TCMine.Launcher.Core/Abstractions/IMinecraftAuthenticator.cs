namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     A cadeia inteira, da conta Microsoft ao token do Minecraft.
///     Continua sendo porta por um motivo que sobreviveu à divisão com
///     <see cref="IMicrosoftTokenProvider" />: em Debug, o host substitui esta
///     implementação por uma que lê um token real do ambiente, e assim quem
///     desenvolve não refaz o fluxo interativo a cada execução.
///     O launcher NÃO guarda o token que sai daqui: ele vale uma vez, para provar
///     identidade ao servidor, e a sessão que conta a partir daí é o cookie que o
///     servidor devolve. Também não guardamos o refresh token da Microsoft à
///     mão — o cache do MSAL é quem o mantém, porque um public client não tem API
///     para reinjetar um refresh token que tenhamos guardado por fora.
/// </summary>
public interface IMinecraftAuthenticator
{
    /// <summary>Sem interação, a partir do que estiver guardado.</summary>
    Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct);

    /// <summary>Abre o fluxo interativo.</summary>
    Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct);

    /// <summary>Descarta o que estiver guardado nesta máquina.</summary>
    Task SignOutAsync(CancellationToken ct);
}
