using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.Infrastructure.Validators;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Windows;
using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна создания своей сессии
/// </summary>
public partial class CreateTunnelSessionViewModel : ModalViewModelBase
{
    private const long MaxThumbnailSizeBytes = 2 * 1024 * 1024; // 2 MB

    [ObservableProperty]
    [Display(Name = "Имя сессии")]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Придумайте имя для сессии")]
    [FileName]
    [NotEndsWith(".")]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    [Display(Name = "Описание сессии")]
    public partial string Description { get; set; } = "";

    [ObservableProperty]
    [Display(Name = "Версия сессии")]
    public partial string Version { get; set; } = "";

    /// <summary>
    /// Заставка сессии в виде байт (отправляется участникам)
    /// </summary>
    public byte[]? Thumbnail { get; set; }

    /// <summary>
    /// Превью заставки
    /// </summary>
    [ObservableProperty]
    [Display(Name = "Заставка сессии")]
    public partial Bitmap? ThumbnailPreview { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyDataErrorInfo]
    [Range(1, 256, ErrorMessage = "Максимум участников может быть от 1 до 256")]
    public partial int? MaxPlayers { get; set; } = 8;

    [IntValue]
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateCommand))]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Введите порт")]
    [Range(1024, ushort.MaxValue, ErrorMessage = "Порт должен быть от 1024 до 65535")]
    public partial int Port { get; set; } = 7777;

    /// <summary>
    /// Создание или редактирование комнаты
    /// </summary>
    [ObservableProperty]
    public partial bool IsNew { get; set; } = true;

    /// <summary>
    /// Название окна
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "MIN - Создание сессии";

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void Create()
    {
        Close(ButtonOptions.Ok);
    }

    /// <summary>
    /// Заполнить форму для редактирования существующей сессии
    /// </summary>
    public void InitializeForEdit(Session session)
    {
        IsNew = false;
        Title = "MIN - Редактирование сессии";

        Name = session.Name;
        Description = session.Description;
        Version = session.Version.ToString();
        Port = session.TunnelPort ?? Port;
        MaxPlayers = session.MaximumParticipants;

        var thumbnailPath = session.GetThumbnailPath();

        if (session.ThumbnailFileName != null && File.Exists(thumbnailPath))
        {
            var bytes = File.ReadAllBytes(thumbnailPath);
            Thumbnail = bytes;
            ThumbnailPreview = new Bitmap(new MemoryStream(bytes));
        }
        else
        {
            Thumbnail = null;
            ThumbnailPreview = null;
        }
    }

    [RelayCommand]
    private async Task PickThumbnail()
    {
        var window = MainWindowViewModel.GetWindow();

        if (window == null)
        {
            return;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Выберите заставку",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Изображения") { Patterns = ["*.png", "*.jpg", "*.jpeg"] },
            ]
        });

        if (files == null || files.Count == 0)
        {
            return;
        }

        var bytes = await File.ReadAllBytesAsync(files[0].Path.LocalPath);

        if (bytes.Length > MaxThumbnailSizeBytes)
        {
            InAppNotifier.Error("Заставка слишком большая (максимум 2 МБ)");
            return;
        }

        Thumbnail = bytes;
        ThumbnailPreview = new Bitmap(new MemoryStream(bytes));
    }

    [RelayCommand]
    private void ClearThumbnail()
    {
        Thumbnail = null;
        ThumbnailPreview = null;
    }

    private bool CanCreate() => !HasErrors;
}
