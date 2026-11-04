using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.Desktop.ViewModels.Windows;

/// <summary>
/// Модель окна просмотра чужой демонстрации экрана
/// </summary>
public partial class ScreenDemoViewerViewModel : ViewModelBase, IDisposable
{
    private readonly IScreenDemoPlaybackService screenDemoPlaybackService;
    private readonly IChatScreenDemoService chatScreenDemoService;

    private readonly Guid roomId;
    private readonly Guid sharerId;

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
    /// Зритель уже покидает демонстрацию (чтобы не отправить Leave дважды)
    /// </summary>
    public bool IsLeaving { get; private set; }

    /// <summary>
    /// Получен ли уже первый кадр (для индикатора загрузки)
    /// </summary>
    public bool HasFrame => FrameImage != null;

    partial void OnFrameImageChanged(WriteableBitmap? value) => OnPropertyChanged(nameof(HasFrame));

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoViewerViewModel"/>
    /// </summary>
    public ScreenDemoViewerViewModel(IScreenDemoPlaybackService screenDemoPlaybackService,
        IChatScreenDemoService chatScreenDemoService,
        Guid roomId,
        Guid sharerId,
        string sharerName)
    {
        this.screenDemoPlaybackService = screenDemoPlaybackService;
        this.chatScreenDemoService = chatScreenDemoService;
        this.roomId = roomId;
        this.sharerId = sharerId;

        Title = $"Демонстрация: {sharerName}";

        screenDemoPlaybackService.FrameDecoded += OnFrameDecoded;
    }

    private void OnFrameDecoded(object? sender, ScreenDemoFrameDecodedEventArgs e)
    {
        if (e.RoomId != roomId || e.SharerId != sharerId)
        {
            return;
        }

        if (Interlocked.Exchange(ref isFrameQueued, 1) != 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            Volatile.Write(ref isFrameQueued, 0);
            if (e.Frame != null)
            {
                RenderFrame(e.Frame);
            }
        });
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
    private async Task LeaveDemo()
    {
        if (IsLeaving)
        {
            return;
        }

        IsLeaving = true;

        await chatScreenDemoService.LeaveDemoAsync(roomId, SubRoomId);
    }

    /// <summary>
    /// Идентификатор подкомнаты демонстрации (заполняется менеджером окон)
    /// </summary>
    public int SubRoomId { get; init; }

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public override void Dispose()
    {
        screenDemoPlaybackService.FrameDecoded -= OnFrameDecoded;
        base.Dispose();
    }
}
