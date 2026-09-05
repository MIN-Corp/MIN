using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Enums;
using MIN.Core.Entities.Contracts.Models;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Models.ReferenceCommands.Layout;
using MIN.Desktop.Infrastructure.Extensions;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Cards;
using MIN.Desktop.ViewModels.Modals;
using MIN.Desktop.Views;

namespace MIN.Desktop.ViewModels.Pages.ChatViewModels;

/// <summary>
/// Методы использования сервисов для чата
/// </summary>
public partial class ChatViewModel : RoutableViewModelBase
{
    private readonly System.Timers.Timer typingTimer = new() { Interval = 3000 };

    private bool isParentWindowActive = true;
    private int? activeVoiceChatSubroomId;

    [ObservableProperty]
    public partial int CaretIndex { get; set; }

    // Updating

    [ObservableProperty]
    public partial bool IsUpdatingNetwork { get; set; }

    [ObservableProperty]
    public partial bool IsCancelingUpdatingNetwork { get; set; }

    private CancellationTokenSource? updatingRoomCts;

    // Voice chat
    private readonly DispatcherTimer callTimer = new(TimeSpan.FromSeconds(1), DispatcherPriority.Background, Dispatcher.UIThread);
    private DateTime callStartedAt;

    /// <summary>
    /// Список участников в звонке
    /// </summary>
    public AvaloniaList<ParticipantVoiceCardViewModel> VoiceChatParticipants { get; } = [];

    /// <summary>
    /// Активен ли сейчас звонок
    /// </summary>
    [ObservableProperty]
    public partial bool IsVoiceChatActive { get; set; }

    /// <summary>
    /// Находится ли локальный пользователь в звонке
    /// </summary>
    [ObservableProperty]
    public partial bool IsInVoiceChat { get; set; }

    /// <summary>
    /// Выключил ли микрофон локальный пользователь
    /// </summary>
    [ObservableProperty]
    public partial bool IsMuted { get; set; }

    /// <summary>
    /// Длительность звонка (если он идёт)
    /// </summary>
    [ObservableProperty]
    public partial TimeSpan CallDuration { get; set; }

    #region Layouting

    [ObservableProperty]
    public partial WindowLayout CurrentLayout { get; private set; }

    private void InitializeLayoutStyles()
    {
        if (parentWindow is MainWindow mainWindow)
        {
            CurrentLayout = mainWindow.CurrentLayout;
        }

        this.RegisterMessageListener<LayoutModeChangedReferenceCommand, ChatViewModel>((msg, _) =>
            CurrentLayout = msg.Layout);
    }

    [RelayCommand]
    private void ToggleRightSideBar()
    {
        if (!chatSideBarViewModel.IsOpened)
        {
            ChangeView(chatSideBarViewModel);
        }
        else
        {
            chatSideBarViewModel.CloseView(this);
        }
    }

    [RelayCommand]
    private void ShowLeftSideBar()
    {
        ChangeView(mainSideBarViewModel);
    }

    #endregion

    #region Timers

    private void InitializeTimers()
    {
        typingTimer.Elapsed += (s, e) => OnTypingTimerStop();
        callTimer.Tick += OnCallTimerTick;
    }

    private void OnTypingTimerStop()
    {
        typingTimer.Stop();
        _ = SendSelfStatusChangedMessage(GetRestingStatus());
    }

    private void OnCallTimerTick(object? sender, EventArgs e)
        => CallDuration = DateTime.Now - callStartedAt;

    private OnlineStatus GetRestingStatus() => isParentWindowActive
            ? OnlineStatus.Online
            : OnlineStatus.Offline;

    #endregion

    #region Chat action events

