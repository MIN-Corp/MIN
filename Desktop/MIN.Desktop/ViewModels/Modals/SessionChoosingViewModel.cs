using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Infrastructure.Services;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Cards;
using MIN.Desktop.ViewModels.Windows;
using MIN.Sessions.Core.Services.Contracts.Enums;
using MIN.Sessions.Core.Services.Contracts.Interfaces;
using MIN.Sessions.Core.Services.Contracts.Models;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна выбора сессии
/// </summary>
public partial class SessionChoosingViewModel : ModalViewModelBase
{
    private readonly Window? parentWindow;
    private readonly ISessionScanner sessionScanner;
    private readonly ITunnelSessionStore tunnelSessionStore;
    private readonly IDialogService dialogService;
    private bool isHost;

    /// <summary>
    /// Выбранная сессия
    /// </summary>
    public Session? SelectedSession { get; set; }

    /// <summary>
    /// Выбран туннель
    /// </summary>
    public bool IsTunnelCreationRequested { get; set; }

    /// <summary>
    /// Скаченные сессии
    /// </summary>
    [ObservableProperty]
    public partial AvaloniaList<DownloadedSessionViewModel> AvaibleSessions { get; set; } = [];

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="SessionChoosingViewModel"/>
    /// </summary>
    public SessionChoosingViewModel(ISessionScanner sessionScanner,
        ITunnelSessionStore tunnelSessionStore,
        IDialogService dialogService)
    {
        parentWindow = MainWindowViewModel.GetWindow();
        this.sessionScanner = sessionScanner;
        this.tunnelSessionStore = tunnelSessionStore;
        this.dialogService = dialogService;
    }

    /// <summary>
    /// Инициализировать окно перед показом (построение карточек с учётом роли)
    /// </summary>
    public void Initialize(bool isHost)
    {
        this.isHost = isHost;
        RebuildCards();
    }

    private void RebuildCards()
    {
        foreach (var card in AvaibleSessions)
        {
            card.Dispose();
        }
        AvaibleSessions.Clear();

        foreach (var session in sessionScanner.DownloadedSessions.Values)
        {
            if (!isHost && session.Kind == SessionKind.PortTunnel)
            {
                continue;
            }

            var card = new DownloadedSessionViewModel(session, parentWindow?.Clipboard, isCreateTunnelCard: false);
            card.OnClicked += (selected) => OnSessionSelected(card, session, selected);
            card.OnDeleteRequested += () => DeleteSessionAsync(session);
            card.OnEditRequested += () => EditTunnelSessionAsync(session);
            AvaibleSessions.Add(card);
        }

        if (isHost)
        {
            var tunnelCard = new DownloadedSessionViewModel(null, parentWindow?.Clipboard, isCreateTunnelCard: true);
            tunnelCard.OnClicked += (selected) => OnSessionSelected(tunnelCard, null, selected);
            AvaibleSessions.Add(tunnelCard);
        }

        var exampleCard = AvaibleSessions.FirstOrDefault(x => !x.IsCreateTunnelCard);
        exampleCard?.Select();
    }

    private async Task EditTunnelSessionAsync(Session session)
    {
        var editForm = await dialogService.ShowDialogAsync<CreateTunnelSessionViewModel>(vm => vm.InitializeForEdit(session));

        if (editForm! != true)
        {
            return;
        }

        if (!Version.TryParse(editForm!.Version, out var version))
        {
            version = new Version(1, 0, 0);
        }

        var updated = new Session
        {
            SessionId = session.SessionId,
            Name = editForm.Name,
            Description = editForm.Description,
            Version = version,
            MaximumParticipants = editForm.MaxPlayers,
            Kind = SessionKind.PortTunnel,
            TunnelPort = editForm.Port,
            ServerExecutableFileName = session.ServerExecutableFileName,
            ClientExecutableFileName = session.ClientExecutableFileName,
            DownloadLink = string.Empty,
        };

        await tunnelSessionStore.SaveAsync(updated, editForm.Thumbnail);
        await sessionScanner.ScanAsync();

        RebuildCards();
    }

    private void OnSessionSelected(DownloadedSessionViewModel card, Session? session, bool selected)
    {
        foreach (var sessionVm in AvaibleSessions)
        {
            if (sessionVm.Session?.SessionId != session?.SessionId)
            {
                sessionVm.Unselect();
            }
        }
        SelectedSession = selected ? session : null;
        IsTunnelCreationRequested = selected && card.IsCreateTunnelCard;
        SelectSessionCommand.NotifyCanExecuteChanged();
    }

    private async Task DeleteSessionAsync(Session session)
    {
        bool confirmation = await dialogService.ShowDialogAsync<DialogBoxViewModel>(model =>
        {
            model.Title = "Удаление сессии";
            model.Description = $"Хотите удалить сессию \"{session.Name}\" с диска?\nЭто действие необратимо.";
            model.ButtonOptions = ButtonOptions.YesNo;
        });

        if (!confirmation)
        {
            return;
        }

        try
        {
            await sessionScanner.DeleteSessionAsync(session.SessionId);
        }
        catch (Exception)
        {
            InAppNotifier.Error("Не удалось удалить сессию: возможно, она сейчас запущена");
            return;
        }

        RebuildCards();
    }

    [RelayCommand(CanExecute = nameof(CanProceed))]
    private void SelectSession()
    {
        Close(ButtonOptions.Ok);
    }

    private bool CanProceed() => SelectedSession != null || IsTunnelCreationRequested;
}
