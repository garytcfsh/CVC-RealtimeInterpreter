using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CVC.RealtimeInterpreter.Core.Providers;

namespace CVC.RealtimeInterpreter.Desktop.ViewModels;

public sealed record ProviderOption(Guid Id, string Name, string Tags);

public partial class ServiceAssignmentViewModel(ProviderCapability role,
    Action<ProviderCapability, ServiceAssignment?> commit) : ObservableObject
{
    private bool rebuilding;
    private bool chinese;

    [ObservableProperty]
    private IReadOnlyList<ProviderOption> providers = [];

    [ObservableProperty]
    private IReadOnlyList<ProviderModel> models = [];

    [ObservableProperty]
    private ProviderOption? selectedProvider;

    [ObservableProperty]
    private ProviderModel? selectedModel;

    public ProviderCapability Role => role;
    public string Label => role switch
    {
        ProviderCapability.Stt => chinese ? "語音辨識 (STT)" : "Speech recognition (STT)",
        ProviderCapability.Translation => chinese ? "翻譯" : "Translation",
        _ => chinese ? "語音生成" : "Speech generation"
    };
    public string ClearLabel => chinese ? "清除選擇" : "Clear selection";
    public string ProviderLabel => chinese ? "供應商" : "Provider";
    public string ModelLabel => chinese ? "模型" : "Model";

    public void Refresh(ProviderConfiguration configuration, bool useChinese)
    {
        rebuilding = true;
        try
        {
            chinese = useChinese;
            ServiceAssignment? assignment = configuration.AssignmentFor(role);
            Providers = configuration.Profiles.Where(profile => profile.Models.Any(model => ProviderCatalog.Supports(model, role)))
                .Select(profile => Providers.FirstOrDefault(option => option.Id == profile.Id && option.Name == profile.Name)
                    ?? new ProviderOption(profile.Id, profile.Name, profile.Capabilities.ToString())).ToArray();
            SelectedProvider = Providers.FirstOrDefault(provider => provider.Id == assignment?.ProviderId);
            Models = configuration.Profiles.FirstOrDefault(provider => provider.Id == assignment?.ProviderId)?.Models
                .Where(model => ProviderCatalog.Supports(model, role)).ToArray() ?? [];
            SelectedModel = Models.FirstOrDefault(model => model.Id == assignment?.ModelId);
            OnPropertyChanged(nameof(Label));
            OnPropertyChanged(nameof(ClearLabel));
            OnPropertyChanged(nameof(ProviderLabel));
            OnPropertyChanged(nameof(ModelLabel));
        }
        finally
        {
            rebuilding = false;
        }
    }

    partial void OnSelectedProviderChanged(ProviderOption? value)
    {
        if (!rebuilding && value is not null)
        {
            commit(role, new(value.Id, null));
        }
    }

    partial void OnSelectedModelChanged(ProviderModel? value)
    {
        if (!rebuilding && SelectedProvider is not null && value is not null)
        {
            commit(role, new(SelectedProvider.Id, value.Id));
        }
    }

    [RelayCommand]
    private void Clear()
    {
        commit(role, null);
    }
}
