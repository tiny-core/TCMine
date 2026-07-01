using System.Reactive;
using ReactiveUI;
using TCMine_Application.Launcher;

namespace TCMine_Launcher.ViewModels;

/// <summary>Definições globais do launcher: RAM alocada e caminho do Java (persistidos pela shell).</summary>
public sealed class SettingsPageViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private readonly ISystemInfo _systemInfo;
    private double _ramMb;
    private string _javaPath;
    private string? _status;

    public SettingsPageViewModel(MainWindowViewModel shell, ISystemInfo systemInfo)
    {
        _shell = shell;
        _systemInfo = systemInfo;
        _ramMb = shell.Prefs.AllocatedRamMb;
        _javaPath = shell.Prefs.JavaPath ?? "";

        Save = ReactiveCommand.Create(SaveImpl);
    }

    public double RamMin => 1024;
    public double RamMax => Math.Max(2048, _systemInfo.TotalPhysicalRamMb / 1024 * 1024);

    public double RamMb
    {
        get => _ramMb;
        set
        {
            this.RaiseAndSetIfChanged(ref _ramMb, value);
            this.RaisePropertyChanged(nameof(RamLabel));
            Status = null;
        }
    }

    public string RamLabel => $"{(int)RamMb} MB ({RamMb / 1024:0.0} GB)";

    public string JavaPath
    {
        get => _javaPath;
        set
        {
            this.RaiseAndSetIfChanged(ref _javaPath, value);
            Status = null;
        }
    }

    public string? Status
    {
        get => _status;
        private set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public ReactiveCommand<Unit, Unit> Save { get; }

    private void SaveImpl()
    {
        _shell.Prefs.AllocatedRamMb = (int)RamMb;
        _shell.Prefs.JavaPath = string.IsNullOrWhiteSpace(JavaPath) ? null : JavaPath.Trim();
        _shell.SaveSettings();
        Status = "Definições guardadas.";
    }
}
