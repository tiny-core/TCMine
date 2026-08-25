using TCMine.Contracts.Servers;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Tests.Servers;

/// <summary>
///     O convite é o que transforma uma conta autenticada em alguém com acesso.
///     Errar aqui não quebra uma tela: entrega um servidor. Cada teste trava uma
///     das garantias — não conceder mais do que foi convidado, não deixar um
///     código servir duas vezes, e não permitir que quem gerencia membros use o
///     mecanismo para se livrar de quem o convidou.
/// </summary>
public sealed class InviteTests
{
    private static readonly Guid ServidorId = Guid.CreateVersion7();

    [Fact]
    public async Task Convite_criado_devolve_o_codigo_uma_vez_e_guarda_so_o_hash()
    {
        var invites = new FakeInvites();

        var result = await new CreateInvite(invites, new FakeUserScope())
            .HandleAsync(ServidorId, ServerRoleDto.Moderator, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        var codigo = result.Value!;

        // O que fica gravado não pode servir como convite: banco vazado não
        // pode virar acesso aos servidores.
        invites.Adicionado.ShouldNotBeNull();
        invites.Adicionado.CodeHash.ShouldNotBe(codigo);
        invites.Adicionado.CodeHash.ShouldBe(SecureToken.Hash(SecureToken.NormalizeCode(codigo)));
        invites.Adicionado.Role.ShouldBe(ServerRole.Moderator);
    }

    [Fact]
    public async Task Convite_nao_concede_o_papel_de_dono()
    {
        // Quem recebesse Owner por link poderia remover quem o convidou.
        var invites = new FakeInvites();

        var result = await new CreateInvite(invites, new FakeUserScope())
            .HandleAsync(ServidorId, ServerRoleDto.Owner, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        invites.Adicionado.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ServerRoleDto.Moderator)]
    [InlineData(ServerRoleDto.Admin)]
    public async Task Abaixo_de_Owner_ninguem_convida(ServerRoleDto? papel)
    {
        // Admin opera o servidor; conceder acesso é decisão de dono. Se Admin
        // pudesse convidar, poderia convidar a si mesmo para outro papel.
        var invites = new FakeInvites();

        var result = await new CreateInvite(invites, new FakeUserScope(papel))
            .HandleAsync(ServidorId, ServerRoleDto.Member, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        invites.Adicionado.ShouldBeNull();
    }

    [Fact]
    public async Task Resgate_cria_o_vinculo_com_o_papel_do_convite()
    {
        var (codigo, invite) = NovoConvite(ServerRole.Moderator);
        var memberships = new FakeMemberships();
        var jogador = Guid.CreateVersion7();

        var result = await new RedeemInvite(
                new FakeInvites(invite), memberships, new FakeWhitelistSync(),
                new FakeUserScope(null) { UserId = jogador })
            .HandleAsync(codigo, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        memberships.Adicionado.ShouldNotBeNull();
        memberships.Adicionado.UserId.ShouldBe(jogador);
        memberships.Adicionado.GameServerId.ShouldBe(ServidorId);
        memberships.Adicionado.Role.ShouldBe(ServerRole.Moderator);
    }

    [Fact]
    public async Task Codigo_serve_uma_vez_so()
    {
        var (codigo, invite) = NovoConvite(ServerRole.Member);
        var invites = new FakeInvites(invite);

        var primeira = await new RedeemInvite(invites, new FakeMemberships(), new FakeWhitelistSync(), Jogador())
            .HandleAsync(codigo, TestContext.Current.CancellationToken);

        // Outra pessoa, mesmo código: um convite que serve duas vezes deixaria
        // de haver como saber quem entrou por ele.
        var segunda = await new RedeemInvite(invites, new FakeMemberships(), new FakeWhitelistSync(), Jogador())
            .HandleAsync(codigo, TestContext.Current.CancellationToken);

        primeira.Succeeded.ShouldBeTrue();
        segunda.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Convite_expirado_nao_serve()
    {
        var codigo = SecureToken.GenerateCode();
        var invite = Convite(codigo, ServerRole.Member, DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = await new RedeemInvite(new FakeInvites(invite), new FakeMemberships(), new FakeWhitelistSync(), Jogador())
            .HandleAsync(codigo, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Convite_revogado_nao_serve()
    {
        var (codigo, invite) = NovoConvite(ServerRole.Member);
        invite.Revoke(DateTimeOffset.UtcNow);

        var result = await new RedeemInvite(new FakeInvites(invite), new FakeMemberships(), new FakeWhitelistSync(), Jogador())
            .HandleAsync(codigo, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Codigo_inexistente_e_expirado_dao_a_mesma_resposta()
    {
        // Diferenciar permitiria varrer códigos: saber que um existe, ainda que
        // expirado, já diz que o formato e o alfabeto estão certos.
        var codigo = SecureToken.GenerateCode();
        var expirado = Convite(codigo, ServerRole.Member, DateTimeOffset.UtcNow.AddMinutes(-1));

        var inexistente = await new RedeemInvite(new FakeInvites(), new FakeMemberships(), new FakeWhitelistSync(), Jogador())
            .HandleAsync(SecureToken.GenerateCode(), TestContext.Current.CancellationToken);

        var vencido = await new RedeemInvite(new FakeInvites(expirado), new FakeMemberships(), new FakeWhitelistSync(), Jogador())
            .HandleAsync(codigo, TestContext.Current.CancellationToken);

        vencido.Error.ShouldBe(inexistente.Error);
    }

    [Fact]
    public async Task Codigo_e_aceito_sem_hifens_e_em_minusculas()
    {
        // O código é lido de uma mensagem e digitado à mão. Recusar por causa
        // da caixa só geraria suporte.
        var (codigo, invite) = NovoConvite(ServerRole.Member);
        var digitado = codigo.Replace("-", "").ToLowerInvariant();

        var result = await new RedeemInvite(new FakeInvites(invite), new FakeMemberships(), new FakeWhitelistSync(), Jogador())
            .HandleAsync(digitado, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Convite_promove_mas_nunca_rebaixa_quem_ja_e_membro()
    {
        var jogador = Guid.CreateVersion7();
        var (codigo, invite) = NovoConvite(ServerRole.Member);

        var existente = new Membership
        {
            UserId = jogador,
            GameServerId = ServidorId,
            Role = ServerRole.Admin
        };

        var result = await new RedeemInvite(
                new FakeInvites(invite),
                new FakeMemberships(existente),
                new FakeWhitelistSync(),
                new FakeUserScope(null) { UserId = jogador })
            .HandleAsync(codigo, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();

        // Usar um convite de Member sem querer não pode custar o papel de Admin.
        existente.Role.ShouldBe(ServerRole.Admin);
    }

    [Fact]
    public async Task Ninguem_remove_o_proprio_acesso()
    {
        // Deixaria o servidor sem quem o gerencie, e não há caminho de volta.
        var eu = Guid.CreateVersion7();
        var memberships = new FakeMemberships(new Membership
        {
            UserId = eu,
            GameServerId = ServidorId,
            Role = ServerRole.Owner
        });

        var result = await new RemoveMember(memberships, new FakeNotifier(), new FakeWhitelistSync(), new FakeUserScope { UserId = eu })
            .HandleAsync(ServidorId, eu, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        memberships.Removido.ShouldBeNull();
    }

    [Fact]
    public async Task Dono_do_servidor_nao_pode_ser_removido_nem_rebaixado()
    {
        var dono = Guid.CreateVersion7();
        var membership = new Membership
        {
            UserId = dono,
            GameServerId = ServidorId,
            Role = ServerRole.Owner
        };

        var remover = await new RemoveMember(new FakeMemberships(membership), new FakeNotifier(), new FakeWhitelistSync(), new FakeUserScope())
            .HandleAsync(ServidorId, dono, TestContext.Current.CancellationToken);

        var rebaixar = await new ChangeMemberRole(new FakeMemberships(membership), new FakeNotifier(), new FakeUserScope())
            .HandleAsync(ServidorId, dono, ServerRoleDto.Member, TestContext.Current.CancellationToken);

        remover.Succeeded.ShouldBeFalse();
        rebaixar.Succeeded.ShouldBeFalse();
        membership.Role.ShouldBe(ServerRole.Owner);
    }

    [Fact]
    public async Task Revogar_convite_ja_usado_nao_tira_o_acesso_de_ninguem()
    {
        // Revogar não desfaz o vínculo criado: quem já entrou continua membro,
        // e tirar o acesso é remover o membro. Confundir os dois faria o admin
        // pensar que revogou um acesso que segue de pé.
        var (_, invite) = NovoConvite(ServerRole.Member);
        invite.Redeem(Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        var result = await new RevokeInvite(new FakeInvites(invite), new FakeUserScope())
            .HandleAsync(invite.Id, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.Error!.ShouldContain("Remova o membro");
    }

    [Fact]
    public async Task Lista_de_acesso_esconde_convites_que_nao_servem_mais()
    {
        // Convite usado ou vencido não tem ação possível: listá-lo encheria a
        // tela de linhas sobre as quais não há o que fazer, e sugeriria acesso
        // pendente onde não há.
        var (_, pendente) = NovoConvite(ServerRole.Member);
        var (_, usado) = NovoConvite(ServerRole.Admin);
        usado.Redeem(Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        var vencido = Convite(SecureToken.GenerateCode(), ServerRole.Member,
            DateTimeOffset.UtcNow.AddMinutes(-1));

        var result = await new ListServerAccess(
                new FakeMemberships(), new FakeInvites(pendente, usado, vencido), new FakeUserScope())
            .HandleAsync(ServidorId, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Value!.PendingInvites.Select(i => i.Id).ShouldBe([pendente.Id]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ServerRoleDto.Moderator)]
    [InlineData(ServerRoleDto.Admin)]
    public async Task Abaixo_de_Owner_ninguem_ve_a_lista_de_membros(ServerRoleDto? papel)
    {
        // Leitura, mas não pública: a lista diz quem joga aqui, e os convites
        // pendentes revelam quem foi chamado e com que papel.
        var result = await new ListServerAccess(
                new FakeMemberships(), new FakeInvites(), new FakeUserScope(papel))
            .HandleAsync(ServidorId, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public async Task Remover_membro_avisa_que_ele_perdeu_o_acesso()
    {
        // O aviso é o que tira as conexões dele do console AGORA. Sem ele, o
        // rebaixamento só valeria quando o jogador resolvesse reconectar — ou
        // seja, nunca, enquanto estivesse lendo o que não deveria.
        var alvo = Guid.CreateVersion7();
        var notifier = new FakeNotifier();
        var memberships = new FakeMemberships(new Membership
        {
            UserId = alvo,
            GameServerId = ServidorId,
            Role = ServerRole.Moderator
        });

        var result = await new RemoveMember(memberships, notifier, new FakeWhitelistSync(), new FakeUserScope())
            .HandleAsync(ServidorId, alvo, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();

        var aviso = notifier.PapeisAvisados.ShouldHaveSingleItem();
        aviso.UserId.ShouldBe(alvo);
        aviso.ServerId.ShouldBe(ServidorId);

        // Nulo, e não Member: ele não virou membro comum, ficou sem acesso.
        aviso.Role.ShouldBeNull();
    }

    [Fact]
    public async Task Rebaixar_membro_avisa_com_o_papel_novo()
    {
        var alvo = Guid.CreateVersion7();
        var notifier = new FakeNotifier();
        var memberships = new FakeMemberships(new Membership
        {
            UserId = alvo,
            GameServerId = ServidorId,
            Role = ServerRole.Admin
        });

        var result = await new ChangeMemberRole(memberships, notifier, new FakeUserScope())
            .HandleAsync(ServidorId, alvo, ServerRoleDto.Member, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        notifier.PapeisAvisados.ShouldHaveSingleItem().Role.ShouldBe(ServerRoleDto.Member);
    }

    [Fact]
    public async Task Recusa_de_gerenciamento_nao_avisa_ninguem()
    {
        // Um aviso disparado numa operação recusada expulsaria do console
        // alguém cujo papel não mudou.
        var notifier = new FakeNotifier();

        await new RemoveMember(new FakeMemberships(), notifier, new FakeWhitelistSync(),
                new FakeUserScope(ServerRoleDto.Admin))
            .HandleAsync(ServidorId, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        notifier.PapeisAvisados.ShouldBeEmpty();
    }

    private static FakeUserScope Jogador() =>
        new(null) { UserId = Guid.CreateVersion7() };

    private static (string Codigo, Invite Convite) NovoConvite(ServerRole role)
    {
        var codigo = SecureToken.GenerateCode();
        return (codigo, Convite(codigo, role, DateTimeOffset.UtcNow.AddDays(7)));
    }

    private static Invite Convite(string codigo, ServerRole role, DateTimeOffset expira) => new()
    {
        CodeHash = SecureToken.Hash(SecureToken.NormalizeCode(codigo)),
        GameServerId = ServidorId,
        Role = role,
        CreatedByUserId = Guid.CreateVersion7(),
        ExpiresAt = expira
    };
}
