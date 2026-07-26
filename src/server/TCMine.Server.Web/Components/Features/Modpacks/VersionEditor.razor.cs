using Microsoft.AspNetCore.Components;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class VersionEditor : ComponentBase
{
    private VersionChannel _channel = VersionChannel.Alpha;
    private string? _lastValue;

    private int _major = 1, _minor, _patch;
    [Parameter] public string Value { get; set; } = "";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    private int Major
    {
        get => _major;
        set
        {
            _major = value;
            Emit();
        }
    }

    private int Minor
    {
        get => _minor;
        set
        {
            _minor = value;
            Emit();
        }
    }

    private int Patch
    {
        get => _patch;
        set
        {
            _patch = value;
            Emit();
        }
    }

    private VersionChannel Channel
    {
        get => _channel;
        set
        {
            _channel = value;
            Emit();
        }
    }

    protected override void OnParametersSet()
    {
        // Só re-parse quando o Value muda de fora — não quando fomos nós a emitir
        // (senão entrava em loop de parse↔format).
        if (Value == _lastValue) return;
        _lastValue = Value;
        (_major, _minor, _patch, _channel) = PackVersion.Parse(Value);
    }

    private void Emit()
    {
        var combined = PackVersion.Format(_major, _minor, _patch, _channel);
        _lastValue = combined;
        _ = ValueChanged.InvokeAsync(combined);
    }
}