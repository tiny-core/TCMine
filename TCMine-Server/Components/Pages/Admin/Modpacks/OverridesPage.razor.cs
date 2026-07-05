using Microsoft.AspNetCore.Components;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
///     Página dedicada de overrides de um modpack (Fase 3): apenas hospeda o <c>OverridesPanel</c>, que é
///     self-contained (carrega e grava direto no disco por <see cref="Id" />). Substitui a antiga aba.
/// </summary>
public partial class OverridesPage : ComponentBase
{
    [Parameter] public Guid Id { get; set; }
}