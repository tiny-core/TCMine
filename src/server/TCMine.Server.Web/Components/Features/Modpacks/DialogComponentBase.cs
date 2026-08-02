using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Web.Components.Features.Modpacks;

/// <summary>
///     Base para os diálogos MudBlazor da aplicação. Centraliza o que todos
///     repetiam: a instância do diálogo, o Snackbar, o flag de "operação em
///     curso" (para desabilitar o botão e exibir progresso) e o fluxo padrão de
///     submit — executar o caso de uso, fechar no sucesso ou mostrar o erro,
///     sempre com try/finally para o botão nunca ficar travado se algo lançar.
/// </summary>
public abstract class DialogComponentBase : ComponentBase
{
    [CascadingParameter] protected IMudDialogInstance Dialog { get; set; } = default!;
    [Inject] protected ISnackbar Snackbar { get; set; } = default!;

    // Uma operação assíncrona em curso. Ligue no markup: Disabled="IsBusy" e/ou
    // um MudProgressCircular condicionado a ela.
    protected bool IsBusy { get; private set; }

    protected void Cancel()
    {
        Dialog.Cancel();
    }

    /// <summary>
    ///     Executa uma ação marcando IsBusy, com try/finally garantido e guarda
    ///     contra duplo-clique. Use quando o fluxo fugir do submit padrão (ex.:
    ///     enfileirar ingestão, upload que mantém o diálogo aberto).
    /// </summary>
    protected async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    ///     Submit padrão para casos de uso que devolvem <see cref="Result"/>: no
    ///     sucesso mostra a mensagem (se houver) e fecha o diálogo com
    ///     <paramref name="closeResult"/> (por omissão true = "algo mudou"); no
    ///     erro mostra o erro e mantém o diálogo aberto.
    /// </summary>
    protected Task SubmitAsync(
        Func<Task<Result>> action,
        string? successMessage = null,
        object? closeResult = null)
    {
        return RunAsync(async () =>
        {
            var result = await action();
            if (result.Succeeded)
            {
                if (successMessage is not null)
                    Snackbar.Add(successMessage, Severity.Success);
                Dialog.Close(DialogResult.Ok(closeResult ?? true));
            }
            else
            {
                Snackbar.Add(result.Error!, Severity.Error);
            }
        });
    }

    /// <summary>
    ///     Submit padrão para casos de uso que devolvem <see cref="Result{T}"/>:
    ///     no sucesso fecha devolvendo o valor produzido (ex.: o Id do novo
    ///     rascunho).
    /// </summary>
    protected Task SubmitAsync<T>(
        Func<Task<Result<T>>> action,
        string? successMessage = null)
    {
        return RunAsync(async () =>
        {
            var result = await action();
            if (result.Succeeded)
            {
                if (successMessage is not null)
                    Snackbar.Add(successMessage, Severity.Success);
                Dialog.Close(DialogResult.Ok(result.Value));
            }
            else
            {
                Snackbar.Add(result.Error!, Severity.Error);
            }
        });
    }
}
