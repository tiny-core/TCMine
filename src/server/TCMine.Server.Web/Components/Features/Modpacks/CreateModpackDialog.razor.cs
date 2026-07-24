using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class CreateModpackDialog : ComponentBase
{
    private MudForm _form = null!;
    private bool _isSaving;
    private string _name = "";
    private string _slug = "";

    // Enquanto false, o slug acompanha o nome automaticamente. A primeira
    // edição manual do slug desliga isso — respeitar a escolha explícita do
    // admin importa mais que a conveniência.
    private bool _slugEditedManually;
    private string _summary = "";

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = null!;

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

    private void Cancel()
    {
        Dialog.Cancel();
    }

    private async Task SubmitAsync()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;

        _isSaving = true;

        var command = new CreateModpackCommand(
            _slug,
            _name,
            string.IsNullOrWhiteSpace(_summary) ? null : _summary);

        var result = await CreateModpackUseCase.HandleAsync(command, CancellationToken.None);

        _isSaving = false;

        if (result.Succeeded)
        {
            Snackbar.Add("Modpack criado.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result.Value));
        }
        else
        {
            // A validação de regra de negócio (slug duplicado, formato) volta
            // como mensagem, não exceção — e aqui vira feedback direto.
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }

    // Deriva um slug a partir do nome: minúsculas, e tudo que não for letra
    // ou dígito vira hífen. O caso de uso valida de novo do lado do servidor.
    private static string Slugify(string text)
    {
        return new string(text.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray())
            .Trim('-');
    }
}