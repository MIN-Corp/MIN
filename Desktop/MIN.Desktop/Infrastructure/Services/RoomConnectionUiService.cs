using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using MIN.Core.Entities;
using MIN.Core.Events.Events;
using MIN.Core.Handlers.Contracts.Constants;
using MIN.Core.Services.Contracts.Exceptions;
using MIN.Core.Services.Contracts.Models;
using MIN.Desktop.Contracts.Constants;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Contracts.Models;
using MIN.Desktop.Contracts.Models.Enums;
using MIN.Desktop.ViewModels.Modals;
using MIN.DI.FeatureCollection;

namespace MIN.Desktop.Infrastructure.Services;

internal class RoomConnectionUiService : IRoomConnectionUiService
{
    private readonly IMinFeatureCollection featureCollection;
    private readonly IChatViewsRegistry chatViewsRegistry;
    private readonly IDialogService dialogService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomConnectionUiService"/>
    /// </summary>
    public RoomConnectionUiService(IMinFeatureCollection featureCollection,
        IChatViewsRegistry chatViewsRegistry,
        IDialogService dialogService)
    {
        this.featureCollection = featureCollection;
        this.chatViewsRegistry = chatViewsRegistry;
        this.dialogService = dialogService;
    }

    async Task<RoomHostResult> IRoomConnectionUiService.HostAsync(RoomHostArgs args, CancellationToken cancellationToken)
    {
        try
        {
            var room = await featureCollection.Core.Lifecycle.StartHostingAsync(args.RoomInfo, args.NetworkOptions, password: args.Password, cancellationToken);
            args.RoomInfo.HasPassword = room.HasPassword;
            await featureCollection.Chat.ChatRoomService.ManageDiscoveryOutOfSettings(args.RoomInfo,
                room.ConnectionAddresses, args.NetworkOptions, cancellationToken: cancellationToken);

            await args.OnRoomReady(room);

            return new RoomHostResult
            {
                Room = room,
            };
        }
        catch (OperationCanceledException)
        {
            return new RoomHostResult
            {
                Failure = HostFailure.Cancelled,
                ErrorMessage = "Создание комнаты было отменено"
            };
        }
        catch (Exception ex)
        {
            return new RoomHostResult
            {
                Failure = HostFailure.Error,
                ErrorMessage = $"Не удалось создать комнату: {ex.Message}"
            };
        }
    }

    async Task<RoomJoinResult> IRoomConnectionUiService.JoinAsync(RoomJoinArgs args, CancellationToken cancellationToken)
    {
        ConnectionResult connectionResult = new();
        var joinSettledTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IDisposable? errorToken = null;
        IDisposable? roomStateToken = null;
        LoadingViewModel? loadingVm = null;
        var passwordRejected = false;

        try
        {
            async void OnRoomReady(Room? room)
            {
                errorToken?.Dispose();
                roomStateToken?.Dispose();

                if (room == null)
                {
                    joinSettledTcs.TrySetResult(false);
                    return;
                }

                joinSettledTcs.TrySetResult(true);
                await args.OnRoomReady(room, connectionResult.ConnectionId);
            }

            Task OnErrorOccured(ErrorOccurredEvent eventMessage, CancellationToken cancellationToken)
            {
                if (eventMessage.RoomId != connectionResult.RoomId)
                {
                    return Task.CompletedTask;
                }

                if (eventMessage.NeedToDisconnect
                    && eventMessage.ErrorMessage?.Contains(RoomPasswordConstants.WrongPasswordMessage) == true)
                {
                    passwordRejected = true;
                }

                if (eventMessage.NeedToDestroy)
                {
                    featureCollection.Core.Lifecycle.MarkRoomForDeletion(connectionResult.RoomId);
                    _ = featureCollection.Core.Lifecycle.DisconnectAsync(connectionResult.RoomId, connectionResult.ConnectionId);
                }

                OnRoomReady(null);
                return Task.CompletedTask;
            }

            Task OnRoomStateChangedEventReceived(RoomStateChangedEvent eventMessage, CancellationToken cancellationToken)
            {
                if (eventMessage.Room.Id != connectionResult.RoomId)
                {
                    return Task.CompletedTask;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    InAppNotifier.Success($"Подключение к комнате {eventMessage.Room.Name} прошло успешно!");
                });
                OnRoomReady(eventMessage.Room);
                return Task.CompletedTask;
            }

            errorToken = featureCollection.Core.EventBus.Subscribe<ErrorOccurredEvent>(OnErrorOccured);

            if (args.ShowLoadingDialog)
            {
                _ = dialogService.ShowDialogAsync<LoadingViewModel>(async vm =>
                {
                    await vm.LoadRoomDataAndRefresh(OnRoomReady, args.Cts);
                    loadingVm = vm;
                });
            }
            else
            {
                roomStateToken = featureCollection.Core.EventBus.Subscribe<RoomStateChangedEvent>(OnRoomStateChangedEventReceived);
            }

            connectionResult = await featureCollection.Core.Lifecycle.ConnectAsync(args.Endpoint, args.ExpectedRoomId, args.Password, args.Cts.Token);

            if (loadingVm != null)
            {
                loadingVm.RoomId = connectionResult.RoomId;
            }

            var joined = await joinSettledTcs.Task.WaitAsync(TimeSpan.FromSeconds(15), args.Cts.Token);

            return new RoomJoinResult
            {
                Failure = joined ? null : JoinFailure.Error,
                PasswordRejected = passwordRejected,
            };
        }
        catch (OperationCanceledException)
        {
            errorToken?.Dispose();
            roomStateToken?.Dispose();
            loadingVm?.CloseByCode();
            return new RoomJoinResult
            {
                Failure = JoinFailure.Cancelled,
                PasswordRejected = passwordRejected,
            };
        }
        catch (RoomIdentityMismatchException ex)
        {
            errorToken?.Dispose();
            roomStateToken?.Dispose();
            loadingVm?.CloseByCode();
            var choiceDialog = await dialogService.ShowDialogAsync<RoomMismatchViewModel>(vm =>
            {
                vm.ActualRoom = ex.ActualRoom;
                vm.Cabinet = string.IsNullOrEmpty(ex.ActualRoom.Cabinet)
                    ? DesktopConstants.UndefinedPcName
                    : ex.ActualRoom.Cabinet;
                vm.IsReconnect = ex.ExistedBefore;
                vm.IsActualRoomOpen = chatViewsRegistry.TryGet(ex.ActualRoom.Id, out _);
            });

            var choice = choiceDialog?.Choice ?? RoomMismatchChoice.Cancel;

            return new RoomJoinResult
            {
                Failure = JoinFailure.Mismatch,
                RoomMismatchChoice = choice,
                RoomIdentityMismatchException = ex,
                PasswordRejected = passwordRejected,
            };
        }
        catch (Exception ex)
        {
            errorToken?.Dispose();
            roomStateToken?.Dispose();
            loadingVm?.CloseByCode();
            InAppNotifier.Error($"Произошла ошибка при подключении: {ex.Message}");
            return new RoomJoinResult
            {
                Failure = JoinFailure.Error,
                PasswordRejected = passwordRejected,
            };
        }
    }
}
