namespace TCMine.Server.Application.Common;

/// <summary>
///     Uma página de resultados mais o total, para a tabela saber quantas
///     páginas existem sem ter carregado tudo.
///     Existe porque "trazer a lista inteira e paginar na tela" só parece
///     funcionar até um pack importado entrar: 471 mods numa versão e centenas
///     no inventário viram milhares de componentes vivos no circuito, e o
///     usuário paga o custo de 500 linhas para ver 25.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount)
{
}

/// <summary>
///     Recorte pedido pela tabela. <paramref name="Page" /> é base zero, como o
///     MudTable entrega.
/// </summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public int Skip => Page * PageSize;
}

/// <summary>
///     Uma página de uma lista que só cresce (histórico), do mais novo para o
///     mais velho. <paramref name="Next" /> é o cursor da página seguinte (o
///     <c>Id</c> da última linha desta), ou nulo quando não há mais nada.
///     Cursor, e não "página N de M": pular as N primeiras linhas e contar o
///     total custam uma varredura que cresce com o histórico, enquanto "as 50
///     anteriores a este Id" vai direto ao ponto pelo índice — a página 1000
///     custa o mesmo que a primeira.
/// </summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, Guid? Next);

/// <summary>Monta um <see cref="CursorPage{T}" /> (fora do tipo genérico: CA1000).</summary>
public static class CursorPage
{
    /// <summary>
    ///     Corta as <paramref name="pageSize" /> primeiras de uma consulta que
    ///     trouxe UMA a mais: a sobra só diz que existe página seguinte.
    /// </summary>
    public static CursorPage<T> From<T>(IReadOnlyList<T> fetched, int pageSize, Func<T, Guid> idOf) =>
        fetched.Count > pageSize
            ? new CursorPage<T>([.. fetched.Take(pageSize)], idOf(fetched[pageSize - 1]))
            : new CursorPage<T>(fetched, null);
}
