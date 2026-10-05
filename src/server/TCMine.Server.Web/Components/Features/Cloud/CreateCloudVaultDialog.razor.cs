using Microsoft.AspNetCore.Components;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Features.Cloud;

public partial class CreateCloudVaultDialog : DialogComponentBase
{
    private string _name = "";

    [Inject] private CreateCloudVault UseCase { get; set; } = default!;

    private Task CreateAsync() =>
        SubmitAsync(() => UseCase.HandleAsync(_name, CancellationToken.None), "Nuvem criada.");
}
