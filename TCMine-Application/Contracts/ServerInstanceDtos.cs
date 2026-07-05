using TCMine_Domain.Entities;

namespace TCMine_Application.Contracts;

/// <summary>Linha da tabela de instâncias no painel (projeção leve para escanear a lista).</summary>
public record ServerInstanceRowDto(
    Guid Id,
    string Name,
    Guid ModpackId,
    string ModpackName,
    ServerInstanceStatus Status,
    int Port,
    int RamMb,
    bool Provisioned,
    bool AutoRestart,
    bool IsStale,
    string PublicAddress);

/// <summary>Opção de modpack para o seletor ao criar/editar uma instância.</summary>
public record ModpackOptionDto(Guid Id, string Name, string Loader, string Minecraft);

/// <summary>
///     Campos editáveis de uma instância (criação e edição). O <see cref="Id" /> vazio (default) indica
///     criação. Loader/versões não entram aqui: derivam do modpack na provisão.
/// </summary>
public record ServerInstanceEditDto(
    Guid Id,
    string Name,
    Guid ModpackId,
    int Port,
    int RamMb,
    int XmsMb,
    int MaxPlayers,
    string Motd,
    string ExtraJvmArgs,
    bool AutoRestart,
    string PublicAddress,
    bool Advertise);

/// <summary>
///     Estado completo de uma instância para a página de detalhe: além dos campos editáveis, o status de
///     runtime e o <see cref="ContainerId" /> (para o console/logs consumir sem novo acesso à BD).
/// </summary>
public record ServerInstanceDetailDto(
    ServerInstanceEditDto Edit,
    string ModpackName,
    ServerInstanceStatus Status,
    string? ContainerId,
    bool Provisioned,
    bool IsStale);