using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Labs.Gif;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.Sessions.Core.Services.Contracts.Enums;
using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Desktop.ViewModels.Cards;

/// <summary>
/// View модель сессии
/// </summary>
public partial class DownloadedSessionViewModel : CardViewModelBase, IDisposable
{
    private readonly IClipboard? clipboard;

    private FileStream? gifStream;
    private GifStreamSource gifSessionImage = null!;

    /// <summary>
    /// Сессия
    /// </summary>
    public Session? Session { get; set; }

    /// <summary>
    /// Имя сессии
    /// </summary>
    [ObservableProperty]
    public partial string SessionName { get; set; } = string.Empty;

    /// <summary>
    /// Описание сессии
    /// </summary>
    [ObservableProperty]
    public partial string SessionDescription { get; set; } = string.Empty;

    /// <summary>
    /// Версия сессии
    /// </summary>
    [ObservableProperty]
    public partial string Version { get; set; } = string.Empty;

    /// <summary>
    /// Инфа о максимальном кол-ве участников
    /// </summary>
    [ObservableProperty]
    public partial string MaximumParticipantInfo { get; set; } = string.Empty;

    /// <summary>
    /// Ссылка на установку сессии
    /// </summary>
    [ObservableProperty]
    public partial string DownloadLink { get; set; } = string.Empty;

    /// <summary>
    /// Выбрана ли карточка
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>
    /// Является ли карточка - созданием туннеля
    /// </summary>
    [ObservableProperty]
    public partial bool IsCreateTunnelCard { get; set; }

    /// <summary>
    /// Является ли сессия туннельной
    /// </summary>
    public bool IsTunnelSession
        => Session?.Kind == SessionKind.PortTunnel;

    /// <summary>
    /// Событие запроса удаления сессии
    /// </summary>
    public event Func<Task>? OnDeleteRequested;

    /// <summary>
    /// Событие запроса редактирования сессии
    /// </summary>
    public event Func<Task>? OnEditRequested;

    /// <summary>
    /// Изображение сессии
    /// </summary>
    public Bitmap? SessionImage { get; set; }

    /// <summary>
    /// .gif Изображение сессии
    /// </summary>
    public GifStreamSource GifSessionImage
    {
        get => gifSessionImage;
        private set
        {
            gifSessionImage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Событие по нажатию на карточку
    /// </summary>
    public Action<bool>? OnClicked { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="DownloadedSessionViewModel"/>
    /// </summary>
    public DownloadedSessionViewModel(Session? session, IClipboard? clipboard, bool isCreateTunnelCard)
    {
        Session = session;
        this.clipboard = clipboard;
        IsCreateTunnelCard = isCreateTunnelCard;

        FillLabels();
    }

    private void FillLabels()
    {
        SessionName = IsCreateTunnelCard
            ? "Cвоя сессия"
            : Session!.Name;

        SessionDescription = IsCreateTunnelCard
            ? "Укажите порт к вашей программе/игре, и участники получат к ней доступ в виде сессии!"
            : Session!.Description;

        if (IsCreateTunnelCard)
        {
            return;
        }

        DownloadLink = Session!.DownloadLink;
        Version = $"Версия: {Session.Version}";
        MaximumParticipantInfo = "Максимум участников: " + (Session.MaximumParticipants.HasValue
            ? Session.MaximumParticipants.Value
            : "Неограничено");

        if (Session.ThumbnailFileName != null)
        {
            var bytes = File.ReadAllBytes(Session.GetThumbnailPath());
            using var ms = new MemoryStream(bytes);

            var format = Path.GetExtension(Session.ThumbnailFileName);

            gifStream?.Dispose();
            gifStream = null;

            if (format == "gif")
            {
                GifSessionImage = GifStreamSource.FromStream(ms);
            }
            else if (format == "svg")
            {
                SessionImage = ImageHelper.SvgToBitmap(ms);
            }
            else
            {
                SessionImage = new Bitmap(ms);
            }
        }
    }

    /// <summary>
    /// Отменить выбор карточки
    /// </summary>
    public void Unselect()
    {
        IsSelected = false;
    }

    /// <summary>
    /// Выбрать карточку
    /// </summary>
    [RelayCommand]
    public void Select()
    {
        IsSelected = !IsSelected;
        OnClicked?.Invoke(IsSelected);
    }

    [RelayCommand]
    private async Task CopyLink()
    {
        if (clipboard == null || IsCreateTunnelCard)
        {
            return;
        }

        try
        {
            await clipboard.SetTextAsync(Session!.DownloadLink);
            DownloadLink = "Скопировано!";
        }
        catch { }
    }


    /// <summary>
    /// Редактировать сессию
    /// </summary>
    [RelayCommand]
    private Task Edit() => OnEditRequested?.Invoke() ?? Task.CompletedTask;

    /// <summary>
    /// Удалить сессию
    /// </summary>
    [RelayCommand]
    private Task Delete() => OnDeleteRequested?.Invoke() ?? Task.CompletedTask;

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public override void Dispose()
    {
        gifStream?.Dispose();
        gifStream = null;

        base.Dispose();
    }
}
