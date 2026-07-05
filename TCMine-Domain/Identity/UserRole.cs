namespace TCMine_Domain.Identity;

/// <summary>
///     Níveis de acesso do painel admin, do mais para o menos privilegiado.
///     Guardado como string no banco (ver mapeamento no AppDbContext) — legível e estável
///     as reordenações do enum.
/// </summary>
public enum UserRole
{
    /// Dono do sistema: gerencia usuários e os secrets (CF/Azure). Criado no setup inicial.
    Owner,

    /// Gerencia conteúdo (modpacks, novidades, releases, servidores)
    Admin,

    /// Opera servidores Minecraft (start/stop), sem editar conteúdo
    Operator,

    /// Somente leitura
    Viewer
}