using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class CreateModpackDialog
{
    private MudForm _form = null!;
    private IBrowserFile? _icon;
    private string? _iconName;
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
    [Inject] private SetModpackIcon IconUseCase { get; set; } = default!;

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

    private void OnIconPicked(IBrowserFile file)
    {
        _icon = file;
        _iconName = file.Name;
    }

    private Task Submit()
    {
        // Fluxo próprio (em vez do SubmitAsync padrão) porque a capa é um segundo
        // passo depois de criar: criamos o modpack, e só então enviamos a imagem
        // para o Id recém-criado.
        return RunAsync(async () =>
        {
            await _form.ValidateAsync();
            if (!_form.IsValid)
                return;

            var command = new CreateModpackCommand(
                _slug, _name,
                string.IsNullOrWhiteSpace(_summary) ? null : _summary,
                _minecraftVersion, _loader);

            var result = await CreateModpackUseCase.HandleAsync(command, CancellationToken.None);
            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Error);
                return;
            }

            await UploadIconIfAnyAsync(result.Value);

            Snackbar.Add("Modpack criado.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result.Value));
        });
    }

    // Envia a capa (se escolhida) para o modpack. Falhar aqui não desfaz a
    // criação — o modpack existe, só ficou sem capa; avisamos e seguimos.
    private async Task UploadIconIfAnyAsync(Guid modpackId)
    {
        if (_icon is null)
            return;

        // Limite defensivo de 5 MB para um ícone.
        await using var stream = _icon.OpenReadStream(5 * 1024 * 1024);
        var result = await IconUseCase.HandleAsync(modpackId, stream, _icon.ContentType, CancellationToken.None);
        if (!result.Succeeded)
            Snackbar.Add($"Modpack criado, mas a capa falhou: {result.Error}", Severity.Warning);
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
