using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CVC.RealtimeInterpreter.Core.Providers;

namespace CVC.RealtimeInterpreter.Desktop.ViewModels;

public partial class ProviderSettingsViewModel : ObservableObject
{
    private readonly IProviderCredentialStore credentials;
    private readonly Action<ProviderConfiguration> save;
    private readonly Dictionary<Guid, (CancellationTokenSource Cancellation, Task Task)> keyWrites = [];
    private readonly SemaphoreSlim keyGate = new(1, 1);
    private readonly HashSet<Guid> storedKeys = [];
    private readonly List<CancellationTokenSource> keyCancellations = [];
    private bool refreshing;
    private bool chinese;
    private bool shuttingDown;

    public ProviderSettingsViewModel(ProviderConfiguration state, IProviderCredentialStore credentials,
        Action<ProviderConfiguration> save, bool useChinese)
    {
        this.credentials = credentials;
        this.save = save;
        State = state;
        chinese = useChinese;
        Assignments = ProviderCatalog.Roles.Select(role => new ServiceAssignmentViewModel(role, SetAssignment)).ToArray();
        Refresh();
    }

    public ProviderConfiguration State
    {
        get; private set;
    }
    public IReadOnlyList<ServiceAssignmentViewModel> Assignments
    {
        get;
    }
    public IReadOnlyList<SettingOption<ProviderConnectionType>> ConnectionTypes
    {
        get;
    } =
    [
        new(ProviderConnectionType.OpenAi, "OpenAI"),
        new(ProviderConnectionType.AzureSpeech, "Azure Speech"),
        new(ProviderConnectionType.OpenAiCompatible, "OpenAI-compatible")
    ];

    [ObservableProperty]
    private IReadOnlyList<ProviderOption> profiles = [];
    [ObservableProperty]
    private ProviderOption? selectedProfile;
    [ObservableProperty]
    private string profileName = string.Empty;
    [ObservableProperty]
    private string endpoint = string.Empty;
    [ObservableProperty]
    private ProviderConnectionType connectionType;
    [ObservableProperty]
    private IReadOnlyList<ProviderModel> configuredModels = [];
    [ObservableProperty]
    private ProviderModel? selectedConfiguredModel;
    [ObservableProperty]
    private string newModelId = string.Empty;
    [ObservableProperty]
    private bool modelStt;
    [ObservableProperty]
    private bool modelTranslation;
    [ObservableProperty]
    private bool modelSpeech;
    [ObservableProperty]
    private bool modelStreaming = true;
    [ObservableProperty]
    private string status = string.Empty;
    [ObservableProperty]
    private bool busy;

    public bool HasSelectedProfile => SelectedProfile is not null;
    public bool CanManage => !Busy && !shuttingDown;
    public bool IsEditable => HasSelectedProfile && !Busy;
    public string Heading => chinese ? "供應商連線" : "Provider connections";
    public string AssignmentsHeading => chinese ? "服務指派" : "Service assignments";
    public string NameLabel => chinese ? "名稱" : "Name";
    public string EndpointLabel => chinese ? "服務網址" : "Endpoint";
    public string ConnectionLabel => chinese ? "連線類型" : "Connection type";
    public string ApiKeyLabel => chinese ? "API 金鑰（自動安全儲存）" : "API key (saved securely automatically)";
    public string KeyStatus => storedKeys.Contains(SelectedProfile?.Id ?? Guid.Empty)
        ? chinese ? "此 Windows 使用者已儲存金鑰；輸入新值可取代。" : "Key stored for this Windows user; enter a value to replace it."
        : chinese ? "尚未儲存 API 金鑰。" : "No API key stored.";
    public string AddLabel => chinese ? "新增供應商" : "Add provider";
    public string DeleteLabel => chinese ? "刪除供應商" : "Delete provider";
    public string RemoveKeyLabel => chinese ? "移除金鑰" : "Remove key";
    public string ModelsLabel => chinese ? "模型與能力" : "Models and capabilities";
    public string AddModelLabel => chinese ? "新增模型" : "Add model";
    public string RemoveModelLabel => chinese ? "移除模型" : "Remove model";
    public string ModelIdLabel => chinese ? "模型識別碼" : "Model ID";
    public string StreamingLabel => chinese ? "串流／即時結果" : "Streaming / live results";
    public string TranslationLabel => chinese ? "翻譯" : "Translation";
    public string SpeechLabel => chinese ? "語音生成" : "Speech generation";
    public string AssignmentHelp => chinese ? "為每個功能分別選擇供應商及模型。信心度不是必要條件。" : "Choose a provider and model for each function. Confidence scores are optional.";
    public string ModelHelp => chinese
        ? "自訂模型的能力由您宣告，尚未連線驗證。STT 模型必須支援即時結果。Azure 模型名稱代表服務預設系列。"
        : "Custom-model capabilities are user-declared and unverified. STT needs live results. Azure model IDs represent service-default families.";
    public string CapabilityTags => SelectedProfile?.Tags ?? string.Empty;

