using System.Collections.Concurrent;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Background;

/// <summary>
///     Última contagem conhecida de jogadores, por servidor.
///     Em memória e singleton: o valor vale por quinze segundos e se
///     reconstitui sozinho na coleta seguinte. Depois de um reinício do painel
///     todo servidor volta a "não sei" até a primeira amostragem, que é a
///     resposta certa — o painel acabou de subir e de fato não sabe.
/// </summary>
public sealed class PlayerCountCache : IPlayerCountSource
{
    private readonly ConcurrentDictionary<Guid, int> _contagens = new();

    public int? TryGet(Guid gameServerId) =>
        _contagens.TryGetValue(gameServerId, out var valor) ? valor : null;

    /// <summary>
    ///     Grava e diz se mudou. O retorno existe para o coletor só empurrar
    ///     evento quando há novidade: repetir "3 jogadores" a cada quinze
    ///     segundos para todo launcher conectado é tráfego que não informa nada.
    /// </summary>
    public bool Set(Guid gameServerId, int online)
    {
        var mudou = true;

        _contagens.AddOrUpdate(
            gameServerId,
            online,
            (_, anterior) =>
            {
                mudou = anterior != online;
                return online;
            });

        return mudou;
    }

    /// <summary>
    ///     Servidor parado ou contagem ilegível: volta a "não sei". Deixar o
    ///     último valor exibiria "5 jogadores" num servidor desligado.
    /// </summary>
    public void Forget(Guid gameServerId) => _contagens.TryRemove(gameServerId, out _);
}
