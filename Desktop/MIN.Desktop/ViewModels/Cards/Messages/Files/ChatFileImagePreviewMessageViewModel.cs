using System.IO;
using Avalonia;
using Avalonia.Input.Platform;
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
    /// <summary>
    /// Порог размера файла (2 МБ), до которого изображения скачиваются автоматически
    /// </summary>
    private const long AutoDownloadSizeThreshold = 2 * 1024 * 1024;

    // GIF
    private FileStream? gifStream;
    private GifHeaderSource gifPreviewImage = null!;

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
    public GifHeaderSource GifPreviewImage
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
        bool isAvailableForNetwork)
        : base(fileTransferFeatureCollection, dialogService, roomScope, fileMetadataMessage,
            timePadding, localParticipant, isHostMessage, removeHeaders, clipboard, isAvailableForNetwork)
    { }

    /// <inheritdoc />
    protected override void FillLabels()
    {
        FileNameAndSize = Downloaded
            ? string.Empty
            : $"{FileMetadataMessage.FileName} {FileTransferFeatureCollection.FileHelperService
            .FormatFileSize(FileMetadataMessage.FileSize)}";

        LoadImage(FileMetadataMessage.FilePath ?? string.Empty);
        UpdateDownloadState();

        IsAvailableForNetworkChanged += OnNetworkAvailabilityChangedForAutoDownload;
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

        switch (CachedFormat)
        {
            case "gif":
                gifStream = File.OpenRead(filePath);
                GifPreviewImage = GifHeaderSource.FromStream(gifStream);
                break;
            case "svg":
                PreviewImage = ImageHelper.SvgToBitmap(filePath);
                break;
            default:
                PreviewImage = new Bitmap(filePath);
                break;
        }
    }

    [RelayCommand]
    private void InteractionClick()
    {
        if (IsTransferring)
        {
            InvokeCancelRequested();
            IsTransferring = false;
            return;
        }

        if (!Downloaded)
        {
            InvokeDownloadRequested();
        }
    }

    /// <summary>
    /// Автоматически запрашивает скачивание изображений, не превышающих порог размера
    /// </summary>
    public void TryAutoDownload()
    {
        if (Downloaded || IsTransferring || !IsAvailableForNetwork
            || FileMetadataMessage.FileSize > AutoDownloadSizeThreshold)
        {
            return;
        }

        InvokeDownloadRequested();
    }

    /// <summary>
    /// Доскачивает небольшие изображения при восстановлении сети
    /// </summary>
    private void OnNetworkAvailabilityChangedForAutoDownload()
    {
        if (IsAvailableForNetwork)
        {
            TryAutoDownload();
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        PreviewImage?.Dispose();
        gifStream?.Dispose();
        gifStream = null;

        base.Dispose();
    }
}
