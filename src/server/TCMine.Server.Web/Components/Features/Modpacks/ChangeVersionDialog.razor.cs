using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ChangeVersionDialog
{
    private bool _isLoading = true;
    private Guid _selectedVersionId;
    private List<ModpackVersion> _versions = [];

    [Parameter] public GameServer Server { get; set; } = default!;

    [Inject] private IModpackRepository ModpackRepository { get; set; } = default!;
    [Inject] private ChangeServerVersion ChangeUseCase { get; set; } = default!;

    /// <summary>
    ///     A mesma condição do <c>ChangeServerVersion</c>, só para avisar antes do
    ///     clique; quem decide continua sendo o caso de uso, que consulta o Docker.
    /// </summary>
    private bool IsStopped => Server.Status is GameServerStatus.Stopped or GameServerStatus.Crashed;

    // Com mundo, só parado: o caso de uso recusaria de qualquer forma, e o botão
    // desligado com o aviso ao lado explica antes de o admin esperar a resposta.
    private bool CanApply =>
        _selectedVersionId != Guid.Empty
        && _selectedVersionId != Server.ModpackVersionId
        && (!Server.HasWorld || IsStopped);

    protected override async Task OnInitializedAsync()
    {
        // Antes este diálogo recusava todo servidor com mundo ("a troca segura
        // fica disponível quando a orquestração estiver ativa"). A troca segura
        // existe há tempo — o ChangeServerVersion faz o backup antes —, e o
        // recuso só não aparecia porque a marca de mundo nunca era gravada.
        var all = await ModpackRepository.ListVersionsAsync(Server.ModpackId, CancellationToken.None);
        // Só instaláveis: publicadas ou arquivadas (arquivada continua rodável
        // por quem já a fixou — é o alvo natural de um rollback).
        _versions =
        [
            .. all
                .Where(v => v.State is ModpackVersionState.Ready or ModpackVersionState.Archived)
        ];
        _selectedVersionId = Server.ModpackVersionId;
        _isLoading = false;
    }

    private Task Apply()
    {
        return SubmitAsync(
            () => ChangeUseCase.HandleAsync(Server.Id, _selectedVersionId, CancellationToken.None),
            "Versão do servidor alterada.");
    }
}
