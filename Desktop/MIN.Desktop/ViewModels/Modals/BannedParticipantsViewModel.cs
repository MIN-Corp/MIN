using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private readonly IMinFeatureCollection featureCollection = null!;
    private readonly IDialogService dialogService = null!;
    private readonly CancellationTokenSource appCts = null!;

    private Guid roomId;

    /// <summary>
    /// Забаненные участники
    /// </summary>
    [ObservableProperty]
    public partial AvaloniaList<BannedParticipantCardViewModel> BannedParticipants { get; set; } = [];

    /// <summary>
    /// Есть ли забаненные участники
    /// </summary>
    [ObservableProperty]
    public partial bool HasBannedParticipants { get; set; }

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
    /// Инициализирует новый экземпляр <see cref="BannedParticipantsViewModel"/>
    /// </summary>
    public BannedParticipantsViewModel() { }

    /// <summary>
    /// Загрузить чёрный список комнаты
    /// </summary>
    public async void Init(Guid roomId)
    {
        try
        {
            this.roomId = roomId;
            await ReloadAsync();
        }
        catch (Exception e)
        {
            InAppNotifier.Error($"Не удалось загрузить чёрный список {e.Message}");
            CloseByCode();
        }
    }

    private async Task ReloadAsync()
    {
        var bans = await featureCollection.Chat.ChatRoomService.GetBannedParticipantsAsync(roomId, appCts.Token);

        BannedParticipants.Clear();

        foreach (var ban in bans)
        {
            var card = new BannedParticipantCardViewModel(ban);

            card.OnEditClicked += async void (cardVm) => await EditBanAsync(cardVm);
            card.OnRemoveClicked += async void (cardVm) => await RemoveBanAsync(cardVm);

            BannedParticipants.Add(card);
            HasBannedParticipants = true;
        }
    }

    private async Task EditBanAsync(BannedParticipantCardViewModel card)
    {
        var banVm = await dialogService.ShowDialogAsync<ParticipantBanViewModel>(x => x.InitializeForEdit(card.BanInfo));

        if (banVm! != true)
        {
            return;
        }

        try
        {
            var updatedBan = await featureCollection.Chat.ChatRoomService.UpdateBanAsync(roomId,
                card.BanInfo.ParticipantId, banVm!.Reason, banVm.SelectedValue, banVm.BanByIp, appCts.Token);

            if (updatedBan != null)
            {
                card.UpdateBan(updatedBan);
            }
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
            await featureCollection.Chat.ChatRoomService.UnbanParticipantAsync(roomId, card.BanInfo.ParticipantId, appCts.Token);
            BannedParticipants.Remove(card);
            HasBannedParticipants = BannedParticipants.Count > 0;
        }
        catch (Exception ex)
        {
            InAppNotifier.Error(ex.Message);
        }
    }
}
