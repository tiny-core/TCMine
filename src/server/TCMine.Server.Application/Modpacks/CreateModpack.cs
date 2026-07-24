using System.Text.RegularExpressions;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Cria um modpack (o container; as versões vêm depois).
///     Um caso de uso por classe. Fica claro o que a operação precisa (as
///     dependências no construtor), o que recebe (o comando) e o que devolve —
///     e testar significa exercitar esta classe, não caçar um método no meio de
///     um serviço com vinte responsabilidades.
/// </summary>
public sealed partial class CreateModpack(
    IModpackRepository repository,
    ICurrentUserScope scope)
{
    public async Task<Result<Guid>> HandleAsync(CreateModpackCommand command, CancellationToken ct)
    {
        var slug = Normalize(command.Slug);

        if (!IsValidSlug(slug))
            return Result<Guid>.Fail(
                "O identificador deve ter de 3 a 64 caracteres, apenas letras minúsculas, números e hífen.");

        // Checagem amigável antes de tentar gravar. Não substitui o índice
        // único do banco — duas requisições simultâneas passariam as duas por
        // aqui — mas transforma o caso comum numa mensagem clara em vez de um
        // erro de constraint feio.
        if (await repository.SlugExistsAsync(slug, ct))
            return Result<Guid>.Fail($"Já existe um modpack com o identificador '{slug}'.");

        var modpack = new Modpack
        {
            OwnerId = scope.OwnerId,
            Slug = slug,
            Name = command.Name.Trim(),
            Summary = string.IsNullOrWhiteSpace(command.Summary) ? null : command.Summary.Trim()
        };

        repository.Add(modpack);
        await repository.SaveChangesAsync(ct);

        return Result<Guid>.Success(modpack.Id);
    }

    private static string Normalize(string slug)
    {
        return slug.Trim().ToLowerInvariant().Replace(' ', '-');
    }

    private static bool IsValidSlug(string slug)
    {
        return slug.Length is >= 3 and <= 64 && SlugPattern().IsMatch(slug);
    }

    // Source-generated: o regex é compilado uma vez, em build, em vez de
    // interpretado a cada chamada.
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}

/// <summary>Dados de entrada. Record separado deixa a assinatura estável.</summary>
public sealed record CreateModpackCommand(string Slug, string Name, string? Summary);