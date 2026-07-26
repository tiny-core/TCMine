using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class NewsDialog : ComponentBase
{
    private string _body = "";

    private News? _editing;

    private bool _isLoading = true;
    private bool _isNew;
    private bool _isPublished;
    private bool _isSaving;
    private List<News> _posts = [];
    private string _title = "";
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter] public Guid ModpackId { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _posts = [.. await NewsRepository.ListByModpackAsync(ModpackId, CancellationToken.None)];
        _isLoading = false;
    }

    private void NewPost()
    {
        _editing = new News { ModpackId = ModpackId, Title = "", Body = "" };
        _isNew = true;
        _title = "";
        _body = "";
        _isPublished = false;
    }

    private void Edit(News post)
    {
        _editing = post;
        _isNew = false;
        _title = post.Title;
        _body = post.Body;
        _isPublished = post.IsPublished;
    }

    private async Task Save()
    {
        _isSaving = true;
        try
        {
            if (_isNew)
            {
                var result = await CreateUseCase.HandleAsync(
                    ModpackId, _title, _body, _isPublished, CancellationToken.None);
                if (!result.Succeeded)
                {
                    Snackbar.Add(result.Error!, Severity.Error);
                    return;
                }
            }
            else
            {
                var result = await UpdateUseCase.HandleAsync(
                    _editing!.Id, _title, _body, _isPublished, CancellationToken.None);
                if (!result.Succeeded)
                {
                    Snackbar.Add(result.Error!, Severity.Error);
                    return;
                }
            }

            Snackbar.Add("Novidade salva.", Severity.Success);
            _editing = null;
            await LoadAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task Delete()
    {
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar novidade", $"Apagar \"{_title}\"?", "Apagar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await DeleteUseCase.HandleAsync(_editing!.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Novidade apagada.", Severity.Success);
            _editing = null;
            await LoadAsync();
        }
        else
        {
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }
}