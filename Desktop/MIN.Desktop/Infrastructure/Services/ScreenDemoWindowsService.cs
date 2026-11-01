using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models.SubRooms;
using MIN.Desktop.ViewModels.Windows;
using MIN.Desktop.Views.Windows;
using MIN.Helpers.Contracts.Interfaces;
using MIN.ScreenDemo.Events;
using MIN.ScreenDemo.Services.Contracts.Interfaces;

namespace MIN.Desktop.Infrastructure.Services;

/// <summary>
/// Менеджер окон демонстрации экрана: открывает предпросмотр у демонстрирующего
/// и окна просмотра у зрителей, закрывает их при завершении демонстрации
/// </summary>
public class ScreenDemoWindowsService : IHostedService
{
    private readonly IEventBus eventBus;
    private readonly IScreenCaptureService screenCaptureService;
    private readonly IScreenDemoPlaybackService screenDemoPlaybackService;
    private readonly IScreenDemoStateService screenDemoStateService;
    private readonly IChatScreenDemoService chatScreenDemoService;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    private readonly Dictionary<(Guid RoomId, Guid SharerId), ScreenDemoViewerWindow> viewerWindows = [];

    private ScreenDemoPreviewWindow? previewWindow;

    int IHostedService.Priority => 50;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoWindowsService"/>
    /// </summary>
    public ScreenDemoWindowsService(IEventBus eventBus,
        IScreenCaptureService screenCaptureService,
        IScreenDemoPlaybackService screenDemoPlaybackService,
        IScreenDemoStateService screenDemoStateService,
        IChatScreenDemoService chatScreenDemoService,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.eventBus = eventBus;
        this.screenCaptureService = screenCaptureService;
        this.screenDemoPlaybackService = screenDemoPlaybackService;
        this.screenDemoStateService = screenDemoStateService;
        this.chatScreenDemoService = chatScreenDemoService;
        this.identityService = identityService;
        this.logger = logger;
    }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        eventBus.Subscribe<ScreenDemoEstablishedEvent>(OnDemoEstablished);
        eventBus.Subscribe<ScreenDemoEndedEvent>(OnDemoEnded);
        return Task.CompletedTask;
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ClosePreviewInternal();
            CloseAllViewersInternal();
        });
        return Task.CompletedTask;
    }

    private Task OnDemoEstablished(ScreenDemoEstablishedEvent e, CancellationToken cancellationToken)
    {
        var selfId = identityService.SelfParticipant.Id;

        if (e.SharerId == selfId)
        {
            Dispatcher.UIThread.Post(() => OpenPreview(e.RoomId, e.SubRoomId));
        }
        else
        {
            Dispatcher.UIThread.Post(() => OpenViewer(e.RoomId, e.SubRoomId, e.SharerId));
        }

        return Task.CompletedTask;
    }

    private Task OnDemoEnded(ScreenDemoEndedEvent e, CancellationToken cancellationToken)
    {
        var selfId = identityService.SelfParticipant.Id;

        Dispatcher.UIThread.Post(() =>
        {
            if (e.SharerId == selfId)
            {
                ClosePreviewInternal();
            }
            else
            {
                CloseViewerInternal(e.RoomId, e.SharerId);
            }
        });

        return Task.CompletedTask;
    }

    private void OpenPreview(Guid roomId, int subRoomId)
    {
        if (previewWindow != null)
        {
            return;
        }

        var demo = screenDemoStateService.GetDemo(roomId, subRoomId);
        var sourceTitle = demo?.Sharer.Name ?? "экрана";

        previewWindow = new ScreenDemoPreviewWindow
        {
            DataContext = new ScreenDemoPreviewViewModel(screenCaptureService, chatScreenDemoService, new SubRoomContext(roomId, subRoomId), sourceTitle),
        };

        previewWindow.Closed += (_, _) =>
        {
            if (previewWindow?.DataContext is ScreenDemoPreviewViewModel viewModel)
            {
                viewModel.Dispose();
            }
        };

        previewWindow.Show();
        logger.Log("Открыто окно предпросмотра демонстрации");
    }

    private void OpenViewer(Guid roomId, int subRoomId, Guid sharerId)
    {
        var key = (roomId, sharerId);

        if (viewerWindows.ContainsKey(key))
        {
            return;
        }

        var demo = screenDemoStateService.GetDemo(roomId, subRoomId);
        var sharerName = demo?.Sharer.Name ?? "неизвестный участник";

        var viewModel = new ScreenDemoViewerViewModel(screenDemoPlaybackService, chatScreenDemoService, roomId, sharerId, sharerName)
        {
            SubRoomId = subRoomId,
        };

        var window = new ScreenDemoViewerWindow
        {
            DataContext = viewModel,
        };

        viewerWindows[key] = window;

        window.Closed += async (_, _) =>
        {
            viewModel.Dispose();

            // Пользователь закрыл окно крестом — уходим из демонстрации.
            // При остановке демонстрации окно закрывает сервис: IsLeaving не установлен,
            // но демонстрация уже завершена — Leave вернёт ошибку, безопасно
            if (!viewModel.IsLeaving)
            {
                await chatScreenDemoService.LeaveDemoAsync(roomId, subRoomId);
            }

            viewerWindows.Remove(key);
        };

        window.Show();
        logger.Log($"Открыто окно просмотра демонстрации от {sharerName}");
    }

    private void ClosePreviewInternal()
    {
        previewWindow?.Close();
        previewWindow = null;
    }

    private void CloseViewerInternal(Guid roomId, Guid sharerId)
    {
        if (viewerWindows.Remove((roomId, sharerId), out var window))
        {
            window.Close();
        }
    }

    private void CloseAllViewersInternal()
    {
        foreach (var window in viewerWindows.Values.ToList())
        {
            window.Close();
        }

        viewerWindows.Clear();
    }
}
