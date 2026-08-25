using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ServerFormDialog
{
    private string _connectAddress = "";
    private bool _isNew;
    private int _maxPlayers = 20;

    /// <summary>Ligada por padrão: um servidor novo nasce fechado.</summary>
    private bool _whitelistEnabled = true;
    private int _memoryMb = 4096;
    private string _name = "";
    private Guid _selectedVersionId;
    private List<ModpackVersion> _versions = [];

    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public GameServer? Existing { get; set; }

    [Inject] private CreateGameServer CreateUseCase { get; set; } = default!;
    [Inject] private UpdateGameServer UpdateUseCase { get; set; } = default!;
    [Inject] private IModpackRepository ModpackRepository { get; set; } = default!;

    private ModpackVersion? _selected => _versions.FirstOrDefault(v => v.Id == _selectedVersionId);
    private int SelectedModCount => _selected?.Files.Count(f => f.Origin != ModFileOrigin.Override) ?? 0;

    protected override async Task OnInitializedAsync()
    {
        _isNew = Existing is null;

        if (Existing is not null)
        {
            _name = Existing.Name;
            _connectAddress = Existing.ConnectAddress;
            _memoryMb = Existing.MemoryMb;
            _maxPlayers = Existing.MaxPlayers;
            _whitelistEnabled = Existing.WhitelistEnabled;
            return;
        }

        // Novo: só publicadas; a mais recente já vem selecionada.
        _versions =
        [
            .. (await ModpackRepository.ListVersionsAsync(ModpackId, CancellationToken.None))
            .Where(v => v.State is ModpackVersionState.Ready && !v.IsPreRelease)
        ];
        _selectedVersionId = _versions.FirstOrDefault()?.Id ?? Guid.Empty;
    }

    private Task Save() => SubmitAsync(SaveCoreAsync, "Servidor salvo.");

    // Create devolve Result<Guid> e Update devolve Result; aqui só interessa o
    // sucesso/erro, então normalizamos para Result (o diálogo fecha com true).
    private async Task<Result> SaveCoreAsync()
    {
        if (!_isNew)
        {
            return await UpdateUseCase.HandleAsync(
                Existing!.Id, _name, _connectAddress, _memoryMb, _maxPlayers, _whitelistEnabled,
                CancellationToken.None);
        }

        var created = await CreateUseCase.HandleAsync(
            ModpackId, _name, _connectAddress, _memoryMb, _maxPlayers, _selectedVersionId,
            CancellationToken.None);
        return created.Succeeded ? Result.Success() : Result.Fail(created.Error!);
    }
}
