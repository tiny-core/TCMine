namespace TCMine.Launcher.Core.Sync;

/// <summary>
///     Quantas cópias de mundo guardar, e quais deixar cair.
///     Sem isto elas acumulavam sem teto: cada atualização de uma instância com
///     mundo cria um .zip, e um pack que atualiza toda semana enche o disco com
///     estados que ninguém vai abrir.
///     A regra é pura e vive aqui porque é uma DECISÃO — o que se perde e o que
///     se guarda —, e decisões dessas merecem teste. Apagar ficheiro é da
///     infraestrutura.
/// </summary>
public static class WorldBackupRetention
{
    /// <summary>
    ///     Quantas manter por instância.
    ///     Cinco cobre alguns rollbacks seguidos sem deixar dezenas de gigabytes
    ///     para trás — o mesmo número que o painel usa do lado do servidor, e
    ///     pela mesma razão. Constante, e não configuração: um número que
    ///     ninguém vai mudar não justifica uma tela, e o dia em que justificar
    ///     ele sai daqui sem partir nada.
    /// </summary>
    public const int Keep = 5;

    /// <summary>
    ///     As cópias que sobram, das mais antigas.
    ///     Recebe nomes de ficheiro e ordena-os ao contrário: o formato é
    ///     <c>saves-AAAAMMDD-HHMMSS.zip</c>, então a ordem alfabética É a
    ///     cronológica — e depender dela em vez da data do sistema de ficheiros
    ///     é o que torna isto testável e imune a uma cópia com data mexida.
    ///     A mais recente nunca entra na lista, o que importa mais do que parece:
    ///     esta função corre logo a seguir a criar uma, e apagá-la seria
    ///     destruir a cópia que a atualização acabou de exigir.
    /// </summary>
    public static IReadOnlyList<string> Expired(IEnumerable<string> fileNames, int keep = Keep) =>
        keep <= 0
            ? []
            : [.. fileNames.OrderByDescending(n => n, StringComparer.Ordinal).Skip(keep)];
}
