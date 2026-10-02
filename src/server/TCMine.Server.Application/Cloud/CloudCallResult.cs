namespace TCMine.Server.Application.Cloud;

/// <summary>
///     Resultado de uma chamada da API do mod. O <c>Result</c> do projeto só diz
///     sucesso/falha; aqui o servidor de jogo precisa saber O QUE fazer: corrigir
///     o pedido (nunca vai passar), tentar de novo (conflito de concorrência) ou
///     parar (sem permissão). Cada status vira um código HTTP no endpoint.
/// </summary>
public readonly record struct CloudCallResult<T>(CloudCallStatus Status, T? Value, string? Error)
{
    public static CloudCallResult<T> Ok(T value) => new(CloudCallStatus.Ok, value, null);

    public static CloudCallResult<T> Invalid(string error) => new(CloudCallStatus.Invalid, default, error);

    /// <summary>Outra requisição mexeu no mesmo lease: nada foi gravado, tente de novo.</summary>
    public static CloudCallResult<T> Conflict(string error) => new(CloudCallStatus.Conflict, default, error);

    public static CloudCallResult<T> Forbidden(string error) => new(CloudCallStatus.Forbidden, default, error);
}

public enum CloudCallStatus
{
    Ok,
    Invalid,
    Conflict,
    Forbidden
}
