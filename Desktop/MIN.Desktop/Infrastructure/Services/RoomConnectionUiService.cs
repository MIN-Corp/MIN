using System;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly IDialogService dialogService;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="RoomConnectionUiService"/>
    /// </summary>
    public RoomConnectionUiService(IMinFeatureCollection featureCollection,
        IDialogService dialogService)
    {
        this.featureCollection = featureCollection;
        this.dialogService = dialogService;
    }

    async Task<RoomHostResult> IRoomConnectionUiService.HostAsync(RoomHostArgs args, CancellationToken cancellationToken)
    {
        try
        {
            var room = await featureCollection.Core.Lifecycle.StartHostingAsync(args.RoomInfo, args.NetworkOptions, cancellationToken);
            await featureCollection.Chat.ChatRoomService.ManageDiscoveryOutOfSettings(args.RoomInfo,
                room.ConnectionAddresses, args.NetworkOptions, cancellationToken: cancellationToken);

            await args.OnRoomReady(room);

            return new RoomHostResult()
            {
                Room = room,
            };
        }
        catch (OperationCanceledException)
        {
            return new RoomHostResult()
            {
                Failure = HostFailure.Cancelled,
                ErrorMessage = "Создание комнаты было отменено"
            };
        }
        catch (Exception ex)
        {
            return new RoomHostResult()
            {
                Failure = HostFailure.Error,
                ErrorMessage = $"Не удалось создать комнату: {ex.Message}"
            };
        }
    }

    async Task<RoomJoinResult> IRoomConnectionUiService.JoinAsync(RoomJoinArgs args, CancellationToken cancellationToken)
    {
        LoadingViewModel? loadingVm = null;

        try
        {
            ConnectionResult connectionResult = new();

            _ = dialogService.ShowDialogAsync<LoadingViewModel>(async vm =>
            {
                await vm.LoadRoomDataAndRefresh(async room =>
                {
                    if (room == null)
                    {
                        return;
                    }

                    await args.OnRoomReady(room, connectionResult.ConnectionId);
                }, args.Cts, DesktopConstants.RoomConnectionTimeoutMs);

                loadingVm = vm;
            });

            connectionResult = await featureCollection.Core.Lifecycle.ConnectAsync(args.Endpoint, args.ExpectedRoomId, args.Cts.Token);

            if (loadingVm != null)
            {
                loadingVm.RoomId = connectionResult.RoomId;
            }

            return new RoomJoinResult()
            {
                RoomId = connectionResult.RoomId,
                ConnectionId = connectionResult.ConnectionId,
            };
        }
        catch (RoomIdentityMismatchException ex)
        {
            loadingVm?.CloseByCode();
            var choiceDialog = await dialogService.ShowDialogAsync<RoomMismatchViewModel>(vm =>
            {
                vm.ActualRoom = ex.ActualRoom;
                vm.IsReconnect = ex.ExistedBefore;
            });

            var choice = choiceDialog?.Choice ?? RoomMismatchChoice.Cancel;

            return new RoomJoinResult()
            {
                Failure = JoinFailure.Mismatch,
                RoomMismatchChoice = choice,
                RoomIdentityMismatchException = ex,
                RoomId = ex.ExpectedRoomId,
                ConnectionId = ex.ConnectionId,
            };
        }
        catch (Exception ex)
        {
            loadingVm?.CloseByCode();
            InAppNotifier.Error($"Произошла ошибка при подключении: {ex.Message}");
            return new RoomJoinResult()
            {
                Failure = JoinFailure.Error,
            };
        }
    }
}
