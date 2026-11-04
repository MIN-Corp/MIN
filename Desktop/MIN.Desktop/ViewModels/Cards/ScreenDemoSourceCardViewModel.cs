using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MIN.ScreenDemo.Services.Contracts.Enums;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.Desktop.ViewModels.Cards;

/// <summary>
/// Карточка источника демонстрации экрана (монитор или окно)
/// </summary>
public partial class ScreenDemoSourceCardViewModel : ObservableObject
{
    /// <summary>
    /// Информация об источнике
    /// </summary>
    public ScreenSourceInfo Source { get; }

    /// <summary>
    /// Отображаемое название источника
    /// </summary>
    public string Title => Source.Title;

    /// <summary>
    /// Монитор ли это (для выбора иконки)
    /// </summary>
    public bool IsMonitor => Source.Kind == ScreenDemoSourceKind.Monitor;

    /// <summary>
    /// Выбран ли источник
    /// </summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>
    /// Событие клика по карточке
    /// </summary>
    public event Action<ScreenDemoSourceCardViewModel, bool>? OnClicked;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ScreenDemoSourceCardViewModel"/>
    /// </summary>
    public ScreenDemoSourceCardViewModel(ScreenSourceInfo source)
    {
        Source = source;
    }

    /// <summary>
    /// Выбрать карточку
    /// </summary>
    public void Select()
    {
        IsSelected = true;
        OnClicked?.Invoke(this, true);
    }

    /// <summary>
    /// Снять выделение
    /// </summary>
    public void Unselect() => IsSelected = false;

    [RelayCommand]
    private void SelectCard() => Select();
}
