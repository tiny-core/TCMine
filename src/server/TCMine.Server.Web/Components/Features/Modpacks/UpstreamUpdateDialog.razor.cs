using System.Text;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class UpstreamUpdateDialog : DialogComponentBase, IDisposable
{
    private const string PreStyle =
        "max-height:260px; overflow:auto; font-size:12px; white-space:pre; margin:0; padding:8px; "
        + "background:var(--mud-palette-background-gray); border-radius:4px;";

    /// <summary>Chaves marcadas. Começa na escolha padrão; cada caixa liga e desliga uma.</summary>
    private readonly HashSet<string> _accepted = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Caminho do config com os dois lados abertos (um por vez: são textos longos).</summary>
    private string? _comparing;

    private string? _error;

    /// <summary>Acompanhamento do trabalho desta janela.</summary>
    private Guid _jobId;

    private string _latestLabel = "";
    private bool _loading = true;
    private string _newVersion = "";
    private string? _ourText;
    private UpstreamMergePlan? _plan;
    private List<SectionView> _sections = [];
    private string? _theirText;
    private IReadOnlyDictionary<string, byte[]> _theirContents = new Dictionary<string, byte[]>();

    [Parameter] public Guid VersionId { get; set; }

    /// <summary>Rótulo do autor na versão atual, só para o texto "4.2 → 4.3".</summary>
    [Parameter]
    public string CurrentLabel { get; set; } = "";

    /// <summary>Nossa numeração atual, para sugerir a próxima.</summary>
    [Parameter]
    public string CurrentVersion { get; set; } = "";

    [Inject] private UpdateFromUpstream UpdateUseCase { get; set; } = default!;
    [Inject] private ReadOverride ReadOverrideUseCase { get; set; } = default!;
    [Inject] private JobProgressRegistry Jobs { get; set; } = default!;

    private JobProgress? Progress => _jobId == Guid.Empty ? null : Jobs.Get(_jobId);

    private int TotalItems => _sections.Sum(s => s.Items.Count);

    public void Dispose()
    {
        Jobs.Changed -= OnJobChanged;
        GC.SuppressFinalize(this);
    }

    private void OnJobChanged() => _ = InvokeAsync(StateHasChanged);

    protected override async Task OnInitializedAsync()
    {
        _newVersion = NextVersion(CurrentVersion);
        Jobs.Changed += OnJobChanged;
        _jobId = Guid.CreateVersion7();

        // dryRun: calcula e mostra o plano sem gravar nada. O admin precisa ver
        // o que vai acontecer antes de aceitar — atualizar às cegas um pack com
        // configs customizados é como aplicar patch sem ler o diff.
        var result = await UpdateUseCase.HandleAsync(VersionId, "", CancellationToken.None, true, _jobId);

        if (result.Succeeded)
        {
            _plan = result.Value!.Plan;
            _latestLabel = result.Value.LatestLabel;
            _theirContents = result.Value.TheirConflictContents;

            if (_plan.IsEmpty)
                _error = "Nada mudou em relação à origem. Você já está na versão do autor.";
            else
            {
                _sections = BuildSections(_plan);
                _accepted.UnionWith(UpstreamSelection.Default(_plan).Accepted);
            }
        }
        else
            _error = result.Error;

        _loading = false;
        _jobId = Guid.Empty;
    }

    private void Toggle(string key, bool on)
    {
        if (on)
            _accepted.Add(key);
        else
            _accepted.Remove(key);
    }

    private void SetAll(SectionView section, bool on)
    {
        foreach (var item in section.Items)
            Toggle(item.Key, on);
    }

    private int Marked(SectionView section) => section.Items.Count(i => _accepted.Contains(i.Key));

    private async Task ToggleCompareAsync(string path)
    {
        if (_comparing == path)
        {
            _comparing = null;
            return;
        }

        _comparing = path;
        _ourText = null;
        _theirText = _theirContents.TryGetValue(path, out var bytes)
            ? AsText(bytes)
            : "(grande demais para mostrar aqui)";

        // O lado do admin vem da versão atual, pelo mesmo leitor do editor de
        // overrides — que já sabe recusar binário e arquivo grande.
        var ours = await ReadOverrideUseCase.HandleAsync(VersionId, path, CancellationToken.None);

        if (_comparing != path)
            return; // fechou ou abriu outro enquanto lia

        _ourText = ours switch
        {
            { Succeeded: false } => "(você apagou este arquivo)",
            { Value.Text: { } text } => text,
            { Value.Reason: { } reason } => $"({reason})",
            _ => "(não dá para mostrar)"
        };
    }

    private Task Apply()
    {
        // Baixar o pack e gravar milhares de configs leva minutos: a janela fica
        // aberta mostrando o passo, em vez de fechar e deixar o admin no escuro.
        _jobId = Guid.CreateVersion7();

        // Cópia: o conjunto da tela continua mudando se o admin clicar durante
        // o envio, e o caso de uso não pode ver a escolha mexer no meio.
        var selection = new UpstreamSelection(new HashSet<string>(_accepted, StringComparer.OrdinalIgnoreCase));

        return SubmitAsync(
            async () =>
            {
                var result = await UpdateUseCase.HandleAsync(
                    VersionId, _newVersion, CancellationToken.None, false, _jobId, selection);

                return result.Succeeded
                    ? Result.Success()
                    : Result.Fail(result.Error!);
            },
            "Rascunho criado. Os mods novos estão sendo baixados em segundo plano.");
    }

    /// <summary>
    ///     Uma seção por tipo de mudança, na ordem em que o admin precisa olhar:
    ///     primeiro o que exige decisão, depois o resto. Seção vazia não aparece.
    /// </summary>
    private static List<SectionView> BuildSections(UpstreamMergePlan plan)
    {
        var conflicts = plan.Conflicts
            .Select(c => new ItemView(
                UpstreamItemKey.Mod(c.ProjectId), c.Name, Explain(c), Icons.Material.Filled.CallSplit))
            .Concat(plan.Overrides.Conflicts.Select(c => new ItemView(
                UpstreamItemKey.Override(c.Path), c.Path, Explain(c), Icons.Material.Filled.CallSplit)
            {
                Path = c.Path, CanCompare = c.Kind is UpstreamConflictKind.BothChanged
            }))
            .ToList();

        var overrides = plan.Overrides;

        List<SectionView> sections =
        [
            new("Conflitos", conflicts, true),
            new("Mods atualizados", [
                .. plan.Update.Select(c => new ItemView(
                    UpstreamItemKey.Mod(c.ProjectId), c.Name,
                    c.OurFileName is { } atual ? $"{atual} → versão nova do autor" : null,
                    Icons.Material.Filled.Upgrade))
            ]),
            new("Mods novos", [
                .. plan.Add.Select(c => new ItemView(
                    UpstreamItemKey.Mod(c.ProjectId), c.Name, null, Icons.Material.Filled.Add))
            ]),
            new("Mods removidos pelo autor", [
                .. plan.Remove.Select(c => new ItemView(
                    UpstreamItemKey.Mod(c.ProjectId), c.Name, c.OurFileName, Icons.Material.Filled.Remove))
            ]),
            new("Configs atualizados", [
                .. overrides.Updated.Select(p => new ItemView(
                    UpstreamItemKey.Override(p), p, null, Icons.Material.Filled.EditNote))
            ]),
            new("Configs novos", [
                .. overrides.Added.Select(p => new ItemView(
                    UpstreamItemKey.Override(p), p, null, Icons.Material.Filled.NoteAdd))
            ]),
            new("Configs removidos pelo autor", [
                .. overrides.Removed.Select(p => new ItemView(
                    UpstreamItemKey.Override(p), p, null, Icons.Material.Filled.DeleteOutline))
            ])
        ];

        return [.. sections.Where(s => s.Items.Count > 0)];
    }

    /// <summary>
    ///     Texto para o painel lado a lado. Byte zero = binário (PNG, .jar), que
    ///     como texto vira lixo — a mesma regra do editor de overrides.
    /// </summary>
    private static string AsText(byte[] bytes) =>
        bytes.AsSpan(0, Math.Min(bytes.Length, 8 * 1024)).IndexOf((byte)0) >= 0
            ? "(arquivo binário)"
            : Encoding.UTF8.GetString(bytes);

    /// <summary>
    ///     Sugere o próximo minor ("1.0.0" → "1.1.0"). É palpite: o admin edita.
    ///     Atualização de pack costuma trazer mods novos, o que é mudança de
    ///     comportamento — patch subestimaria.
    /// </summary>
    private static string NextVersion(string current)
    {
        var parts = current.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var minor))
            return "";

        return $"{parts[0]}.{minor + 1}.0";
    }

    // Os dois lados em palavras: o id de arquivo da origem não diz nada ao
    // admin, o nome do jar que ele tem aqui diz.
    private static string Explain(UpstreamModConflict conflict) => conflict.Kind switch
    {
        UpstreamConflictKind.BothChanged =>
            $"Você trocou a versão ({conflict.OurFileName ?? "sua"}) e o autor também. "
            + "Marcado: entra a do autor. Desmarcado: fica a sua.",
        UpstreamConflictKind.RemovedHereKeptThere =>
            "Você removeu este mod; o autor continua incluindo e o atualizou. "
            + "Marcado: volta, na versão do autor. Desmarcado: segue removido.",
        UpstreamConflictKind.ChangedHereRemovedThere =>
            $"Você trocou a versão ({conflict.OurFileName ?? "sua"}) e o autor removeu do pack. "
            + "Marcado: sai. Desmarcado: o seu fica.",
        _ => ""
    };

    private static string Explain(UpstreamOverrideConflict conflict) => conflict.Kind switch
    {
        UpstreamConflictKind.BothChanged =>
            "Você editou este arquivo e o autor também. Marcado: entra o do autor (o seu é substituído).",
        UpstreamConflictKind.RemovedHereKeptThere =>
            "Você apagou este arquivo e o autor o mudou. Marcado: volta, na versão do autor.",
        UpstreamConflictKind.ChangedHereRemovedThere =>
            "Você editou este arquivo e o autor o tirou do pack. Marcado: sai. Desmarcado: o seu fica.",
        _ => ""
    };

    // List, e não IReadOnlyList: o Virtualize pede ICollection.
    private sealed record SectionView(string Title, List<ItemView> Items, bool IsConflicts = false);

    private sealed record ItemView(string Key, string Title, string? Detail, string Icon)
    {
        /// <summary>Caminho do config, quando o item é um config.</summary>
        public string? Path { get; init; }

        /// <summary>Dá para abrir os dois lados (os dois existem).</summary>
        public bool CanCompare { get; init; }
    }
}
