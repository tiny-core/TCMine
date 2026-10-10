using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Servers;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ServerFormDialog
{
    private string _connectAddress = "";
    private int _gamePort = GamePortDefaults.First;
    private bool _isNew;
    private int _maxPlayers = 20;
    private int _memoryMb = 4096;
    private string _minecraftVersion = "";
    private string _name = "";

    /// <summary>Por que não houve porta para sugerir (faixa esgotada). Nulo = houve.</summary>
    private string? _portSuggestionError;

    /// <summary>Host público da instalação, para a prévia do endereço automático.</summary>
    private string? _publicHost;

    private Guid _selectedVersionId;
    private List<ModpackVersion> _versions = [];

    /// <summary>Ligada por padrão: um servidor novo nasce fechado.</summary>
    private bool _whitelistEnabled = true;

    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public GameServer? Existing { get; set; }

    [Inject] private CreateGameServer CreateUseCase { get; set; } = default!;
    [Inject] private UpdateGameServer UpdateUseCase { get; set; } = default!;
    [Inject] private IModpackRepository ModpackRepository { get; set; } = default!;
    [Inject] private IGamePortAllocator Ports { get; set; } = default!;
    [Inject] private GetPublicHost PublicHostLookup { get; set; } = default!;

    /// <summary>
    ///     A porta ESCRITA no endereço, quando há. Só ela pode discordar da porta
    ///     do servidor: um endereço sem porta segue a do servidor sozinho.
    ///     Discordar não é erro — um roteador pode expor a 25565 e entregar na
    ///     25570 —, mas é a causa mais comum de "não consigo entrar", então o
    ///     formulário avisa em vez de calar.
    /// </summary>
    private int? ExplicitAddressPort =>
        GamePortRange.IsValid(_gamePort) ? GameAddress.ExplicitPort(_connectAddress) : null;

    /// <summary>
    ///     O endereço como será publicado. Com a porta em zero (faixa esgotada, e
    ///     o caso de uso é quem recusa) a prévia usa a padrão só para ter o que
    ///     mostrar.
    /// </summary>
    private string PublishedAddress =>
        GameAddress.Resolve(
            _connectAddress,
            GamePortRange.IsValid(_gamePort) ? _gamePort : GamePortDefaults.First,
            _publicHost);

    private ModpackVersion? _selected => _versions.FirstOrDefault(v => v.Id == _selectedVersionId);
    private int SelectedModCount => _selected?.Files.Count(f => f.Origin != ModFileOrigin.Override) ?? 0;

    protected override async Task OnInitializedAsync()
    {
        _isNew = Existing is null;

        _publicHost = (await PublicHostLookup.HandleAsync(false, CancellationToken.None)).Effective;

        if (Existing is not null)
        {
            _name = Existing.Name;
            _connectAddress = Existing.ConnectAddress;
            _gamePort = Existing.GamePort;
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

        // A sugestão é só uma leitura: nada fica reservado. Se outro servidor
        // pegar esta porta antes do Salvar, o caso de uso confere de novo e recusa.
        // Sem porta livre o campo fica em zero, que o caso de uso lê como
        // "escolha por mim" — e devolve o mesmo erro mostrado aqui.
        var suggested = await Ports.AllocateAsync(CancellationToken.None);
        _gamePort = suggested.Succeeded ? suggested.Value : 0;
        _portSuggestionError = suggested.Succeeded ? null : suggested.Error;

        // A versão do Minecraft é do modpack (fixa, imutável), não da versão
        // publicada — a legenda mostrava LoaderVersion rotulado de "Minecraft",
        // que é outro número (ex.: a build do NeoForge).
        var modpack = await ModpackRepository.GetByIdAsync(ModpackId, CancellationToken.None);
        _minecraftVersion = modpack?.MinecraftVersion ?? "";
    }

    /// <summary>
    ///     Troca a porta e, se o endereço tinha a anterior ESCRITA, leva o
    ///     endereço junto. Sem porta escrita não há o que levar — ele já segue a
    ///     do servidor. E um endereço que o admin apontou para outra porta
    ///     (redirecionamento no roteador) é decisão dele e fica como está.
    /// </summary>
    private void OnGamePortChanged(int port)
    {
        if (GameAddress.ExplicitPort(_connectAddress) == _gamePort)
            _connectAddress = GameAddress.WithPort(_connectAddress, port);

        _gamePort = port;
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
                CancellationToken.None, _gamePort);
        }

        var created = await CreateUseCase.HandleAsync(
            ModpackId, _name, _connectAddress, _memoryMb, _maxPlayers, _selectedVersionId,
            CancellationToken.None, _gamePort);
        return created.Succeeded ? Result.Success() : Result.Fail(created.Error!);
    }
}
