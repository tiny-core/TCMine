using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     A instalação/atualização de modpack em curso, se houver — sobrevive à
///     navegação entre páginas.
///     Antes disto, o progresso vivia em campos locais de <c>ModpacksPage</c> e
///     <c>InstancesPage</c>: sair da tela no meio de uma instalação e voltar não
///     mostrava nada rodando — a operação continuava por trás o tempo todo, só
///     o jogador não via — e um toast avisava quando terminava, sem explicar o
///     vazio de progresso no meio do caminho.
///     Singleton, como o <see cref="LauncherShellState" />, e pelo mesmo motivo.
///     Só uma operação de cada vez no launcher inteiro (duas disputariam o
///     mesmo content store e a mesma banda — ver o comentário original em
///     ModpacksPage), então um único slot compartilhado é suficiente: não é um
///     registro por Id.
/// </summary>
public sealed class InstallOperationState
{
    /// <summary>
    ///     Modpack alvo, quando a operação nasceu no catálogo — é por ele que
    ///     <c>ModpacksPage</c> sabe em qual card mostrar a barra ao reabrir a
    ///     tela. Null quando a operação é uma atualização de instância já
    ///     existente (essa não tem um card de catálogo para apontar).
    /// </summary>
    public Guid? ModpackId { get; private set; }

    public InstallProgress? Progress { get; private set; }

    public bool IsRunning => Progress is not null;

    public event Action? Changed;

    public void Begin(Guid? modpackId)
    {
        ModpackId = modpackId;
        Progress = InstallProgress.Planning;
        Changed?.Invoke();
    }

    public void Report(InstallProgress progress)
    {
        Progress = progress;
        Changed?.Invoke();
    }

    public void Finish()
    {
        ModpackId = null;
        Progress = null;
        Changed?.Invoke();
    }
}
