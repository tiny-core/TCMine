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
    public static PagedResult<T> Empty { get; } = new([], 0);
}

/// <summary>
///     Recorte pedido pela tabela. <paramref name="Page" /> é base zero, como o
///     MudTable entrega.
/// </summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public int Skip => Page * PageSize;
}
