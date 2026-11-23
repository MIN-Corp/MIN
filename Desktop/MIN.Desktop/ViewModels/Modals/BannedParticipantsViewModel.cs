using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using MIN.Core.Entities.Contracts.Extensions;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Cards;
using MIN.DI.FeatureCollection;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна чёрного списка участников
/// </summary>
public partial class BannedParticipantsViewModel : ModalViewModelBase
{
    private readonly IMinFeatureCollection featureCollection;
    private readonly IDialogService dialogService;
    private readonly CancellationTokenSource appCts;

    private Guid roomId;

    /// <summary>
    /// Забаненные участники
    /// </summary>
    [ObservableProperty]
    public partial AvaloniaList<BannedParticipantCardViewModel> BannedParticipants { get; set; } = [];

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="BannedParticipantsViewModel"/>
    /// </summary>
    public BannedParticipantsViewModel(IMinFeatureCollection featureCollection,
        IDialogService dialogService,
        ICtsProvider ctsProvider)
    {
        this.featureCollection = featureCollection;
        this.dialogService = dialogService;
        appCts = ctsProvider.AppCts;
    }

    /// <summary>
    /// Загрузить чёрный список комнаты
    /// </summary>
    public async void Init(Guid roomId)
    {
        this.roomId = roomId;
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var bans = await featureCollection.Chat.ChatRoomService.GetBannedParticipantsAsync(roomId, appCts.Token);

        BannedParticipants.Clear();

        foreach (var ban in bans)
        {
            var card = new BannedParticipantCardViewModel(ban.ParticipantId, ban.Name, FormatUnbanDate(ban.BannedUntil));

            card.OnEditClicked += async void (cardVm) => await EditBanAsync(cardVm);
            card.OnRemoveClicked += async void (cardVm) => await RemoveBanAsync(cardVm);

            BannedParticipants.Add(card);
        }
    }

    private async Task EditBanAsync(BannedParticipantCardViewModel card)
    {
        var banVm = await dialogService.ShowDialogAsync<ParticipantBanViewModel>(x => x.InitializeForEdit(card.ParticipantName));

        if (banVm! != true)
        {
            return;
        }

        try
        {
            await featureCollection.Chat.ChatRoomService.UpdateBanDateAsync(roomId,
                card.ParticipantId, banVm!.SelectedValue, appCts.Token);

            card.UpdateUnbanDate(FormatUnbanDate(banVm.SelectedValue.ToUnbanDate()));
        }
        catch (Exception ex)
        {
            InAppNotifier.Error(ex.Message);
        }
    }

    private async Task RemoveBanAsync(BannedParticipantCardViewModel card)
    {
        try
        {
            await featureCollection.Chat.ChatRoomService.UnbanParticipantAsync(roomId, card.ParticipantId, appCts.Token);
            BannedParticipants.Remove(card);
        }
        catch (Exception ex)
        {
            InAppNotifier.Error(ex.Message);
        }
    }

    private static string FormatUnbanDate(DateTimeOffset? bannedUntil)
        => bannedUntil is { } until
            ? $"до {until:dd.MM.yyyy HH:mm}"
            : "Навсегда";
}
