namespace TCMine.Launcher.Core.Sync;

/// <summary>
///     Identidade de uma instância local do jogo.
///     A chave é da INSTÂNCIA, e não do par (modpack, versão) como já foi. A
///     diferença decide duas coisas que o jogador pede: atualizar mantendo o
///     mundo — mesma instância, versão nova — e ter duas instalações do mesmo
///     pack, cada uma no seu mundo, para quem joga em servidores que estão em
///     versões diferentes.
///     Com a chave no par, "atualizar" criava sempre uma pasta nova e o mundo
///     ficava para trás na antiga; e "criar outra instância do mesmo pack" não
///     tinha sequer como ser expresso.
///     O identificador É o nome da pasta, e isso não é economia: as instalações
///     que já existem no disco foram nomeadas pela regra antiga, e adotá-las pelo
///     nome que já têm significa que ninguém precisa de renomear pasta nenhuma —
///     nem arriscar o mundo de alguém numa migração.
/// </summary>
public readonly record struct InstanceKey(string Id)
{
    /// <summary>
    ///     Uma instância nova. GUID v7 sem hífens: ordenável por criação, seguro
    ///     em qualquer sistema de ficheiros, e sem nada do nome do pack — acento
    ///     e barra partem-se em algum sistema, e renomear o pack renomearia a
    ///     pasta, forçando descarregar tudo outra vez.
    /// </summary>
    public static InstanceKey New() => new($"{Guid.CreateVersion7():N}");

    public string ToDirectoryName() => Id;
}
