using ReactiveUI;
using TCMine_Domain.Launcher;

namespace TCMine_Launcher.ViewModels;

/// <summary>Editor de memória (RAM) de uma instância — usado pela janela de Memória (footer e Instâncias).</summary>
public sealed class MemoryEditViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private readonly InstalledModpack _instance;
    private double _ramMb;

    public MemoryEditViewModel(MainWindowViewModel shell, InstalledModpack instance)
    {
        _shell = shell;
        _instance = instance;
        _ramMb = shell.EffectiveRam(instance);
    }

    public string InstanceName => _instance.Name;
    public double RamMin => 1024;
    public double RamMax => _shell.RamHardMax;

    public double RamMb
    {
        get => _ramMb;
        set
        {
            this.RaiseAndSetIfChanged(ref _ramMb, value);
            _instance.RamOverrideMb = (int)value;
            _shell.SaveInstance(_instance);
            this.RaisePropertyChanged(nameof(RamLabel));
        }
    }

    public string RamLabel => $"{(int)RamMb} MB ({RamMb / 1024:0.0} GB)";
}
