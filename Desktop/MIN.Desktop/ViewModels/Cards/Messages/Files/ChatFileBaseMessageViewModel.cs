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
    readonly protected IFileTransferFeatureCollection FileTransferFeatureCollection;

    /// <summary>
    /// Буфер обмена
    /// </summary>
    readonly protected IClipboard? Clipboard;

    /// <summary>
    /// Scope событий для комнаты
    /// </summary>
    readonly protected IEventScope RoomScope;

    /// <summary>
    /// Локальный пользователь
    /// </summary>
    readonly protected ParticipantInfo LocalParticipant;

    /// <summary>
    /// Кешированный формат
    /// </summary>
    readonly protected string CachedFormat;

    /// <summary>
    /// Подкиска на прогресс скачивание
    /// </summary>
    protected IDisposable FileTransferProgressSubsciptionToken = null!;

    /// <summary>
    /// Сообщение файла
    /// </summary>
    public FileMetadataMessage FileMetadataMessage { get; init; }

    [ObservableProperty]
    public partial int DownloadProgress { get; set; }

    [ObservableProperty]
    public partial bool IsTransferring { get; set; }

    [ObservableProperty]
    public partial bool IsAssembling { get; set; }

    [ObservableProperty]
    public partial bool Downloaded { get; set; }

    [ObservableProperty]
    public partial bool CantDownload { get; set; }

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
        bool isAvailableForNetwork)
        : base(fileMetadataMessage,
            fileMetadataMessage,
            fileMetadataMessage,
            dialogService,
            fileMetadataMessage.Sender.Name,
            timePadding,
            localParticipant.Id == fileMetadataMessage.Sender.Id,
            isHostMessage,
            removeHeaders,
            isAvailableForNetwork)
    {
        FileTransferFeatureCollection = fileTransferFeatureCollection;
        LocalParticipant = localParticipant;
        Clipboard = clipboard;
        RoomScope = roomScope;
        FileMetadataMessage = fileMetadataMessage;

        CachedFormat = fileTransferFeatureCollection.FileHelperService
            .GetFileType(fileMetadataMessage.FileName);

        Downloaded = !string.IsNullOrEmpty(fileMetadataMessage.FilePath) || fileMetadataMessage.AsDownloaded;

        CantDownload = !isAvailableForNetwork && !Downloaded;

        IsAvailableForNetworkChanged += ()
            => CantDownload = !IsAvailableForNetwork && !Downloaded;

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
    /// Файл собирается
    /// </summary>
    protected virtual void OnTransferAssembling() { }

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
        Subscriptions.Add(roomScope.Subscribe<FileTransferStartedEvent>(OnFileTransferStarted));
        Subscriptions.Add(roomScope.Subscribe<FileTransferFailedEvent>(OnFileTransferFailed));
        Subscriptions.Add(roomScope.Subscribe<FileTransferAssemblingEvent>(OnFileTransferAssembling));
        Subscriptions.Add(roomScope.Subscribe<FileTransferCompletedEvent>(OnFileTransferCompleted));
    }

    private Task OnFileTransferStarted(FileTransferStartedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.FileMetadataId != FileMetadataMessage.Id)
        {
            return Task.CompletedTask;
        }

        IsTransferring = true;

        FileTransferProgressSubsciptionToken = RoomScope.Subscribe((FileTransferProgressEvent e, CancellationToken _) =>
        {
            if (eventMessage.FileMetadataId != FileMetadataMessage.Id)
            {
                return Task.CompletedTask;
            }

            var progress = 100 * e.BytesReceived / FileMetadataMessage.FileSize;
            DownloadProgress = (int)Math.Min(progress, 100);
            OnTransferProgressUpdated(
                FileTransferFeatureCollection.FileHelperService.FormatFileSize(e.BytesReceived),
                FileTransferFeatureCollection.FileHelperService.FormatFileSize(eventMessage.FileSize));

            return Task.CompletedTask;
        });

        UpdateDownloadState();
        OnTransferProgressUpdated("0",
            FileTransferFeatureCollection.FileHelperService.FormatFileSize(eventMessage.FileSize));
        return Task.CompletedTask;
    }

    private Task OnFileTransferFailed(FileTransferFailedEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.FileMetadataId != FileMetadataMessage.Id
            || eventMessage.SenderId != LocalParticipant.Id)
        {
            return Task.CompletedTask;
        }

        IsTransferring = false;
        IsAssembling = false;

        UpdateDownloadState();
        OnTransferFailed(eventMessage.ErrorMessage ?? string.Empty);

        FileTransferProgressSubsciptionToken.Dispose();
        return Task.CompletedTask;
    }

    private Task OnFileTransferAssembling(FileTransferAssemblingEvent eventMessage, CancellationToken cancellationToken)
    {
        if (eventMessage.FileMetadataId != FileMetadataMessage.Id)
        {
            return Task.CompletedTask;
        }

        DownloadProgress = 100;
        IsTransferring = false;
        IsAssembling = true;

        UpdateDownloadState();
        OnTransferAssembling();
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
        FileTransferProgressSubsciptionToken.Dispose();

        IsTransferring = false;
        IsAssembling = false;

        UpdateDownloadState();
        OnTransferCompleted();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Обновить состояния файла
    /// </summary>
    protected void UpdateDownloadState()
        => FileDownloadState = IsAssembling
            ? FileDownloadState.Assembling
            : IsTransferring
                ? FileDownloadState.IsDownloading
                : Downloaded
                    ? FileDownloadState.Downloaded
                    : FileDownloadState.NotDownloaded;

    [RelayCommand]
    private async Task CopyNameToClipboard()
    {
        if (Clipboard != null)
        {
            await Clipboard.SetTextAsync(FileMetadataMessage.FileName);
            InAppNotifier.Info("Скопировано в буфер обмена");
        }
    }

    [RelayCommand]
    private Task ShowInFolder()
    {
        if (!Path.Exists(FileMetadataMessage.FilePath))
        {
            InAppNotifier.Warning("Файл не нашёлся");
            Downloaded = false;
            CantDownload = !IsAvailableForNetwork && !Downloaded;
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
