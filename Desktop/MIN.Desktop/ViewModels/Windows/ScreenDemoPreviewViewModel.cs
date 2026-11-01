using System;
using System.Threading;
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
    private readonly IScreenCaptureService screenCaptureService;
    private readonly IChatScreenDemoService chatScreenDemoService;
    private readonly SubRoomContext context;

    private WriteableBitmap? bitmap;
    private int isFrameQueued;

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
    /// Инициализирует новый экземпляр <see cref="ScreenDemoPreviewViewModel"/>
    /// </summary>
    public ScreenDemoPreviewViewModel(IScreenCaptureService screenCaptureService,
        IChatScreenDemoService chatScreenDemoService,
        SubRoomContext context,
        string sourceTitle)
    {
        this.screenCaptureService = screenCaptureService;
        this.chatScreenDemoService = chatScreenDemoService;
        this.context = context;

        Title = $"Демонстрация экрана: {sourceTitle}";

        screenCaptureService.FrameCaptured += OnFrameCaptured;
    }

    private void OnFrameCaptured(object? sender, ScreenFrame e)
    {
        // Коалесинг кадров: если отрисовка не успевает, рендерим только последний кадр
        if (Interlocked.Exchange(ref isFrameQueued, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Volatile.Write(ref isFrameQueued, 0);
            RenderFrame(e);
        });
    }

    private void RenderFrame(ScreenFrame frame)
    {
        if (!ScreenFrameBlitter.IsSameSize(bitmap, frame))
        {
            bitmap?.Dispose();
            bitmap = ScreenFrameBlitter.Create(frame);
            FrameImage = bitmap;
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

        await chatScreenDemoService.EndDemoAsync(context.RoomId, context.SubRoomId);
        // Окно закроется в ScreenDemoWindowsService по ScreenDemoEndedEvent
    }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public override void Dispose()
    {
        screenCaptureService.FrameCaptured -= OnFrameCaptured;
        base.Dispose();
    }
}
