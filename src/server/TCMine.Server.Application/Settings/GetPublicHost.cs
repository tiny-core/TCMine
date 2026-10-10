using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Settings;

/// <summary>
///     O host público desta instalação: o que o admin gravou ou, na falta dele,
///     o IP detectado.
///     Num lugar só porque a precedência é regra — o que foi escrito vence o que
///     foi detectado — e ela é consultada por quatro bordas: o hub que entrega os
///     servidores ao launcher, as duas listas de servidores do painel e o
///     formulário. Cada uma decidindo sozinha, uma delas acabaria mostrando um
///     endereço que o launcher não recebe.
/// </summary>
public sealed class GetPublicHost(ISettingsRepository settings, IPublicAddressProvider detector)
{
    /// <param name="alwaysDetect">
    ///     Detectar mesmo havendo host gravado. Só a tela de configurações pede:
    ///     ela mostra os dois lado a lado. Para todo o resto, um host gravado
    ///     dispensa a ida à rede.
    /// </param>
    public async Task<PublicHostResult> HandleAsync(bool alwaysDetect, CancellationToken ct)
    {
        var stored = (await settings.GetAsync(ct)).PublicHost;
        var configured = string.IsNullOrWhiteSpace(stored) ? null : stored.Trim();

        var detected = configured is null || alwaysDetect
            ? await detector.GetAsync(ct)
            : null;

        return new PublicHostResult(configured, detected);
    }
}

/// <param name="Configured">O que está gravado nas configurações. Nulo = nada.</param>
/// <param name="Detected">O IP detectado. Nulo = não se detectou, ou não foi preciso.</param>
public sealed record PublicHostResult(string? Configured, string? Detected)
{
    /// <summary>O que vale: o gravado e, na falta dele, o detectado.</summary>
    public string? Effective => Configured ?? Detected;
}