    [RelayCommand]
    private async Task UploadFileClick()
    {
        var files = await parentWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите файл",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Все файлы") { Patterns = ["*.*"] },
                new FilePickerFileType("Изображения") { Patterns = ["*.png", "*.jpg", "*.jpeg"] },
            ]
        });

        foreach (var file in files)
        {
            UploadFile(file.Path.LocalPath);
        }
    }

    [RelayCommand]
    private async Task StartSessionClick()
    {
        var downloadedSessions = featureCollection.Sessions.SessionScanner.DownloadedSessions.Values;
        if (!downloadedSessions.Any())
        {
            InAppNotifier.Info("У вас не установлена ни одна сессия!");
            return;
        }

        var choosingForm = await dialogService.ShowDialogAsync<SessionChoosingViewModel>();
        if (choosingForm! == true)
        {
            InAppNotifier.Info($"Сессия \"{choosingForm!.SelectedSession!.Name}\" запускается, это может занять некоторе время");
            await SendSessionStartMessage(choosingForm!.SelectedSession!);
        }
    }

    [RelayCommand]
    private async Task StartVoiceCallClick()
        => await SendVoiceCallStartMessage();

    [RelayCommand]
    private async Task JoinVoiceCall()
    {
        if (activeVoiceChatSubroomId == null)
        {
            return;
        }

        await OnVoiceCallJoinRequested(activeVoiceChatSubroomId.Value);
    }

    [RelayCommand]
    private async Task ToggleMute()
    {
        if (IsMuted)
        {
            await OnUnmuteSelfRequested();
        }
        else
        {
            await OnMuteSelfRequested();
        }
        IsMuted = !IsMuted;
    }

    [RelayCommand]
    private async Task LeaveVoiceCall()
    {
        if (activeVoiceChatSubroomId == null)
        {
            return;
        }

        await OnVoiceCallLeaveRequested(activeVoiceChatSubroomId.Value);
    }

    #endregion

    #region Button event attachment

    [RelayCommand]
    private async Task LeaveRoom()
    {
        if (IsHost && room.ParticipantCount > 1)
        {
            bool confirmation = await dialogService.ShowDialogAsync<DialogBoxViewModel>(model =>
            {
                model.Title = $"Закрытие комнаты {room.Name}";
                model.Description = "Вы точно хотите закрыть комнату? "
                + "\nПоскольку вы - хост, вы остановите комнату для всех участников.";
                model.ButtonOptions = ButtonOptions.YesNo;
            });

            if (!confirmation)
            {
                return;
            }
        }

        await Disconnect();
    }

    [RelayCommand]
    private async Task EditRoom()
    {
        var editFormResult = await dialogService.ShowDialogAsync<CreateRoomViewModel>(vm =>
        {
            vm.InitializeWithRoom(new RoomInfo(room), room.LocalRoomSettings.NetworkOptions);
        });

        if (editFormResult == true)
        {
            IsUpdatingNetwork = true;
            updatingRoomCts = CancellationTokenSource.CreateLinkedTokenSource(appCts.Token);

            try
            {
                await featureCollection.Chat.ChatRoomService.UpdateNetworkOutOfSettings(editFormResult.Room,
                    room.ConnectionAddresses, editFormResult.NetworkOptions, room.LocalRoomSettings.NetworkOptions, updatingRoomCts.Token);

                if (editFormResult.Room.Name != room.Name || editFormResult.Room.MaximumParticipants != room.MaximumParticipants)
                {
                    await featureCollection.Chat.ChatRoomService.SendUpdatedRoomInfoAsync(editFormResult.Room, updatingRoomCts.Token);
                }

                chatSideBarViewModel.UpdateStats(room);

                InAppNotifier.Success("Комната успешно была обновлена!");
            }
            catch (OperationCanceledException)
            {
                InAppNotifier.Info("Обновление комнаты было отменено");
            }
            catch (Exception ex)
            {
                InAppNotifier.Error(ex.Message);
            }
            finally
            {
                IsUpdatingNetwork = false;
                IsCancelingUpdatingNetwork = false;
                updatingRoomCts = null;
            }
        }
    }

    [RelayCommand]
    private async Task CancelRoomUpdate()
    {
        IsCancelingUpdatingNetwork = true;

        if (updatingRoomCts != null)
        {
            await updatingRoomCts.CancelAsync();
        }
    }

    #endregion

    #region MessageTextBox events

    [RelayCommand]
    private void MessageTextChanged()
    {
        if (string.IsNullOrEmpty(SendingMessage))
        {
            OnTypingTimerStop();
        }
        else
        {
            if (!typingTimer.Enabled)
            {
                _ = SendSelfStatusChangedMessage(OnlineStatus.Typing);
            }

            typingTimer.Stop();
            typingTimer.Start();
        }
    }

    #endregion

    #region Parent form events

    private void InitializeParentFormWindowStateEvents()
    {
        parentWindow.Activated += Parent_Activated;
        parentWindow.Deactivated += Parent_Deactivate;
    }

    private void ClearParentFormEvents()
    {
        parentWindow.Activated -= Parent_Activated;
        parentWindow.Deactivated -= Parent_Deactivate;

        featureCollection.Helper.NotificationService.OnNotificationClick -= OnNotificationClick;
        featureCollection.Helper.NotificationService.NotificationTurnOffClicked -= NotificationTurnOffClicked;
    }

    private async void Parent_Deactivate(object? sender, EventArgs e)
    {
        typingTimer.Stop();
        await SendSelfStatusChangedMessage(OnlineStatus.Offline);
        isParentWindowActive = false;
    }

    private async void Parent_Activated(object? sender, EventArgs e)
    {
        await SendSelfStatusChangedMessage(OnlineStatus.Online);
        isParentWindowActive = true;
    }

    [RelayCommand]
    private async Task PasteData() => await PasteDataFromClipboard(false);

    [RelayCommand]
    private async Task PasteDataWithText() => await PasteDataFromClipboard(true);

    private async Task PasteDataFromClipboard(bool includingText = false)
    {
        var clipboard = parentWindow.Clipboard;

        if (clipboard == null)
        {
            return;
        }

        var formats = await clipboard.GetDataFormatsAsync();

        if (formats.Contains(DataFormat.File))
        {
            if (await clipboard.TryGetFilesAsync() is IEnumerable<IStorageItem> files)
            {
                foreach (var file in files)
                {
                    if (!string.IsNullOrEmpty(file.Path.AbsolutePath))
                    {
                        UploadFile(file.Path.AbsolutePath);
                    }
                }
                return;
            }
        }

        if (includingText)
        {
            if (formats.Contains(DataFormat.Text))
            {
                if (await clipboard.TryGetTextAsync() is string text)
                {
                    SendingMessage = SendingMessage.Insert(CaretIndex, text);
                    CaretIndex += text.Length;
                    return;
                }
            }
        }

        var image = await clipboard.TryGetBitmapAsync();
        if (image is Bitmap bitmap)
        {
            var timestamp = DateTime.Now.ToString("yyyy-dd-MM-HH-mm-ss-fffff");
            var tempPath = Path.Combine(Path.GetTempPath(), $"clipboard_{timestamp}.png");
            bitmap.Save(tempPath, new PngBitmapEncoderOptions());
            UploadFile(tempPath);
        }
    }

    #endregion

    #region Drag

    [RelayCommand]
    private void DropFiles(List<string> paths)
    {
        foreach (var path in paths)
        {
            UploadFile(path);
        }
    }

    #endregion
}
