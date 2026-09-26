using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Messaging.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Constants;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Contracts.Models;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Cards;

namespace MIN.Desktop.ViewModels.Pages.ChatViewModels;

/// <summary>
/// Методы использования сервисов для чата
/// </summary>
public partial class ChatViewModel : RoutableViewModelBase
{
    private readonly AvaloniaList<IDescribableStatus> currentStatuses = [];
    private readonly List<ParticipantInfo> currentTypingParticipants = [];
    private Guid currentTypingParticipantsStatusId;

    /// <summary>
    /// Приложенные файлы
    /// </summary>
    public AvaloniaList<FileAttachmentViewModel> AttachedFiles { get; } = [];

    /// <summary>
    /// Показать статус
    /// </summary>
    public bool ShowStatus => currentStatuses.Count > 0;

    /// <summary>
    /// Флаг переключения автоскролла вниз
    /// </summary>
    [ObservableProperty]
    public partial bool AutoScrollBottom { get; set; }

    /// <summary>
    /// Является ли сейчас ползунок в самом низу scroll viewer
    /// </summary>
    [ObservableProperty]
    public partial bool IsAtBottom { get; set; }

    partial void OnIsAtBottomChanged(bool value)
    {
        if (value == true)
        {
            MissedMessagesCount = 0;
        }
    }

    [ObservableProperty]
    public partial string StatusContent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int MissedMessagesCount { get; set; }

    /// <summary>
    /// Показать приложенные файлаы
    /// </summary>
    public bool SomeFilesAttached => AttachedFiles.Count > 0;

    #region Update

    private async Task UpdateChatFlow()
    {
        Messages.Clear();

        var context = featureCollection.Core.RoomFactory.GetOrCreateContext(roomId);
        var messages = context.Messages.GetHistory().ToList();

        await RenderMessages(messages);

        if (room.TotalMessageCount > StoreConstants.MessagesPageSize)
        {
            ShowLoadMoreLabel();
            oldestLoadedTimestamp = messages[0].Timestamp;
            oldestLoadedMessageId = messages[0].Id;
        }
    }

    private async Task RenderMessages(List<IMessage> messages, bool appendOnTop = false)
    {
        foreach (var message in messages)
        {
            await AddMessageToChatFlow(message, appendOnTop);
        }
    }

    private void InitializeObservableCollections()
    {
        currentStatuses.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowStatus));
        AttachedFiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SomeFilesAttached));
            SendMessageCommand.NotifyCanExecuteChanged();
        };
    }

    #endregion

    #region Helper methods

    private void AddStatus(IDescribableStatus status)
    {
        currentStatuses.Add(status);
        StatusContent = string.Join(", ", currentStatuses.Select(x => x.GetDescription()));
    }

    private void RemoveStatus(Guid statusId)
    {
        var foundStatus = currentStatuses.FirstOrDefault(x => x.Id == statusId);
        if (foundStatus != null)
        {
            currentStatuses.Remove(foundStatus);
        }

        if (currentStatuses.Count != 0)
        {
            StatusContent = string.Join(", ", currentStatuses.Select(x => x.GetDescription()));
        }
    }

    private void UploadFile(string filePath)
    {
        var fileAttachment = new FileAttachment(Path.GetFileName(filePath),
            filePath);

        var fileVm = new FileAttachmentViewModel(fileAttachment);
        fileVm.OnDelete += () => AttachedFiles.Remove(fileVm);

        AttachedFiles.Add(fileVm);
    }

    [RelayCommand]
    private async Task ScrollToBottom()
    {
        AutoScrollBottom = true;
        await Task.Yield();
        AutoScrollBottom = false;
    }

    #endregion
}
