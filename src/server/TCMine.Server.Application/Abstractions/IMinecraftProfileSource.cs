namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Verifica um token de acesso do Minecraft e devolve de quem ele é.
///     A verificação acontece aqui, no servidor, e não no launcher: o launcher
///     roda na máquina do jogador, então qualquer coisa que ele afirme sobre a
///     própria identidade é entrada não confiável. Quem apresenta um token que
///     a Mojang reconhece prova, ao mesmo tempo, que é dono da conta e que tem
///     o jogo — as duas perguntas que importam para deixar alguém entrar.
/// </summary>
public interface IMinecraftProfileSource
{
    /// <summary>
    ///     Nulo quando o token é inválido, expirado ou a conta não tem Minecraft.
    ///     Falha de rede ou indisponibilidade da Mojang sobe como exceção: negar
    ///     o login seria indistinguível de token inválido, e o jogador legítimo
    ///     ficaria de fora sem ninguém entender por quê.
    /// </summary>
    Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct);
}

/// <summary>
///     Identidade do jogador no jogo. O <paramref name="Uuid" /> vem sem hífens,
///     como a Mojang devolve, e é a chave estável: o nome de jogador pode ser
///     trocado a cada 30 dias, o UUID nunca muda.
/// </summary>
public sealed record MinecraftProfile(string Uuid, string Name);
