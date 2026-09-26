using System.IO;
using Avalonia;
using Avalonia.Input.Platform;
using Avalonia.Labs.Gif;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Core.Entities.Contracts.Models;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Infrastructure.Services;
using MIN.FileTransfer.DI.FeatureCollection;
using MIN.FileTransfer.Messaging;

namespace MIN.Desktop.ViewModels.Cards.Messages.Files;

public partial class ChatFileImagePreviewMessageViewModel : ChatFileBaseMessageViewModel
{
    // GIF
    private FileStream? gifStream;
    private GifStreamSource gifPreviewImage = null!;

    [ObservableProperty]
    public partial string FileNameAndSize { get; set; } = string.Empty;

    /// <summary>
    /// Превью изображения
    /// </summary>
    [ObservableProperty]
    public partial Bitmap? PreviewImage { get; set; }

    /// <summary>
    /// Превью GIF изображения
    /// </summary>
    public GifStreamSource GifPreviewImage
    {
        get => gifPreviewImage;
        private set
        {
            gifPreviewImage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatFileImagePreviewMessageViewModel"/>
    /// </summary>
    public ChatFileImagePreviewMessageViewModel(IFileTransferFeatureCollection fileTransferFeatureCollection,
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
        FileNameAndSize = Downloaded
            ? string.Empty
            : $"{FileMetadataMessage.FileName} {fileTransferFeatureCollection.FileHelperService
            .FormatFileSize(FileMetadataMessage.FileSize)}";

        LoadImage(FileMetadataMessage.FilePath ?? string.Empty);
        UpdateDownloadState();
    }

    /// <inheritdoc />
    protected override void OnTransferProgressUpdated(string formattedReceived, string formattedTotal)
    {
        FileNameAndSize = $"{FileMetadataMessage.FileName} {formattedReceived} / {formattedTotal}";
    }

    /// <inheritdoc />
    protected override void OnTransferFailed(string errorMessage)
    {
        FileNameAndSize = $"Ошибка: {errorMessage}";
    }

    /// <inheritdoc />
    protected override void OnTransferCompleted()
    {
        FileNameAndSize = string.Empty;
        LoadImage(FileMetadataMessage.FilePath!);
    }

    private void LoadImage(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        gifStream?.Dispose();
        gifStream = null;

        if (cachedFormat == "gif")
        {
            gifStream = File.OpenRead(filePath);
            GifPreviewImage = GifStreamSource.FromStream(gifStream);
        }
        else if (cachedFormat == "svg")
        {
            PreviewImage = ImageHelper.SvgToBitmap(filePath);
        }
        else
        {
            PreviewImage = new Bitmap(filePath);
        }
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

        if (!Downloaded)
        {
            InvokeDownloadRequested();
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        gifStream?.Dispose();
        gifStream = null;

        base.Dispose();
    }
}
