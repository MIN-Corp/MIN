using System.Linq;
using Avalonia.Collections;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Cards;
using MIN.ScreenDemo.Services.Contracts.Enums;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.Desktop.ViewModels.Modals;

/// <summary>
/// Модель окна выбора источника демонстрации экрана
/// </summary>
public partial class ScreenDemoSourcePickerViewModel : ModalViewModelBase
{
    private readonly IScreenSourceService screenSourceService;

    /// <summary>
    /// Выбранный источник (результат диалога)
    /// </summary>
    public ScreenSourceInfo? SelectedSource { get; private set; }

    /// <summary>
    /// Записывать ли системный звук во время демонстрации
    /// </summary>
    [ObservableProperty]
    public partial bool WithSound { get; set; }

    /// <summary>
    /// Доступные мониторы
    /// </summary>
    [ObservableProperty]
    public partial AvaloniaList<ScreenDemoSourceCardViewModel> Monitors { get; set; } = [];

    /// <summary>
    /// Доступные окна приложений
    /// </summary>
    [ObservableProperty]
    public partial AvaloniaList<ScreenDemoSourceCardViewModel> Windows { get; set; } = [];

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoSourcePickerViewModel"/>
    /// </summary>
    public ScreenDemoSourcePickerViewModel(IScreenSourceService screenSourceService)
    {
        this.screenSourceService = screenSourceService;

        if (!Design.IsDesignMode)
        {
            RebuildSources();
        }
    }

    private void RebuildSources()
    {
        Monitors.Clear();
        Windows.Clear();

        foreach (var source in screenSourceService.GetSources())
        {
            var card = new ScreenDemoSourceCardViewModel(source);
            card.OnClicked += OnSourceCardClicked;
            (source.Kind == ScreenDemoSourceKind.Monitor ? Monitors : Windows).Add(card);
        }
    }

    private void OnSourceCardClicked(ScreenDemoSourceCardViewModel card, bool selected)
    {
        if (!selected)
        {
            return;
        }

        foreach (var group in new[] { Monitors, Windows })
        {
            foreach (var other in group.Where(x => x != card))
            {
                other.Unselect();
            }
        }

        SelectedSource = card.Source;
        StartDemoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanStartDemo))]
    private void StartDemo() => Close(ButtonOptions.Ok);

    private bool CanStartDemo() => SelectedSource != null;
}
