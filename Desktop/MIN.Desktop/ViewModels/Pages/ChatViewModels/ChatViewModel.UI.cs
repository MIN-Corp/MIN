using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Models;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Contracts.Models;
using MIN.Desktop.Contracts.Models.Statuses;
using MIN.Desktop.ViewModels.Cards;

namespace MIN.Desktop.ViewModels.Pages.ChatViewModels;

/// <summary>
/// Методы использования сервисов для чата
/// </summary>
public partial class ChatViewModel
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
        if (value)
        {
            MissedMessagesCount = 0;
        }
    }

    [ObservableProperty]
    public partial string StatusContent { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int MissedMessagesCount { get; set; }

    /// <summary>
    /// Показать приложенные файлы
    /// </summary>
    public bool SomeFilesAttached => AttachedFiles.Count > 0;

    #region Update

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
        RefreshStatusContent();
    }

    private void RemoveStatus(Guid statusId)
    {
        var foundStatus = currentStatuses.FirstOrDefault(x => x.Id == statusId);
        if (foundStatus != null)
        {
            currentStatuses.Remove(foundStatus);
        }

        RefreshStatusContent();
    }

    private void RefreshStatusContent()
        => StatusContent = string.Join(", ", currentStatuses.Select(x => x.GetDescription()));

    private void UpdateTypingStatus()
    {
        var oldStatus = currentStatuses.FirstOrDefault(x => x.Id == currentTypingParticipantsStatusId);
        if (oldStatus == null)
        {
            return;
        }

        var index = currentStatuses.IndexOf(oldStatus);
        currentStatuses[index] = new ParticipantTypingStatus(
            currentTypingParticipantsStatusId,
            currentTypingParticipants.Select(x => x.Name).ToList());
        RefreshStatusContent();
    }

    private void AddTypingParticipant(ParticipantInfo participant)
    {
        if (currentTypingParticipants.Contains(participant))
        {
            return;
        }

        if (currentTypingParticipants.Count == 0)
        {
            currentTypingParticipantsStatusId = Guid.NewGuid();
        }

        currentTypingParticipants.Add(participant);

        if (currentTypingParticipants.Count == 1)
        {
            AddStatus(new ParticipantTypingStatus(
                currentTypingParticipantsStatusId,
                currentTypingParticipants.Select(x => x.Name).ToList()));
        }
        else
        {
            UpdateTypingStatus();
        }
    }

    private void RemoveTypingParticipant(ParticipantInfo participant)
    {
        if (!currentTypingParticipants.Remove(participant))
        {
            return;
        }

        if (currentTypingParticipants.Count == 0)
        {
            RemoveStatus(currentTypingParticipantsStatusId);
        }
        else
        {
            UpdateTypingStatus();
        }
    }

    private void ClearTypingStatus()
    {
        currentTypingParticipants.Clear();
        RemoveStatus(currentTypingParticipantsStatusId);
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
    private Task ScrollToBottomAsync()
    {
        Dispatcher.UIThread.Post(async void () =>
        {
            AutoScrollBottom = true;
            await Task.Yield();
            AutoScrollBottom = false;
        });

        return Task.CompletedTask;
    }

    #endregion
}