    public void SetLanguage(bool useChinese)
    {
        chinese = useChinese;
        ConnectionTypes[2].Label = chinese ? "OpenAI 相容格式" : "OpenAI-compatible";
        foreach (string property in new[]
        {
            nameof(Heading), nameof(AssignmentsHeading), nameof(NameLabel), nameof(EndpointLabel), nameof(ConnectionLabel),
            nameof(ApiKeyLabel), nameof(KeyStatus), nameof(AddLabel), nameof(DeleteLabel), nameof(RemoveKeyLabel),
            nameof(ModelsLabel), nameof(AddModelLabel), nameof(RemoveModelLabel), nameof(ModelIdLabel), nameof(StreamingLabel),
            nameof(TranslationLabel), nameof(SpeechLabel), nameof(AssignmentHelp), nameof(ModelHelp)
        })
        {
            OnPropertyChanged(property);
        }

        foreach (ServiceAssignmentViewModel assignment in Assignments)
        {
            assignment.Refresh(State, chinese);
        }
    }

    private static string Tags(ProviderProfile profile) => string.Join(" · ", ProviderCatalog.Roles
        .Where(role => profile.Capabilities.HasFlag(role)).Select(role => role switch
        {
            ProviderCapability.Stt => "STT",
            ProviderCapability.Translation => "Translation",
            _ => "Speech generation"
        }));

    private void Refresh(Guid? select = null)
    {
        Guid? id = select ?? SelectedProfile?.Id;
        refreshing = true;
        try
        {
            Profiles = State.Profiles.Select(profile =>
                Profiles.FirstOrDefault(option => option.Id == profile.Id && option.Name == profile.Name && option.Tags == Tags(profile))
                ?? new ProviderOption(profile.Id, profile.Name, Tags(profile))).ToArray();
            SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == id) ?? Profiles.FirstOrDefault();
            LoadEditor();
        }
        finally
        {
            refreshing = false;
        }

        foreach (ServiceAssignmentViewModel assignment in Assignments)
        {
            assignment.Refresh(State, chinese);
        }

