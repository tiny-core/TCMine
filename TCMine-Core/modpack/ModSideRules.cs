namespace TCMine_Core.modpack;

/// <summary>
/// Em que lado um mod roda. Dimensão de domínio <b>compartilhada</b> pelo launcher
/// (monta a instância cliente) e pelo servidor (monta a instância de servidor);
/// por isso, vive no TCMine-Core, e não duplicada nos dois lados.
///
/// Distinto de <c>Target</c> (mod/resourcepack/shaderpack), que é só o destino de pasta no
/// cliente. <see cref="Both"/> é o padrão e o valor 0 (mods normalmente rodam nos dois lados).
/// </summary>
public enum ModSide
{
    Both,
    Client,
    Server
}

/// <summary>
/// Regra única de filtragem por lado — a fonte da verdade para "qual mod vai para o cliente vs
/// para o servidor". Usada pelo manifesto público (launcher) e pela build de servidor, para os
/// dois lados decidirem igual.
/// </summary>
public static class ModSideRules
{
    /// <summary>Vai para a instância do cliente (launcher)</summary>
    public static bool RunsOnClient(ModSide side) => side is ModSide.Both or ModSide.Client;

    /// <summary> Vai para a instância de servidor</summary>
    public static bool RunsOnServer(ModSide side) => side is ModSide.Both or ModSide.Server;
}
