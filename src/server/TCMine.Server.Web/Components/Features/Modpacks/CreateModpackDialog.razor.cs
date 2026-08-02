using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class CreateModpackDialog
{
    private MudForm _form = null!;
    private ModLoader _loader = ModLoader.NeoForge;
    private bool _mcReleasesOnly = true;
    private IReadOnlyList<string> _mcVersions = [];

    private string _minecraftVersion = "";
    private string _name = "";
    private string _slug = "";

    // Enquanto false, o slug acompanha o nome automaticamente. A primeira
    // edição manual do slug desliga isso — respeitar a escolha explícita do
    // admin importa mais que a conveniência.
    private bool _slugEditedManually;
    private string _summary = "";

    [Inject] private IVersionCatalog Catalog { get; set; } = default!;
    [Inject] private CreateModpack CreateModpackUseCase { get; set; } = default!;

    protected override async Task OnInitializedAsync() => await ReloadMc();

    private async Task ReloadMc() =>
        _mcVersions = await Catalog.GetMinecraftVersionsAsync(_mcReleasesOnly, CancellationToken.None);

    private Task<IEnumerable<string>> SearchMc(string value, CancellationToken ct)
    {
        return Task.FromResult(string.IsNullOrWhiteSpace(value)
            ? _mcVersions.AsEnumerable()
            : _mcVersions.Where(v => v.Contains(value, StringComparison.OrdinalIgnoreCase)));
    }

    private void OnNameChanged(string value)
    {
        _name = value;

        if (!_slugEditedManually)
            _slug = Slugify(value);
    }

    private void OnSlugChanged(string value)
    {
        _slug = value;
        _slugEditedManually = true;
    }

    private async Task Submit()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;

        var command = new CreateModpackCommand(
            _slug, _name,
            string.IsNullOrWhiteSpace(_summary) ? null : _summary,
            _minecraftVersion, _loader);

        // A validação de regra de negócio (slug duplicado, formato) volta como
        // mensagem via Result, não exceção — a base transforma em snackbar.
        await SubmitAsync(
            () => CreateModpackUseCase.HandleAsync(command, CancellationToken.None),
            "Modpack criado.");
    }

    // Deriva um slug a partir do nome: minúsculas, e tudo que não for letra ou
    // dígito vira hífen. O caso de uso valida de novo do lado do servidor.
    private static string Slugify(string text)
    {
        return new string(text.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray())
            .Trim('-');
    }
}
