using ReactiveUI;
using TCMine_Domain.Launcher;

namespace TCMine_Launcher.ViewModels;

/// <summary>Editor de memória (RAM) de uma instância — usado pela janela de Memória (footer e Instâncias).</summary>
public sealed class MemoryEditViewModel(MainWindowViewModel shell, InstalledModpack instance) : ViewModelBase
{
    private double _ramMb = shell.EffectiveRam(instance);

    public string InstanceName => instance.Name;
    public double RamMin => 1024;
    public double RamMax => shell.RamHardMax;

    public double RamMb
    {
        get => _ramMb;
        set
        {
            this.RaiseAndSetIfChanged(ref _ramMb, value);
            instance.RamOverrideMb = (int)value;
            shell.SaveInstance(instance);
            this.RaisePropertyChanged(nameof(RamLabel));
        }
    }

    public string RamLabel => $"{(int)RamMb} MB ({RamMb / 1024:0.0} GB)";
}