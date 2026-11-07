using CommunityToolkit.Mvvm.ComponentModel;

namespace CVC.RealtimeInterpreter.Desktop.ViewModels;

/// <summary>Stable option identity: localization updates its label without resetting a selector.</summary>
public sealed class SettingOption<T>(T value, string label) : ObservableObject
{
    private string label = label;

    public T Value { get; } = value;

    public string Label
    {
        get => label;
        set => SetProperty(ref label, value);
    }
}
