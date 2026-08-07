using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackNewsPage : ComponentBase
{
    private string _body = "";
    private News? _editing;
    private bool _isLoading = true;
    private bool _isNew;
    private bool _isPublished;
    private bool _isSaving;
    private Modpack? _modpack;
    private List<News> _posts = [];
    private string _title = "";

    [Parameter] public Guid ModpackId { get; set; }

    [Inject] private IModpackRepository ModpackRepository { get; set; } = default!;
    [Inject] private INewsRepository NewsRepository { get; set; } = default!;
    [Inject] private CreateNews CreateUseCase { get; set; } = default!;
    [Inject] private UpdateNews UpdateUseCase { get; set; } = default!;
    [Inject] private DeleteNews DeleteUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _modpack = await ModpackRepository.GetByIdAsync(ModpackId, CancellationToken.None);
        await LoadPostsAsync();
        _isLoading = false;
    }

    private async Task LoadPostsAsync() => _posts =
        [.. await NewsRepository.ListByModpackAsync(ModpackId, CancellationToken.None)];

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
            // Create devolve Result<Guid> e Update devolve Result; normalizamos.
            Result result;
            if (_isNew)
            {
                var created = await CreateUseCase.HandleAsync(
                    ModpackId, _title, _body, _isPublished, CancellationToken.None);
                result = created.Succeeded ? Result.Success() : Result.Fail(created.Error!);
            }
            else
            {
                result = await UpdateUseCase.HandleAsync(
                    _editing!.Id, _title, _body, _isPublished, CancellationToken.None);
            }

            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Error);
                return;
            }

            Snackbar.Add("Novidade salva.", Severity.Success);
            _editing = null;
            await LoadPostsAsync();
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
            await LoadPostsAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }
}
