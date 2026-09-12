namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Prova a conta Microsoft do jogador e devolve o access token dela.
///     Esta é a única parte da autenticação que NÃO é portável: o MSAL com
///     broker é Windows-only, e é por isso que ela está atrás de porta. Tudo o
///     que vem depois — Xbox Live, XSTS, Minecraft Services — é HTTPS comum e
///     vive na infraestrutura portável, onde um port para Linux o reaproveita
///     inteiro em vez de reescrevê-lo.
///     O token que sai daqui é a credencial mais sensível do launcher e não é
///     guardado: serve uma vez, para o degrau seguinte. O que persiste é o cache
///     do MSAL, gerido por ele — ver a nota sobre isso em
///     <see cref="IMinecraftAuthenticator" />.
/// </summary>
public interface IMicrosoftTokenProvider
{
    /// <summary>
    ///     Tenta reautenticar sem interação, a partir do que estiver em cache.
    ///     É o que faz o jogador abrir o launcher e já estar dentro. Não ter
    ///     credencial guardada é resultado normal, não erro.
    /// </summary>
    Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct);

    /// <summary>Abre o fluxo interativo (navegador do sistema ou broker).</summary>
    Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct);

    /// <summary>Descarta o que estiver guardado nesta máquina.</summary>
    Task SignOutAsync(CancellationToken ct);
}
