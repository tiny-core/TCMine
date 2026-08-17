using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Repositório de usuários em memória.
///     Devolve a MESMA instância que recebeu na semente, e não uma cópia: é isso
///     que faz uma alteração do caso de uso ficar visível para o teste sem
///     precisar espiar o que foi passado ao <c>UpdateAsync</c>.
/// </summary>
internal sealed class FakeUsers(params User[] seed) : IUserRepository
{
    private readonly List<User> _users = [.. seed];

    public User? Adicionado { get; private set; }
    public bool Atualizado { get; private set; }

    public Task<bool> AnyAsync(CancellationToken ct) => Task.FromResult(_users.Count > 0);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) =>
        Task.FromResult(_users.FirstOrDefault(u =>
            string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<User?> GetByMicrosoftObjectIdAsync(string objectId, CancellationToken ct) =>
        Task.FromResult(_users.FirstOrDefault(u => u.MicrosoftObjectId == objectId));

    public Task<User?> GetByMinecraftUuidAsync(string uuid, CancellationToken ct) =>
        Task.FromResult(_users.FirstOrDefault(u =>
            string.Equals(u.MinecraftUuid, uuid, StringComparison.OrdinalIgnoreCase)));

    public Task AddAsync(User user, CancellationToken ct)
    {
        Adicionado = user;
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken ct)
    {
        Atualizado = true;
        return Task.CompletedTask;
    }
}
