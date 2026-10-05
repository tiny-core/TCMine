using Microsoft.Extensions.Logging.Abstractions;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Application.Tests.Fakes;
using TCMine.Server.Domain.Cloud;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Tests.Cloud;

/// <summary>
///     Permissões do painel da nuvem: um dono nunca enxerga nem mexe na nuvem de
///     outro. "Não encontrada" e "sem acesso" respondem igual, para não confirmar
///     que a nuvem alheia existe.
/// </summary>
public sealed class CloudPanelUseCasesTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Dono_ve_so_as_proprias_nuvens_e_o_admin_ve_todas()
    {
        var repo = new FakeCloudAdminRepository();
        var eu = new FakeUserScope();
        repo.Vaults.Add(new CloudVault { Name = "Minha", OwnerId = eu.OwnerId });
        repo.Vaults.Add(new CloudVault { Name = "Alheia", OwnerId = Guid.CreateVersion7() });

        (await new ListCloudVaults(repo, eu).HandleAsync(Ct)).Select(v => v.Name).ShouldBe(["Minha"]);
        (await new ListCloudVaults(repo, new FakeUserScope { IsInstanceAdmin = true }).HandleAsync(Ct)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Nuvem_de_outro_dono_responde_como_inexistente()
    {
        var repo = new FakeCloudAdminRepository();
        var alheia = new CloudVault { Name = "Alheia", OwnerId = Guid.CreateVersion7() };
        repo.Vaults.Add(alheia);

        var result = await new GetCloudVault(repo, new FakeUserScope()).HandleAsync(alheia.Id, Ct);

        result.Succeeded.ShouldBeFalse();
        result.Error.ShouldBe(CloudVaultAccess.NotFound);
    }

    [Fact]
    public async Task Criar_nuvem_faz_do_usuario_o_dono_e_valida_o_nome()
    {
        var repo = new FakeCloudAdminRepository();
        var eu = new FakeUserScope();
        var useCase = new CreateCloudVault(repo, eu);

        (await useCase.HandleAsync("   ", Ct)).Succeeded.ShouldBeFalse();
        (await useCase.HandleAsync("  Survival  ", Ct)).Succeeded.ShouldBeTrue();

        repo.Vaults.Single().Name.ShouldBe("Survival");
        repo.Vaults.Single().OwnerId.ShouldBe(eu.OwnerId);
    }

    [Fact]
    public async Task Limite_fora_de_faixa_recusa_o_formulario_inteiro()
    {
        var repo = new FakeCloudAdminRepository();
        var eu = new FakeUserScope();
        var nuvem = new CloudVault { Name = "N", OwnerId = eu.OwnerId };
        repo.Vaults.Add(nuvem);

        var result = await new UpdateCloudVault(repo, eu).HandleAsync(nuvem.Id,
            new CloudVaultSettings("Outro nome", true, CloudPolicyMode.Blocklist, 0, 8192, 5, 100, 1000), Ct);

        result.Succeeded.ShouldBeFalse();
        nuvem.Name.ShouldBe("N", "nada pode mudar quando o formulário é recusado");
    }

    [Fact]
    public async Task Ligar_servidor_a_nuvem_de_outro_dono_e_recusado()
    {
        var repo = new FakeCloudAdminRepository();
        var eu = new FakeUserScope { IsInstanceAdmin = true }; // nem o admin fura a regra do dono
        var servidor = Servidor(Guid.CreateVersion7());
        var nuvem = new CloudVault { Name = "N", OwnerId = Guid.CreateVersion7() };
        repo.Vaults.Add(nuvem);

        var result = await SetVault(repo, new FakeCloudCredentialRepository(), servidor, eu).HandleAsync(servidor.Id, nuvem.Id, Ct);

        result.Succeeded.ShouldBeFalse();
        servidor.CloudVaultId.ShouldBeNull();
    }

    [Fact]
    public async Task Trocar_ou_desligar_a_nuvem_revoga_a_chave()
    {
        var repo = new FakeCloudAdminRepository();
        var eu = new FakeUserScope();
        var servidor = Servidor(eu.OwnerId);
        var nuvem = new CloudVault { Name = "N", OwnerId = eu.OwnerId };
        repo.Vaults.Add(nuvem);
        servidor.AttachToCloudVault(nuvem);
        var chaves = new FakeCloudCredentialRepository();
        chaves.Credentials.Add(new CloudServerCredential
            { GameServerId = servidor.Id, VaultId = nuvem.Id, KeyPrefix = "abcdefghijkm", KeyHash = "h" });

        (await SetVault(repo, chaves, servidor, eu).HandleAsync(servidor.Id, null, Ct)).Succeeded.ShouldBeTrue();

        servidor.CloudVaultId.ShouldBeNull();
        chaves.Credentials.Single().IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task Sem_ser_dono_do_servidor_nao_mexe_na_nuvem_dele()
    {
        var repo = new FakeCloudAdminRepository();
        var admin = new FakeUserScope(ServerRoleDto.Admin);
        var servidor = Servidor(admin.OwnerId);
        var nuvem = new CloudVault { Name = "N", OwnerId = admin.OwnerId };
        repo.Vaults.Add(nuvem);

        var result = await SetVault(repo, new FakeCloudCredentialRepository(), servidor, admin)
            .HandleAsync(servidor.Id, nuvem.Id, Ct);

        result.Succeeded.ShouldBeFalse("ligar à nuvem é decisão do dono do servidor");
    }

    [Fact]
    public async Task Canal_de_outra_nuvem_nao_aparece_nem_descongela()
    {
        var repo = new FakeCloudAdminRepository();
        var eu = new FakeUserScope();
        var minha = new CloudVault { Name = "Minha", OwnerId = eu.OwnerId };
        repo.Vaults.Add(minha);
        var canalAlheio = new CloudChannel { VaultId = Guid.CreateVersion7(), PlayerUuid = new string('a', 32), Name = "X" };
        canalAlheio.Freeze("teste");
        repo.Channels.Add(canalAlheio);

        (await new GetCloudChannelBalances(repo, eu).HandleAsync(minha.Id, canalAlheio.Id, Ct)).Succeeded.ShouldBeFalse();
        (await new UnfreezeCloudChannel(repo, eu, NullLogger<UnfreezeCloudChannel>.Instance)
            .HandleAsync(minha.Id, canalAlheio.Id, Ct)).Succeeded.ShouldBeFalse();
        canalAlheio.IsFrozen.ShouldBeTrue();
    }

    private static SetServerCloudVault SetVault(FakeCloudAdminRepository repo, FakeCloudCredentialRepository chaves,
        GameServer servidor, FakeUserScope scope) =>
        new(repo, new UmServidor(servidor), chaves, scope, TimeProvider.System, NullLogger<SetServerCloudVault>.Instance);

    private static GameServer Servidor(Guid dono) => new()
    {
        Name = "S", ModpackId = Guid.CreateVersion7(), ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "localhost", RconSecret = "segredo", OwnerId = dono
    };

    private sealed class UmServidor(GameServer servidor) : FakeServerRepositoryBase
    {
        public override Task<GameServer?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<GameServer?>(id == servidor.Id ? servidor : null);

        public override Task UpdateAsync(GameServer server, CancellationToken ct) => Task.CompletedTask;
    }
}
