using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Common.Core.Extensions;
using MIN.Desktop.Contracts.Constants;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.Infrastructure.Validators;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Modals;
using MIN.DI.FeatureCollection;
using MIN.Helpers.Contracts.Models;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Desktop.ViewModels.Pages;

/// <summary>
/// Модель боковой панели настроек
/// </summary>
public partial class SettingsSideBarViewModel : ValidatingRoutableViewModelBase
{
    private readonly IMinFeatureCollection featureCollection;
    private readonly IDialogService dialogService;
    private readonly CancellationTokenSource appCts = null!;

    /// <inheritdoc />
    public override ViewLayoutType LayoutType => ViewLayoutType.LeftSideBar;

    /// <summary>
    /// Текущие настройки
    /// </summary>
    [ObservableProperty]
    public partial Settings Settings { get; set; } = null!;

    /// <summary>
    /// Микрофоны
    /// </summary>
    public AvaloniaList<string> InputDevices { get; set; } = [];

    /// <summary>
    /// Динамики
    /// </summary>
    public AvaloniaList<string> OutputDevices { get; set; } = [];

    /// <summary>
    /// Доступные шумоподавления
    /// </summary>
    public AvaloniaList<string> NoiseReductions { get; set; } = [];

    /// <summary>
    /// Версия приложения
    /// </summary>
    [ObservableProperty]
    public partial string Version { get; set; } = string.Empty;

    /// <summary>
    /// Имя своего участника
    /// </summary>
    [ObservableProperty]
    [Display(Name = "Имя участника")]
    [NotifyDataErrorInfo]
    [ParticipantName]
    [NotEndsWith(".")]
    public partial string ParticipantName { get; set; } = string.Empty;

    /// <summary>
    /// Время ожидания поиска комнаты
    /// </summary>
    [ObservableProperty]
    [IntValue]
    [Range(100, DesktopConstants.RoomConnectionTimeoutMs, ErrorMessage = "Время ожидания поиска комнаты должно быть от 100 до 10000 миллисекунд")]
    [NotifyDataErrorInfo]
    public partial int DiscoveryTimeout { get; set; }

    /// <summary>
    /// Порт для обнаружения в сети
    /// </summary>
    [ObservableProperty]
    [IntValue]
    [Range(1, ushort.MaxValue, ErrorMessage = "Порт должен быть от 1 до 65535")]
    [NotifyDataErrorInfo]
    public partial int DiscoveryPort { get; set; }

    /// <summary>
    /// Включена ли светлая тема
    /// </summary>
    [ObservableProperty]
    public partial bool LightThemeEnabled { get; set; }

    /// <summary>
    /// Выбранное (по номеру) шумоподавление
    /// </summary>
    [ObservableProperty]
    public partial int ChoosenNoiseReduction { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SettingsSideBarViewModel"/>
    /// </summary>
    public SettingsSideBarViewModel(IMinFeatureCollection featureCollection,
        IDialogService dialogService,
        ICtsProvider ctsProvider)
    {
        this.featureCollection = featureCollection;
        this.dialogService = dialogService;

        if (!Design.IsDesignMode)
        {
            appCts = ctsProvider.AppCts;

            var inputDeviceNames = featureCollection.Voice.AudioDeviceService.GetInputDevices(asDecoded: true).Select(x => x.Name);
            foreach (var name in inputDeviceNames)
            {
                InputDevices.Add(name);
            }

            var outputDeviceNames = featureCollection.Voice.AudioDeviceService.GetOutputDevices(asDecoded: true).Select(x => x.Name);
            foreach (var name in outputDeviceNames)
            {
                OutputDevices.Add(name);
            }

            foreach (NoiseReduction denoiser in Enum.GetValues(typeof(NoiseReduction)))
            {
                NoiseReductions.Add(denoiser.GetDescription());
            }

            featureCollection.Helper.SettingsProvider.OnSettingsSaved += FillControls;

            FillControls();

            Application.Current!.RequestedThemeVariant = Settings.LightThemeEnabled
                ? ThemeVariant.Light
                : ThemeVariant.Dark;
        }
    }

    partial void OnLightThemeEnabledChanged(bool value)
    {
        Dispatcher.UIThread.Invoke(() => Application.Current!.RequestedThemeVariant = value ? ThemeVariant.Light : ThemeVariant.Dark);
    }

    /// <summary>
    /// Вернуться назад
    /// </summary>
    [RelayCommand]
    public async Task Back()
    {
        if (CanSave())
        {
            Settings.DiscoveryPort = DiscoveryPort;
            Settings.DiscoveryTimeout = DiscoveryTimeout;
            var localParticipant = featureCollection.Core.IdentityService.SelfParticipant;
            localParticipant.Name = ParticipantName;
            await featureCollection.Core.IdentityService.SaveParticipant(localParticipant);
        }

        Settings.LightThemeEnabled = LightThemeEnabled;

        if (ChoosenNoiseReduction >= 0 && ChoosenNoiseReduction < Enum.GetValues(typeof(NoiseReduction)).Length)
        {
            Settings.NoiseReduction = (NoiseReduction)ChoosenNoiseReduction;
        }

        await featureCollection.Helper.SettingsProvider.SaveSettings(Settings);
        ChangeViewToPrevious();
    }

    /// <summary>
    /// Открыть окно логов
    /// </summary>
    [RelayCommand]
    public async Task OpenLogsAsync()
    {
        await dialogService.ShowAsync<LogViewModel>();
        ChangeViewToPrevious();
    }

    /// <summary>
    /// Очистить кэш
    /// </summary>
    [RelayCommand]
    public void ClearCacheAsync()
    {
        featureCollection.Helper.AppDataProvider.ClearFolder("cryptography");
        featureCollection.Helper.AppDataProvider.ClearFolder("network");
        InAppNotifier.Success("Кэш был успешно очищен");
    }

    /// <summary>
    /// Отсканировать папку с сессиями
    /// </summary>
    [RelayCommand]
    public async Task ScanSessionsAsync()
    {
        await featureCollection.Chat.ChatSessionService.ScanDownloadedSessions(appCts.Token);
        InAppNotifier.Info($"Найдено установленных активностей: {featureCollection.Sessions.SessionScanner.DownloadedSessions.Count}");
    }

    private void FillControls()
    {
        Version = $"Версия: {featureCollection.Helper.VersionProvider.Version}";

        Settings = featureCollection.Helper.SettingsProvider.GetSettings();
        LightThemeEnabled = Settings.LightThemeEnabled;
        DiscoveryTimeout = Settings.DiscoveryTimeout;
        DiscoveryPort = Settings.DiscoveryPort;
        ChoosenNoiseReduction = (int)Settings.NoiseReduction;

        ParticipantName = featureCollection.Core.IdentityService.SelfParticipant.Name;
    }

    private bool CanSave() => !HasErrors;
}
