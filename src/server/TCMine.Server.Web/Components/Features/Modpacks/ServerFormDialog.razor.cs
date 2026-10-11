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

    /// <summary>Host público e domínio da instalação, para a prévia do endereço.</summary>
    private AddressSettings _address = new(null, null, null);

    private string _subdomain = "";

    /// <summary>Teto de opções no campo Porta: uma faixa enorme não vira uma lista interminável.</summary>
    private const int MaxPortChoices = 100;

    private GamePortChoices _portChoices = new(GamePortDefaults.First, GamePortDefaults.Last, [], false);

    /// <summary>
    ///     O que o campo Porta oferece: as livres da faixa. Na edição, a porta
    ///     que o servidor já tem entra sempre — mesmo fora da faixa (um servidor
    ///     de antes de a faixa mudar), porque mantê-la é sempre permitido.
    /// </summary>
    private IEnumerable<int> PortOptions =>
        Existing is null || _portChoices.Free.Contains(Existing.GamePort)
            ? _portChoices.Free
            : _portChoices.Free.Prepend(Existing.GamePort);

    private string PortHelp
    {
        get
        {
            var range = $"Portas livres da faixa {_portChoices.RangeStart}–{_portChoices.RangeEnd}";
            var more = _portChoices.Truncated ? $" (as {MaxPortChoices} primeiras)" : "";

            return _isNew
                ? $"{range}{more}. A faixa muda em Configurações → Rede."
                : $"{range}{more}. Uma troca vale a partir do próximo início.";
        }
    }

    /// <summary>O rótulo de uma opção: só a porta atual de um servidor fora da faixa precisa de explicação.</summary>
    private string PortLabel(int port) =>
        Existing is not null
        && port == Existing.GamePort
        && !GamePortRange.Contains(_portChoices.RangeStart, _portChoices.RangeEnd, port)
            ? $"{port} (atual, fora da faixa)"
            : port.ToString(System.Globalization.CultureInfo.InvariantCulture);

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
    [Inject] private GetAddressSettings AddressLookup { get; set; } = default!;

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
        _address.For(
            _connectAddress,
            GamePortRange.IsValid(_gamePort) ? _gamePort : GamePortDefaults.First,
            _subdomain);

    /// <summary>
    ///     O que o campo Subdomínio vai fazer, dito no próprio campo: com o DNS
    ///     configurado mostra o nome completo; sem ele, diz o que falta — o
    ///     subdomínio fica guardado e passa a valer quando a Cloudflare for
    ///     configurada.
    /// </summary>
    private string SubdomainHelp =>
        _address.DnsBaseDomain is { } domain
            ? $"Opcional. Publica este servidor como nome.{domain}, sem porta (só Minecraft Java)."
            : "Opcional. Só é publicado com a Cloudflare configurada em Configurações → Rede.";

    /// <summary>
    ///     Os dois preenchidos: o endereço escrito vence, e o subdomínio é criado
    ///     na Cloudflare mas não é o que os jogadores recebem. Vale avisar, porque
    ///     quem preenche os dois quase sempre queria o subdomínio.
    /// </summary>
    private bool AddressHidesSubdomain =>
        !string.IsNullOrWhiteSpace(_connectAddress)
        && GameDns.ServerHost(_subdomain, _address.DnsBaseDomain) is not null;

    private ModpackVersion? _selected => _versions.FirstOrDefault(v => v.Id == _selectedVersionId);
    private int SelectedModCount => _selected?.Files.Count(f => f.Origin != ModFileOrigin.Override) ?? 0;

    protected override async Task OnInitializedAsync()
    {
        _isNew = Existing is null;

        _address = await AddressLookup.HandleAsync(false, CancellationToken.None);

        // Antes de preencher os campos: a lista é o que o campo Porta oferece,
        // nos dois modos. Na edição o servidor em causa fica de fora da conta,
        // para a porta que ele já tem aparecer como escolha.
        _portChoices = await Ports.ListChoicesAsync(Existing?.Id, MaxPortChoices, CancellationToken.None);

        if (Existing is not null)
        {
            _name = Existing.Name;
            _connectAddress = Existing.ConnectAddress;
            _gamePort = Existing.GamePort;
            _subdomain = Existing.Subdomain ?? "";
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
        _gamePort = _portChoices.Free.Count > 0 ? _portChoices.Free[0] : 0;
        _portSuggestionError = _portChoices.Free.Count > 0
            ? null
            : $"Não há porta livre na faixa {_portChoices.RangeStart}–{_portChoices.RangeEnd}. "
              + "Aumente a faixa em Configurações → Rede ou apague um servidor.";

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
                CancellationToken.None, _gamePort, _subdomain);
        }

        var created = await CreateUseCase.HandleAsync(
            ModpackId, _name, _connectAddress, _memoryMb, _maxPlayers, _selectedVersionId,
            CancellationToken.None, _gamePort, _subdomain);
        return created.Succeeded ? Result.Success() : Result.Fail(created.Error!);
    }
}
