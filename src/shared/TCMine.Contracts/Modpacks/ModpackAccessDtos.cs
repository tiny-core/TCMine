namespace TCMine.Contracts.Modpacks;

/// <summary>
///     O papel de alguém sobre um modpack específico.
///     Espaçado a cada 10, como o <c>ServerRoleDto</c> — mesma razão: cabe um
///     papel intermediário no futuro sem renumerar os existentes.
/// </summary>
public enum ModpackRoleDto
{
    /// <summary>Edita mods, overrides, cria e publica versões.</summary>
    Editor = 0,

    /// <summary>Editor + gerencia quem mais edita, e pode apagar o modpack.</summary>
    Owner = 10
}
