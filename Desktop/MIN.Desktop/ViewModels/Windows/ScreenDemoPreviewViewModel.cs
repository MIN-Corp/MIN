using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models.SubRooms;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.Desktop.ViewModels.Windows;

/// <summary>
/// Модель окна предпросмотра собственной демонстрации экрана
/// </summary>
public partial class ScreenDemoPreviewViewModel : ViewModelBase, IDisposable
{
    private readonly IScreenDemoTransmitter screenDemoTransmitter;
    private readonly IChatScreenDemoService chatScreenDemoService;

    private WriteableBitmap? bitmap;

    /// <summary>
    /// Кадр перерисован — окно должно вызвать InvalidateVisual у Image
    /// </summary>
    public event Action? FrameRendered;

    /// <summary>
    /// Текущий кадр
    /// </summary>
    [ObservableProperty]
    public partial WriteableBitmap? FrameImage { get; set; }

    /// <summary>
    /// Заголовок окна
    /// </summary>
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    /// <summary>
    /// Идёт ли остановка демонстрации (чтобы окно не пыталось остановить повторно)
    /// </summary>
    public bool IsStopping { get; private set; }

    /// <summary>
    /// Контекст подкомнаты демонстрации
    /// </summary>
    public SubRoomContext Context { get; }

    /// <summary>
    /// Получен ли уже первый кадр (для индикатора загрузки)
    /// </summary>
    public bool HasFrame => FrameImage != null;

    partial void OnFrameImageChanged(WriteableBitmap? value) => OnPropertyChanged(nameof(HasFrame));

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoPreviewViewModel"/>
    /// </summary>
    public ScreenDemoPreviewViewModel(IScreenDemoTransmitter screenDemoTransmitter,
        IChatScreenDemoService chatScreenDemoService,
        SubRoomContext context,
        string sourceTitle)
    {
        this.screenDemoTransmitter = screenDemoTransmitter;
        this.chatScreenDemoService = chatScreenDemoService;
        Context = context;

        Title = $"Демонстрация экрана: {sourceTitle}";

        screenDemoTransmitter.FramePreview += OnFramePreview;
    }

    private void OnFramePreview(object? sender, ScreenFrame e)
    {
        if (!ScreenFrameBlitter.IsRenderable(e))
        {
            return;
        }

        // Синхронно: буфер кадра переиспользуется трансмиттером сразу после события
        Dispatcher.UIThread.Invoke(() => RenderFrame(e));
    }

    private void RenderFrame(ScreenFrame frame)
    {
        if (!ScreenFrameBlitter.IsRenderable(frame))
        {
            return;
        }

        if (!ScreenFrameBlitter.IsSameSize(bitmap, frame))
        {
            var previous = bitmap;
            bitmap = ScreenFrameBlitter.Create(frame);
            FrameImage = bitmap;

            // Утилизируем отложено: композитор мог ещё читать старый битмап
            if (previous != null)
            {
                Dispatcher.UIThread.Post(previous.Dispose, DispatcherPriority.Background);
            }
        }
        ScreenFrameBlitter.Blit(bitmap!, frame);
        FrameRendered?.Invoke();
    }

    [RelayCommand]
    private async Task StopDemo()
    {
        if (IsStopping)
        {
            return;
        }

        IsStopping = true;

        await chatScreenDemoService.EndDemoAsync(Context.RoomId, Context.SubRoomId);
        // Окно закроется в ScreenDemoWindowsService по ScreenDemoEndedEvent
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public override void Dispose()
    {
        screenDemoTransmitter.FramePreview -= OnFramePreview;
        base.Dispose();
    }
}
