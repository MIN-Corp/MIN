using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Contracts.Models.Enums;
using MIN.Desktop.Infrastructure.Services;
using MIN.FileTransfer.DI.FeatureCollection;
using MIN.FileTransfer.Events;
using MIN.FileTransfer.Messaging;

namespace MIN.Desktop.ViewModels.Cards.Messages.Files;

public abstract partial class ChatFileBaseMessageViewModel : BaseTextContentChatMessageViewModel
{
    /// <summary>
    /// Функциональность для обмена файлов
    /// </summary>
    readonly protected IFileTransferFeatureCollection fileTransferFeatureCollection;

    /// <summary>
    /// Буфер обмена
    /// </summary>
    readonly protected IClipboard? clipboard;

    /// <summary>
    /// Scope событий для комнаты
    /// </summary>
    readonly protected IEventScope roomScope;

    /// <summary>
    /// Локальный пользователь
    /// </summary>
    readonly protected ParticipantInfo localParticipant;

    /// <summary>
    /// Закешированный формат
    /// </summary>
    readonly protected string cachedFormat;

    /// <summary>
    /// Подкиска на прогресс скачивание
    /// </summary>
    protected IDisposable fileTransferProgressSubsciptionToken = null!;

    /// <summary>
    /// Сообщение файла
    /// </summary>
    public FileMetadataMessage FileMetadataMessage { get; init; }

    [ObservableProperty]
    public partial int DownloadProgress { get; set; }

    [ObservableProperty]
    public partial bool IsTransfering { get; set; }

    [ObservableProperty]
    public partial bool Downloaded { get; set; }

    [ObservableProperty]
    public partial FileDownloadState FileDownloadState { get; set; } = FileDownloadState.None;

    /// <summary>
    /// Запрос на скавивания
    /// </summary>
    public event Func<Task>? OnDownloadRequested;

    /// <summary>
    /// Запрос отмены
    /// </summary>
    public event Func<Task>? OnCancelRequested;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatFileBaseMessageViewModel"/>
    /// </summary>
    protected ChatFileBaseMessageViewModel(IFileTransferFeatureCollection fileTransferFeatureCollection,
        IDialogService dialogService,
        IEventScope roomScope,
        FileMetadataMessage fileMetadataMessage,
        Thickness timePadding,
        ParticipantInfo localParticipant,
        bool isHostMessage,
        bool removeHeaders,
        IClipboard? clipboard,
        bool isAvaibleForNetwork)
        : base(fileMetadataMessage,
            fileMetadataMessage,
            fileMetadataMessage,
            dialogService,
            fileMetadataMessage.Sender.Name,
            timePadding,
            localParticipant.Id == fileMetadataMessage.Sender.Id,
            isHostMessage,
            removeHeaders,
            isAvaibleForNetwork)
    {
        this.fileTransferFeatureCollection = fileTransferFeatureCollection;
        this.localParticipant = localParticipant;
        this.clipboard = clipboard;
        this.roomScope = roomScope;
        FileMetadataMessage = fileMetadataMessage;

        cachedFormat = fileTransferFeatureCollection.FileHelperService
            .GetFileType(fileMetadataMessage.FileName);

        Downloaded = !string.IsNullOrEmpty(fileMetadataMessage.FilePath) || fileMetadataMessage.AsDownloaded;

        FillLabels();

        if (!(IsLocal && IsHost))
        {
            SubscribeToEvents(roomScope);
        }
    }

    /// <summary>
    /// Заолнить поля
    /// </summary>
    protected virtual void FillLabels() { }

    /// <summary>
    /// Получен пакет файла
    /// </summary>
    protected virtual void OnTransferProgressUpdated(string formattedReceived, string formattedTotal) { }

    /// <summary>
    /// Передача прервалась
    /// </summary>
    protected virtual void OnTransferFailed(string errorMessage) { }

    /// <summary>
    /// Передача завершилась
    /// </summary>
    protected virtual void OnTransferCompleted() { }

    /// <summary>
    /// Вызвать событие скачивания
    /// </summary>
    protected void InvokeDownloadRequested()
    {
        OnDownloadRequested?.Invoke();
    }

