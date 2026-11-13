using System;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.Desktop.ViewModels.Cards;

/// <summary>
/// Модель потока демонстрации для встроенного просмотра и всплывающих окон.
/// Либо собственный поток (из трансмиттера), либо поток другого участника (из сервиса воспроизведения)
/// </summary>
public partial class ScreenDemoStreamViewModel : ViewModelBase, IDisposable
{
    private readonly IScreenDemoTransmitter? screenDemoTransmitter;
    private readonly IScreenDemoPlaybackService? screenDemoPlaybackService;

    private readonly Guid roomId;

    private WriteableBitmap? bitmap;

    /// <summary>
    /// Кадр перерисован — VM должен вызвать InvalidateVisual у Image
    /// </summary>
    public event Action? FrameRendered;

    /// <summary>
    /// Это собственный поток локального пользователя?
    /// </summary>
    public bool IsOwnStream { get; }

    /// <summary>
    /// Кто демонстрирует экран
    /// </summary>
    public Guid SharerId { get; }

    /// <summary>
    /// Идентификатор подкомнаты демонстрации
    /// </summary>
    public int SubRoomId { get; }

    /// <summary>
    /// Текущий кадр
    /// </summary>
    [ObservableProperty]
    public partial WriteableBitmap? FrameImage { get; set; }

    /// <summary>
    /// Получен ли уже первый кадр (для индикатора загрузки)
    /// </summary>
    public bool HasFrame => FrameImage != null;

    partial void OnFrameImageChanged(WriteableBitmap? value) => OnPropertyChanged(nameof(HasFrame));

    /// <summary>
    /// Громкость звука демонстрации (0-100). Только для чужих потоков
    /// </summary>
    [ObservableProperty]
    public partial int SoundVolume { get; set; } = 100;

    partial void OnSoundVolumeChanged(int value)
        => screenDemoPlaybackService?.ChangeStreamVolume(roomId, SharerId, value);

    /// <summary>
    /// Поток собственного экрана
    /// </summary>
    public ScreenDemoStreamViewModel(Guid roomId, int subRoomId, Guid sharerId, IScreenDemoTransmitter screenDemoTransmitter)
    {
        this.roomId = roomId;
        SubRoomId = subRoomId;
        SharerId = sharerId;
        this.screenDemoTransmitter = screenDemoTransmitter;
        IsOwnStream = true;

        screenDemoTransmitter.FramePreview += OnFramePreview;
    }

    /// <summary>
    /// Поток другого участника
    /// </summary>
    public ScreenDemoStreamViewModel(Guid roomId, int subRoomId, Guid sharerId, IScreenDemoPlaybackService screenDemoPlaybackService)
    {
        this.roomId = roomId;
        SubRoomId = subRoomId;
        SharerId = sharerId;
        this.screenDemoPlaybackService = screenDemoPlaybackService;

        screenDemoPlaybackService.FrameDecoded += OnFrameDecoded;
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

    private void OnFrameDecoded(object? sender, ScreenDemoFrameDecodedEventArgs e)
    {
        if (e.RoomId != roomId || e.SharerId != SharerId)
        {
            return;
        }

        if (e.Frame == null || !ScreenFrameBlitter.IsRenderable(e.Frame))
        {
            return;
        }

        // Буферы воспроизведения переиспользуются (двойная буферизация) —
        // у отрисовки есть ~2 интервала кадра
        Dispatcher.UIThread.Post(() => RenderFrame(e.Frame));
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

    /// <inheritdoc cref="IDisposable.Dispose"/>
    public override void Dispose()
    {
        if (screenDemoTransmitter != null)
        {
            screenDemoTransmitter.FramePreview -= OnFramePreview;
        }

        if (screenDemoPlaybackService != null)
        {
            screenDemoPlaybackService.FrameDecoded -= OnFrameDecoded;
        }

        base.Dispose();
    }
}
