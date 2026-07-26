namespace TCMine.Server.Domain.Common;

/// <summary>
///     Base de toda entidade persistida.
///     Os setters são protected: só a própria entidade muda o próprio estado.
///     Isso força as mudanças a passarem por métodos com nome ("MarkReady"),
///     que é onde as regras cabem. Propriedade pública com set livre transforma
///     a entidade num saco de dados e espalha regra pelo sistema inteiro.
/// </summary>
public abstract class Entity
{
    /// <summary>
    ///     GUID v7: os primeiros bits são timestamp, então os valores gerados em
    ///     sequência ficam ordenados. Num índice de banco isso evita a
    ///     fragmentação que o GUID v4 aleatório causa ao inserir sempre no meio
    ///     da árvore.
    /// </summary>
    public Guid Id { get; protected set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    protected void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

/// <summary>
///     Costura de multi-tenant.
///     Hoje só existe um dono e o filtro será no-op. Mas toda entidade raiz já
///     nasce com OwnerId, porque adicionar isso depois significa revisar todas
///     as queries do sistema — e um filtro esquecido vira vazamento de dados
///     entre organizações, que é incidente de segurança, não bug.
///     O custo agora é uma propriedade. Depois é uma semana.
/// </summary>
public interface IOwnedEntity
{
    Guid OwnerId { get; }
}