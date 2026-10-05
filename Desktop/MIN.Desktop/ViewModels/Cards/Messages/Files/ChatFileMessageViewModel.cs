using System;
using System.Diagnostics;
using System.IO;
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
using MIN.FileTransfer.Messaging;

namespace MIN.Desktop.ViewModels.Cards.Messages.Files;

public partial class ChatFileMessageViewModel : ChatFileBaseMessageViewModel
{
    [ObservableProperty]
    public partial string InteractionFileStatus { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UniversalFileStatus { get; set; } = string.Empty;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatFileMessageViewModel"/>
    /// </summary>
    public ChatFileMessageViewModel(IFileTransferFeatureCollection fileTransferFeatureCollection,
        IDialogService dialogService,
        IEventScope roomScope,
        FileMetadataMessage fileMetadataMessage,
        Thickness timePadding,
        ParticipantInfo localParticipant,
        bool isHostMessage,
        bool removeHeaders,
        IClipboard? clipboard,
        bool isAvaibleForNetwork)
        : base(fileTransferFeatureCollection, dialogService, roomScope, fileMetadataMessage,
            timePadding, localParticipant, isHostMessage, removeHeaders, clipboard, isAvaibleForNetwork)
    { }

    /// <inheritdoc />
    protected override void FillLabels()
    {
        UniversalFileStatus = fileTransferFeatureCollection.FileHelperService
            .FormatFileSize(FileMetadataMessage.FileSize);
        InteractionFileStatus = cachedFormat;
    }
    /// <inheritdoc />
    protected override void OnTransferProgressUpdated(string formattedReceived, string formattedTotal)
    {
        UniversalFileStatus = $"{formattedReceived} / {formattedTotal}";
    }

    /// <inheritdoc />
    protected override void OnTransferFailed(string errorMessage)
    {
        UniversalFileStatus = $"Ошибка: {errorMessage}";
    }

    /// <inheritdoc />
    protected override void OnTransferCompleted()
    {
        UniversalFileStatus = fileTransferFeatureCollection.FileHelperService
            .FormatFileSize(FileMetadataMessage.FileSize);
    }

    [RelayCommand]
    private void InteractionMouseEnter()
    {
        InteractionFileStatus = string.Empty;
        UpdateDownloadState();
    }

    [RelayCommand]
    private void InteractionMouseLeave()
    {
        if (FileDownloadState != FileDownloadState.IsDownloading)
        {
            FileDownloadState = FileDownloadState.None;
        }
        InteractionFileStatus = cachedFormat;
    }

    [RelayCommand]
    private void InteractionClick()
    {
        if (IsTransfering)
        {
            InvokeCancelRequested();
            IsTransfering = false;
            return;
        }

        if (Downloaded)
        {
            var path = FileMetadataMessage.FilePath;

            if (!Path.Exists(path))
            {
                InAppNotifier.Warning("Файл не нашёлся");
                Downloaded = false;
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                InAppNotifier.Error($"Не удалось открыть файл: {ex.Message}");
            }
        }
        else
        {
            InvokeDownloadRequested();
        }
    }
}
