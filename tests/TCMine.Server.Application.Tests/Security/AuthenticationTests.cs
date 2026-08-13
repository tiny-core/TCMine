using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Security;

/// <summary>
///     Bug aqui não quebra uma tela: entrega o painel. Cada teste trava uma das
///     garantias — não vazar quem tem conta, não aceitar senha errada, não deixar
///     um link de recuperação virar chave permanente.
/// </summary>
public sealed class AuthenticationTests
{
    [Fact]
    public async Task Login_aceita_a_senha_certa()
    {
        var users = new FakeUsers(Usuario("ana@teste.com", "senha-boa"));

        var result = await new AuthenticateUser(users, new FakeHasher())
            .HandleAsync("ana@teste.com", "senha-boa", CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("ana@teste.com", result.Value!.Email);
    }

    [Fact]
    public async Task Login_recusa_senha_errada()
    {
        var users = new FakeUsers(Usuario("ana@teste.com", "senha-boa"));

        var result = await new AuthenticateUser(users, new FakeHasher())
            .HandleAsync("ana@teste.com", "outra-coisa", CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Login_da_a_mesma_mensagem_para_conta_inexistente_e_senha_errada()
    {
        // Mensagens diferentes transformariam a tela de login num verificador de
        // quem tem conta aqui — o primeiro passo de quem vai tentar adivinhar.
        var useCase = new AuthenticateUser(new FakeUsers(Usuario("ana@teste.com", "senha-boa")), new FakeHasher());

        var inexistente = await useCase.HandleAsync("ninguem@teste.com", "x", CancellationToken.None);
        var senhaErrada = await useCase.HandleAsync("ana@teste.com", "x", CancellationToken.None);

        Assert.Equal(inexistente.Error, senhaErrada.Error);
    }

    [Fact]
    public async Task Login_regrava_o_hash_quando_o_formato_envelheceu()
    {
        var user = Usuario("ana@teste.com", "senha-boa");
        var users = new FakeUsers(user);
        var hasher = new FakeHasher { Rehash = true };

        await new AuthenticateUser(users, hasher).HandleAsync("ana@teste.com", "senha-boa", CancellationToken.None);

        // Aproveita que a senha em claro está em mãos: é a única hora em que dá
        // para migrar o formato sem pedir nada ao usuário.
        Assert.StartsWith("hash:", user.PasswordHash);
        Assert.True(users.Atualizado);
    }

    [Fact]
    public async Task Primeiro_admin_so_pode_ser_criado_enquanto_nao_ha_ninguem()
    {
        // Sem esta guarda, a rota de setup deixaria qualquer visitante virar
        // administrador da instalação a qualquer momento.
        var users = new FakeUsers(Usuario("ja@existe.com", "x"));

        var result = await new CreateFirstAdmin(users, new FakeHasher())
            .HandleAsync("novo@teste.com", "Novo", "senha-longa", CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Primeiro_admin_recusa_senha_curta()
    {
        var result = await new CreateFirstAdmin(new FakeUsers(), new FakeHasher())
            .HandleAsync("novo@teste.com", "Novo", "curta", CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Primeiro_admin_normaliza_o_email_e_marca_como_admin()
    {
        var users = new FakeUsers();

        var result = await new CreateFirstAdmin(users, new FakeHasher())
            .HandleAsync("  Ana@Teste.COM ", " Ana ", "senha-longa", CancellationToken.None);

        Assert.True(result.Succeeded);

        // E-mail guardado em minúsculas: senão "Ana@" e "ana@" viram duas contas
        // e o login por uma delas falha sem explicação.
        Assert.Equal("ana@teste.com", users.Adicionado!.Email);
        Assert.Equal("Ana", users.Adicionado.DisplayName);
        Assert.True(users.Adicionado.IsInstanceAdmin);
    }

    [Fact]
    public async Task Trocar_senha_exige_a_atual()
    {
        // Impede que alguém com a sessão aberta (máquina destravada, cookie
        // roubado) tome a conta trocando a senha.
        var user = Usuario("ana@teste.com", "atual");
        var users = new FakeUsers(user);

        var result = await new ChangePassword(users, new FakeHasher())
            .HandleAsync(user.Id, "chute-errado", "nova-senha-longa", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("hash:atual", user.PasswordHash);
    }

    [Fact]
    public async Task Trocar_senha_invalida_link_de_recuperacao_em_aberto()
    {
        var user = Usuario("ana@teste.com", "atual");
        user.PasswordResetTokenHash = "qualquer";
        user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);

        var result = await new ChangePassword(new FakeUsers(user), new FakeHasher())
            .HandleAsync(user.Id, "atual", "nova-senha-longa", CancellationToken.None);

        Assert.True(result.Succeeded);

        // Um link vivo depois da troca seria uma porta extra para a conta.
        Assert.Null(user.PasswordResetTokenHash);
        Assert.Null(user.PasswordResetTokenExpiresAt);
    }

    [Fact]
    public async Task Pedir_recuperacao_para_email_inexistente_responde_sucesso_sem_mandar_nada()
    {
        var email = new FakeEmail();

        var result = await new RequestPasswordReset(new FakeUsers(), email)
            .HandleAsync("ninguem@teste.com", "https://painel/reset?t={token}", CancellationToken.None);

        // Responder "não encontrado" faria desta tela um verificador de contas.
        Assert.True(result.Succeeded);
        Assert.Empty(email.Enviados);
    }

    [Fact]
    public async Task Pedir_recuperacao_guarda_só_o_hash_do_token_e_manda_o_link()
    {
        var user = Usuario("ana@teste.com", "atual");
        var email = new FakeEmail();

        await new RequestPasswordReset(new FakeUsers(user), email)
            .HandleAsync("ana@teste.com", "https://painel/reset?t={token}", CancellationToken.None);

        var enviado = Assert.Single(email.Enviados);
        Assert.Contains("https://painel/reset?t=", enviado.Body, StringComparison.Ordinal);

        // O que fica no banco não pode servir como link: vazamento do banco não
        // pode virar acesso às contas.
        Assert.NotNull(user.PasswordResetTokenHash);
        Assert.DoesNotContain(user.PasswordResetTokenHash!, enviado.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reset_funciona_com_o_token_do_email_e_so_uma_vez()
    {
        var user = Usuario("ana@teste.com", "antiga");
        var users = new FakeUsers(user);
        var email = new FakeEmail();

        await new RequestPasswordReset(users, email)
            .HandleAsync("ana@teste.com", "T:{token}", CancellationToken.None);

        var token = email.Enviados[0].Body.Split("T:")[1].Split('\n')[0].Trim();
        var useCase = new ResetPassword(users, new FakeHasher());

        var primeira = await useCase.HandleAsync("ana@teste.com", token, "nova-senha-longa", CancellationToken.None);
        Assert.True(primeira.Succeeded);
        Assert.Equal("hash:nova-senha-longa", user.PasswordHash);

        // Uso único: o mesmo link não pode redefinir a senha de novo amanhã.
        var segunda = await useCase.HandleAsync("ana@teste.com", token, "outra-senha-longa", CancellationToken.None);
        Assert.False(segunda.Succeeded);
    }

    [Fact]
    public async Task Reset_recusa_token_expirado()
    {
        var user = Usuario("ana@teste.com", "antiga");
        user.PasswordResetTokenHash = RequestPasswordReset.HashToken("token-valido");
        user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var result = await new ResetPassword(new FakeUsers(user), new FakeHasher())
            .HandleAsync("ana@teste.com", "token-valido", "nova-senha-longa", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("hash:antiga", user.PasswordHash);
    }

    [Fact]
    public async Task Reset_recusa_token_errado()
    {
        var user = Usuario("ana@teste.com", "antiga");
        user.PasswordResetTokenHash = RequestPasswordReset.HashToken("token-certo");
        user.PasswordResetTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1);

        var result = await new ResetPassword(new FakeUsers(user), new FakeHasher())
            .HandleAsync("ana@teste.com", "token-chutado", "nova-senha-longa", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("hash:antiga", user.PasswordHash);
    }

    // ---- Fixtures ----

    private static User Usuario(string email, string senha) => new()
    {
        Email = email,
        DisplayName = "Ana",
        PasswordHash = $"hash:{senha}"
    };

    // ---- Fakes ----

    /// <summary>Hash previsível: "hash:{senha}". Deixa a asserção legível sem PBKDF2.</summary>
    private sealed class FakeHasher : IPasswordHasher
    {
        public bool Rehash { get; init; }

        public string Hash(string password) => $"hash:{password}";

        public PasswordVerification Verify(string hash, string password) =>
            hash != $"hash:{password}"
                ? PasswordVerification.Failed
                : Rehash
                    ? PasswordVerification.SuccessRehashNeeded
                    : PasswordVerification.Success;
    }

    private sealed class FakeUsers(params User[] seed) : IUserRepository
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
            throw new NotImplementedException();

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

    private sealed class FakeEmail : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Enviados { get; } = [];

        public Task SendAsync(string to, string subject, string body, CancellationToken ct)
        {
            Enviados.Add((to, subject, body));
            return Task.CompletedTask;
        }
    }
}