    /// <summary>
    /// Вызвать событие отмены
    /// </summary>
    protected void InvokeCancelRequested()
    {
        OnCancelRequested?.Invoke();
    }

    private void SubscribeToEvents(IEventScope roomScope)
    {
        roomScope.Subscribe<FileTransferStartedEvent>(OnFileTransferStarted);
        roomScope.Subscribe<FileTransferFailedEvent>(OnFileTransferFailed);
        roomScope.Subscribe<FileTransferCompletedEvent>(OnFileTransferCompleted);
    }

    private Task OnFileTransferStarted(FileTransferStartedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.FileMetadataId != FileMetadataMessage.Id)
        {
            return Task.CompletedTask;
        }

        IsTransfering = true;

        fileTransferProgressSubsciptionToken = roomScope.Subscribe((FileTransferProgressEvent e, CancellationToken _) =>
        {
            if (eventMessage.FileMetadataId != FileMetadataMessage.Id)
            {
                return Task.CompletedTask;
            }

            var progress = 100 * e.BytesReceived / FileMetadataMessage.FileSize;
            DownloadProgress = (int)Math.Min(progress, 100);
            OnTransferProgressUpdated(
                fileTransferFeatureCollection.FileHelperService.FormatFileSize(e.BytesReceived),
                fileTransferFeatureCollection.FileHelperService.FormatFileSize(eventMessage.FileSize));

            return Task.CompletedTask;
        });

        UpdateDownloadState();
        OnTransferProgressUpdated("0",
            fileTransferFeatureCollection.FileHelperService.FormatFileSize(eventMessage.FileSize));
        return Task.CompletedTask;
    }

    private Task OnFileTransferFailed(FileTransferFailedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.FileMetadataId != FileMetadataMessage.Id
            || eventMessage.SenderId != localParticipant.Id)
        {
            return Task.CompletedTask;
        }

        IsTransfering = false;

        UpdateDownloadState();
        OnTransferFailed(eventMessage.ErrorMessage ?? string.Empty);

        fileTransferProgressSubsciptionToken.Dispose();
        return Task.CompletedTask;
    }

    private Task OnFileTransferCompleted(FileTransferCompletedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.FileMetadataId != FileMetadataMessage.Id)
        {
            return Task.CompletedTask;
        }

        Downloaded = true;
        FileMetadataMessage.FilePath ??= eventMessage.FilePath;
        fileTransferProgressSubsciptionToken?.Dispose();

        IsTransfering = false;

        UpdateDownloadState();
        OnTransferCompleted();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Обновить состояния файла
    /// </summary>
    protected void UpdateDownloadState()
    {
        FileDownloadState = IsTransfering
            ? FileDownloadState.IsDownloading : Downloaded
            ? FileDownloadState.Downloaded : FileDownloadState.NotDownloaded;
    }

    [RelayCommand]
    private async Task CopyNameToClipboard()
    {
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(FileMetadataMessage.FileName);
            InAppNotifier.Info("Скопировано в буфер обмена");
        }
    }

    [RelayCommand]
    private Task ShowInFolder()
    {
        if (!Path.Exists(FileMetadataMessage.FilePath))
        {
            InAppNotifier.Warning("Файл не нашёлся");
            return Task.CompletedTask;
        }

        var dir = Path.GetDirectoryName(FileMetadataMessage.FilePath);
        if (string.IsNullOrEmpty(dir))
        {
            return Task.CompletedTask;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start("explorer.exe", $"/select,\"{FileMetadataMessage.FilePath}\"");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start("open", $"-R \"{FileMetadataMessage.FilePath}\"");
        }
        else // unix
        {
            Process.Start("xdg-open", dir);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Сообщение отредактировано
    /// </summary>
    protected override Task ConfirmEditMessage()
    {
        EditContent = EditContent.Trim();

        if (Content != EditContent)
        {
            OnEditRequested?.Invoke(EditContent);
        }
        else
        {
            IsEditing = false;
        }
        return Task.CompletedTask;
    }
}