        SetLanguage(chinese);
    }

    private void LoadEditor()
    {
        ProviderProfile? profile = State.Profiles.FirstOrDefault(item => item.Id == SelectedProfile?.Id);
        ProfileName = profile?.Name ?? string.Empty;
        Endpoint = profile?.Endpoint ?? string.Empty;
        ConnectionType = profile?.ConnectionType ?? ProviderConnectionType.OpenAiCompatible;
        ConfiguredModels = profile?.Models ?? [];
        SelectedConfiguredModel = null;
        OnPropertyChanged(nameof(HasSelectedProfile));
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(CapabilityTags));
        OnPropertyChanged(nameof(KeyStatus));
    }

    partial void OnSelectedProfileChanged(ProviderOption? value)
    {
        if (!refreshing && value is not null)
        {
            refreshing = true;
            LoadEditor();
            refreshing = false;
            Status = string.Empty;
        }
    }

    partial void OnBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(CanManage));
    }

    partial void OnProfileNameChanged(string value) => UpdateProfile();
    partial void OnEndpointChanged(string value) => UpdateProfile();
    partial void OnConnectionTypeChanged(ProviderConnectionType value) => UpdateProfile();

    private bool Apply(ProviderConfiguration next, Guid? select = null)
    {
        try
        {
            next.Validate();
            State = next;
            save(next);
            Status = string.Empty;
            Refresh(select);
            return true;
        }
        catch (InvalidDataException exception)
        {
            Status = (chinese ? "無法套用：" : "Could not apply: ") + exception.Message;
            return false;
        }
    }

    private void UpdateProfile()
    {
        if (refreshing || SelectedProfile is null)
        {
            return;
        }

        Guid id = SelectedProfile.Id;
        ProviderProfile current = State.Profiles.Single(profile => profile.Id == id);
        ProviderProfile updated = current with
        {
            Name = ProfileName,
            Endpoint = Endpoint,
            ConnectionType = ConnectionType,
            Models = current.ConnectionType == ConnectionType ? current.Models : ProviderCatalog.ModelsFor(ConnectionType)
        };
        Apply(State with
        {
            Profiles = State.Profiles.Select(profile => profile.Id == id ? updated : profile).ToArray()
        }, id);
    }

    private void SetAssignment(ProviderCapability role, ServiceAssignment? assignment)
    {
        Apply(State.WithAssignment(role, assignment));
    }

    [RelayCommand]
    private void AddProvider()
    {
        Guid id = Guid.NewGuid();
        ProviderProfile profile = new(id, chinese ? "新供應商" : "New provider", ProviderConnectionType.OpenAiCompatible, "", []);
        Apply(State with
        {
            Profiles = [.. State.Profiles, profile]
        }, id);
    }

    [RelayCommand]
    private async Task DeleteProviderAsync()
    {
        if (SelectedProfile is null || Busy)
        {
            return;
        }

        Guid id = SelectedProfile.Id;
        ProviderConfiguration next = State with
        {
            Profiles = State.Profiles.Where(profile => profile.Id != id).ToArray()
        };
        try
        {
            next.Validate();
            Busy = true;
            await FlushCredentialsAsync();
            await credentials.DeleteKeyAsync(id);
            storedKeys.Remove(id);
            Apply(next);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Status = exception.Message;
        }
        finally
        {
            Busy = false;
        }
    }

    [RelayCommand]
    private void AddModel()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        ProviderCapability capabilities = (ModelStt ? ProviderCapability.Stt : ProviderCapability.None) |
            (ModelTranslation ? ProviderCapability.Translation : ProviderCapability.None) |
            (ModelSpeech ? ProviderCapability.SpeechGeneration : ProviderCapability.None);
        ProviderProfile profile = State.Profiles.Single(item => item.Id == SelectedProfile.Id);
        ProviderModel? known = ProviderCatalog.ModelsFor(profile.ConnectionType).FirstOrDefault(model => model.Id == NewModelId.Trim());
        ProviderModel model = known ?? new(NewModelId.Trim(), capabilities, ModelStreaming);
        ProviderProfile updated = profile with
        {
            Models = [.. profile.Models, model]
        };
        if (Apply(State with
        {
            Profiles = State.Profiles.Select(item => item.Id == profile.Id ? updated : item).ToArray()
        }))
        {
            NewModelId = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveModel()
    {
        if (SelectedProfile is null || SelectedConfiguredModel is null)
        {
            return;
        }

        ProviderProfile profile = State.Profiles.Single(item => item.Id == SelectedProfile.Id);
        ProviderProfile updated = profile with
        {
            Models = profile.Models.Where(model => model.Id != SelectedConfiguredModel.Id).ToArray()
        };
        Apply(State with
        {
            Profiles = State.Profiles.Select(item => item.Id == profile.Id ? updated : item).ToArray()
        });
    }

    public async Task InitializeCredentialsAsync()
    {
        try
        {
            foreach (ProviderProfile profile in State.Profiles)
            {
                if (await credentials.HasKeyAsync(profile.Id))
                {
                    storedKeys.Add(profile.Id);
                }
            }

            OnPropertyChanged(nameof(KeyStatus));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Status = exception.Message;
        }
    }

    public void QueueApiKey(string key)
    {
        if (shuttingDown || SelectedProfile is null || string.IsNullOrEmpty(key))
        {
            return;
        }

        Guid id = SelectedProfile.Id;
        if (keyWrites.TryGetValue(id, out var previous))
        {
            previous.Cancellation.Cancel();
        }

        CancellationTokenSource cancellation = new();
        keyCancellations.Add(cancellation);
        Task writing = WriteKeyAsync(id, key, cancellation.Token);
        keyWrites[id] = (cancellation, writing);
    }

    private async Task WriteKeyAsync(Guid id, string key, CancellationToken cancellation)
    {
        try
        {
            await Task.Delay(350, cancellation);
            await keyGate.WaitAsync(cancellation);
            try
            {
                await credentials.WriteKeyAsync(id, key);
                storedKeys.Add(id);
                Status = string.Empty;
                OnPropertyChanged(nameof(KeyStatus));
            }
            finally
            {
                keyGate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // A newer edit for the same provider replaces this draft.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Status = exception.Message;
        }
    }

    public async Task FlushCredentialsAsync()
    {
        await Task.WhenAll(keyWrites.Values.Select(item => item.Task).ToArray());
    }

    public async Task ShutdownAsync()
    {
        shuttingDown = true;
        await FlushCredentialsAsync();
        foreach (CancellationTokenSource cancellation in keyCancellations)
        {
            cancellation.Dispose();
        }
    }

    [RelayCommand]
    private async Task RemoveKeyAsync()
    {
        if (SelectedProfile is null)
        {
            return;
        }

        Guid id = SelectedProfile.Id;
        try
        {
            Busy = true;
            await FlushCredentialsAsync();
            await credentials.DeleteKeyAsync(id);
            storedKeys.Remove(id);
            OnPropertyChanged(nameof(KeyStatus));
            Status = string.Empty;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Status = exception.Message;
        }
        finally
        {
            Busy = false;
        }
    }
}
