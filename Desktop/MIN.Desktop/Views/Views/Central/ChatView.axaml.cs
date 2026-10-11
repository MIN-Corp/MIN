using System;
using Avalonia.Controls;
using Avalonia.Input;
using MIN.Desktop.ViewModels.Pages.ChatViewModels;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views.Central;

/// <summary>
/// Страница чата
/// </summary>
public partial class ChatView : RoutableViewBase<ChatViewModel>
{
    private const double BottomThresholdPercent = 0.5;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatView"/>
    /// </summary>
    public ChatView()
    {
        InitializeComponent();
    }

    private void Border_DragEnter(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
        DragDropBorder.IsVisible = true;
    }

    private void Border_DragLeave(object? sender, DragEventArgs e)
    {
        DragDropBorder.IsVisible = false;
    }

    private void Border_DragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Copy;
    }

    private void Border_Drop(object? sender, DragEventArgs e)
    {
        DragDropBorder.IsVisible = false;
    }

    private void OnMessagesScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (DataContext is not ChatViewModel vm)
        {
            return;
        }

        var sv = MessagesScroller;
        var threshold = BottomThresholdPercent * sv.Viewport.Height;
        threshold = Math.Min(threshold, sv.Viewport.Height * 0.5);

        var atBottom = sv.Offset.Y + sv.Viewport.Height
                       >= sv.Extent.Height - threshold;

        vm.IsAtBottom = atBottom;
    }
}
