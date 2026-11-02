using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using MIN.Chat.Services.Contracts.Interfaces;
using MIN.Common.Core.Contracts.Interfaces;
using MIN.Core.Events.Contracts.Interfaces;
using MIN.Core.Events.Events;
using MIN.Core.Identity.Contracts.Interfaces;
using MIN.Core.Stores.Contracts.Models.SubRooms;
using MIN.Desktop.ViewModels.Windows;
using MIN.Desktop.Views.Windows;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;
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
    private readonly IScreenDemoTransmitter screenDemoTransmitter;
    private readonly IScreenDemoPlaybackService screenDemoPlaybackService;
    private readonly IScreenDemoStateService screenDemoStateService;
    private readonly IChatScreenDemoService chatScreenDemoService;
    private readonly IIdentityService identityService;
    private readonly ILoggerProvider logger;

    private readonly Dictionary<(Guid RoomId, Guid SharerId), ScreenDemoViewerWindow> viewerWindows = [];
    private readonly Dictionary<(Guid RoomId, Guid SharerId), int> viewerSubRooms = [];

    private ScreenDemoPreviewWindow? previewWindow;
    private Guid? previewRoomId;

    int IHostedService.Priority => 50;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoWindowsService"/>
    /// </summary>
    public ScreenDemoWindowsService(IEventBus eventBus,
        IScreenDemoTransmitter screenDemoTransmitter,
        IScreenDemoPlaybackService screenDemoPlaybackService,
        IScreenDemoStateService screenDemoStateService,
        IChatScreenDemoService chatScreenDemoService,
        IIdentityService identityService,
        ILoggerProvider logger)
    {
        this.eventBus = eventBus;
        this.screenDemoTransmitter = screenDemoTransmitter;
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
        eventBus.Subscribe<ScreenDemoViewerLeftEvent>(OnViewerLeft);
        eventBus.Subscribe<RoomWentOfflineEvent>(OnRoomWentOffline);
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

    private Task OnViewerLeft(ScreenDemoViewerLeftEvent e, CancellationToken cancellationToken)
    {
        if (e.ViewerId != identityService.SelfParticipant.Id)
        {
            return Task.CompletedTask;
        }

        Dispatcher.UIThread.Post(() =>
        {
            var entry = viewerSubRooms.FirstOrDefault(x => x.Key.RoomId == e.RoomId && x.Value == e.SubRoomId);

            if (entry.Key != default)
            {
                CloseViewerInternal(e.RoomId, entry.Key.SharerId);
            }
        });

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

    private Task OnRoomWentOffline(RoomWentOfflineEvent e, CancellationToken cancellationToken)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (previewRoomId == e.RoomId)
            {
                ClosePreviewInternal();
            }

            foreach (var key in viewerWindows.Keys.Where(k => k.RoomId == e.RoomId).ToList())
            {
                CloseViewerInternal(key.RoomId, key.SharerId);
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
            DataContext = new ScreenDemoPreviewViewModel(screenDemoTransmitter, chatScreenDemoService, new SubRoomContext(roomId, subRoomId), sourceTitle),
        };

        previewRoomId = roomId;

        previewWindow.Closed += async (_, _) =>
        {
            if (previewWindow?.DataContext is ScreenDemoPreviewViewModel viewModel)
            {
                viewModel.Dispose();

                // Крест на предпросмотре = завершить демонстрацию
                var demo = screenDemoStateService.GetDemo(viewModel.Context.RoomId, viewModel.Context.SubRoomId);

                if (!viewModel.IsStopping && demo != null && demo.Sharer.Id == identityService.SelfParticipant.Id)
                {
                    await chatScreenDemoService.EndDemoAsync(viewModel.Context.RoomId, viewModel.Context.SubRoomId);
                }
            }

            previewWindow = null;
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
        viewerSubRooms[key] = subRoomId;

        window.Closed += async (_, _) =>
        {
            try
            {
                viewModel.Dispose();
                viewerWindows.Remove(key);
                viewerSubRooms.Remove(key);

                var demo = screenDemoStateService.GetDemo(roomId, subRoomId);

                if (!viewModel.IsLeaving && demo != null && demo.ViewerIds.Contains(identityService.SelfParticipant.Id))
                {
                    await chatScreenDemoService.LeaveDemoAsync(roomId, subRoomId);
                }
            }
            catch (Exception ex)
            {
                logger.Log($"Ошибка при закрытии окна просмотра: {ex}", LogLevel.Error);
            }
        };

        window.Show();
        logger.Log($"Открыто окно просмотра демонстрации от {sharerName}");
    }

    private void ClosePreviewInternal()
    {
        previewWindow?.Close();
        previewWindow = null;
        previewRoomId = null;
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
