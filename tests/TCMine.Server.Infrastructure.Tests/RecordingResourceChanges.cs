using TCMine.Contracts.Hubs;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>Guarda os avisos que os repositórios mandariam aos launchers.</summary>
internal sealed class RecordingResourceChanges : IResourceChanges
{
    public List<(ResourceKind Kind, Guid? Id)> Touched { get; } = [];

    public void Touch(ResourceKind kind, Guid? id = null) => Touched.Add((kind, id));
}
