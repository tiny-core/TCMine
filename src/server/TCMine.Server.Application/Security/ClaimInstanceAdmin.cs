using System.Security.Cryptography;
using System.Text;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Security;

/// <summary>
///     Código de uso único para reaver a administração da instalação.
///     Vive só em memória, de propósito: quem o recebe é quem lê o log do
///     servidor — ou seja, quem opera a máquina —, e um arranque novo emite
///     outro. Gravá-lo no banco criaria uma credencial que sobrevive a quem a
///     pediu. Guarda o hash, não o código.
/// </summary>
public sealed class AdminClaimCode
{
    private readonly Lock _gate = new();
    private byte[]? _hash;

    public bool IsOpen
    {
        get
        {
            lock (_gate)
                return _hash is not null;
        }
    }

    /// <summary>Emite um código novo, invalidando o anterior.</summary>
    public string Issue()
    {
        var code = SecureToken.GenerateCode();
        lock (_gate)
            _hash = HashOf(code);
        return code;
    }

    /// <summary>Confere e, se bater, gasta o código. Comparação em tempo constante.</summary>
    public bool TryConsume(string code)
    {
        var candidate = HashOf(code);
        lock (_gate)
        {
            if (_hash is null || !CryptographicOperations.FixedTimeEquals(_hash, candidate))
                return false;

            _hash = null;
            return true;
        }
    }

    private static byte[] HashOf(string code) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(SecureToken.NormalizeCode(code)));
}

/// <summary>
///     Devolve a administração a quem opera o servidor quando nenhum admin
///     consegue mais entrar.
///     O caso real: as contas de e-mail e senha deixaram de existir quando o
///     login passou a ser só pela Microsoft. O admin antigo, sem Microsoft (e
///     muitas vezes sem Minecraft vinculado), entrava e ganhava uma conta NOVA —
///     sem papel, porque a instalação "já tinha usuários". Ninguém mais
///     conseguia promover ninguém.
///     A prova de que é o dono não pode ser "foi o primeiro a entrar": a página
///     de login é pública, e qualquer conta Microsoft chega até ela. A prova é o
///     código que só aparece no log do servidor.
/// </summary>
public sealed class ClaimInstanceAdmin(IUserRepository users, AdminClaimCode codes)
{
    /// <summary>
    ///     Há usuários, mas nenhum admin que consiga entrar (todo admin está sem
    ///     identidade Microsoft)? É a única situação em que o código é emitido.
    /// </summary>
    public async Task<bool> IsNeededAsync(CancellationToken ct)
    {
        var all = await users.ListAsync(ct);
        return all.Count > 0 && !all.Any(u => u.IsInstanceAdmin && u.MicrosoftObjectId is not null);
    }

    public async Task<Result<User>> HandleAsync(Guid userId, string code, CancellationToken ct)
    {
        if (!await IsNeededAsync(ct))
            return Result<User>.Fail("A instalação já tem um administrador que consegue entrar.");

        if (string.IsNullOrWhiteSpace(code) || !codes.TryConsume(code))
            return Result<User>.Fail("Código inválido. Ele aparece no log do servidor a cada arranque.");

        var user = await users.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<User>.Fail("Usuário não encontrado.");

        // Um único admin antigo é o próprio dono voltando: funde, e os modpacks,
        // servidores e acessos dele passam para a conta nova. Com mais de um não
        // dá para saber quem é quem — promove só quem resgatou, e os outros ficam
        // para a página de Usuários.
        var legacy = (await users.ListAsync(ct))
            .Where(u => u.IsInstanceAdmin && u.MicrosoftObjectId is null && u.Id != userId)
            .ToList();

        if (legacy is [var previous])
        {
            await users.MergeAsync(user.Id, previous.Id, ct);

            // Depois da fusão: a absorvida já não ocupa o índice único do UUID.
            user.MinecraftUuid ??= previous.MinecraftUuid;
        }

        user.IsInstanceAdmin = true;
        await users.UpdateAsync(user, ct);

        return Result<User>.Success(user);
    }
}
