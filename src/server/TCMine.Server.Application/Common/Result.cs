namespace TCMine.Server.Application.Common;

/// <summary>
///     Resultado de uma operação que pode falhar por regra de negócio.
///     Exceção fica reservada para o que é de fato excepcional — banco fora do
///     ar, bug. "Slug duplicado" é resultado previsto e esperado, então não
///     merece exceção: o custo de stack trace e o ruído no fluxo não se
///     justificam para algo que a UI vai simplesmente mostrar ao usuário.
/// </summary>
public readonly record struct Result
{
    public bool Succeeded { get; private init; }
    public string? Error { get; private init; }

    public static Result Success()
    {
        return new Result { Succeeded = true };
    }

    public static Result Fail(string error)
    {
        return new Result { Succeeded = false, Error = error };
    }
}

/// <summary>Resultado que também carrega um valor em caso de sucesso.</summary>
public readonly record struct Result<T>
{
    public bool Succeeded { get; private init; }
    public T? Value { get; private init; }
    public string? Error { get; private init; }

    public static Result<T> Success(T value)
    {
        return new Result<T> { Succeeded = true, Value = value };
    }

    public static Result<T> Fail(string error)
    {
        return new Result<T> { Succeeded = false, Error = error };
    }
}