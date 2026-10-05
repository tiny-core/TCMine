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

    /// <summary>
    ///     Pico do dia (UTC) por servidor. Mesmo tratamento do resto desta
    ///     classe: em memória, reinício zera — e aqui isso é honesto duas
    ///     vezes, porque o pico de ANTES de ontem já não importaria mesmo.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, (DateOnly Dia, int Pico)> _picos = new();

    public int? TryGet(Guid gameServerId) =>
        _contagens.TryGetValue(gameServerId, out var valor) ? valor : null;

    public int? PeakToday(Guid gameServerId)
    {
        if (!_picos.TryGetValue(gameServerId, out var entrada))
            return null;

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        return entrada.Dia == hoje ? entrada.Pico : null;
    }

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

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        _picos.AddOrUpdate(
            gameServerId,
            (hoje, online),
            (_, atual) => atual.Dia == hoje ? (hoje, Math.Max(atual.Pico, online)) : (hoje, online));

        return mudou;
    }

    /// <summary>
    ///     Servidor parado ou contagem ilegível: volta a "não sei". Deixar o
    ///     último valor exibiria "5 jogadores" num servidor desligado.
    ///     O PICO do dia não some aqui de propósito: o servidor ter parado não
    ///     desfaz quantos jogadores ele teve hoje.
    /// </summary>
    public void Forget(Guid gameServerId) => _contagens.TryRemove(gameServerId, out _);
}
