using System.Text.RegularExpressions;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Modpacks;

/// <summary>
///     Cria um modpack (o container; as versões vêm depois).
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

        // Checagem amigável antes de gravar. Não substitui o índice único do
        // banco — duas requisições simultâneas passariam as duas por aqui —
        // mas transforma o caso comum numa mensagem clara.
        if (await repository.SlugExistsAsync(slug, ct))
            return Result<Guid>.Fail($"Já existe um modpack com o identificador '{slug}'.");

        if (string.IsNullOrWhiteSpace(command.MinecraftVersion))
            return Result<Guid>.Fail("Informe a versão do Minecraft.");

        var modpack = new Modpack
        {
            OwnerId = scope.OwnerId,
            Slug = slug,
            Name = command.Name.Trim(),
            Summary = string.IsNullOrWhiteSpace(command.Summary) ? null : command.Summary.Trim(),
            MinecraftVersion = command.MinecraftVersion.Trim(),
            Loader = command.Loader
        };

        await repository.CreateAsync(modpack, ct);

        return Result<Guid>.Success(modpack.Id);
    }

    // Normaliza para minúsculas e troca espaços por hífen antes de validar.
    private static string Normalize(string slug)
    {
        return slug.Trim().ToLowerInvariant().Replace(' ', '-');
    }

    private static bool IsValidSlug(string slug)
    {
        return slug.Length is >= 3 and <= 64 && SlugPattern().IsMatch(slug);
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}

public sealed record CreateModpackCommand(
    string Slug,
    string Name,
    string? Summary,
    string MinecraftVersion,
    ModLoader Loader);