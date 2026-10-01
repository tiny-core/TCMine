namespace TCMine.Launcher.Core.Identity;

/// <summary>
///     Troca um código de convite pelo acesso a um servidor.
///     Existe porque o painel já sabia CRIAR convites, mas o launcher não tinha
///     como RESGATAR um: sem isto, uma conta nova nunca ganhava Membership
///     nenhuma, e a lista de servidores da tela inicial ficava vazia para
///     sempre — mesmo depois de o administrador convidar o jogador pelo nome.
/// </summary>
public sealed class RedeemInvite(ILauncherSessionApi api)
{
    public async Task<InviteRedeemResult> HandleAsync(Uri serverUrl, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return InviteRedeemResult.Failed("Informe o código do convite.");

        return await api.RedeemInviteAsync(serverUrl, code.Trim(), ct);
    }
}
