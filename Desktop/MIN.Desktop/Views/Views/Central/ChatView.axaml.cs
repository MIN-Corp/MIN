using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MIN.Desktop.ViewModels.Cards.Messages;
using MIN.Desktop.ViewModels.Pages.ChatViewModels;
using MIN.Desktop.Views.Base;

namespace MIN.Desktop.Views.Views.Central;

/// <summary>
/// Страница чата
/// </summary>
public partial class ChatView : RoutableViewBase<ChatViewModel>
{
    private const double BottomThresholdPercent = 0.5;
    private const double AnchorTolerance = 0.5;

    private ChatViewModel? chatVm;
    private bool wasAtBottom = true;
    private BaseChatMessageViewModel? prependAnchorCard;
    private double prependAnchorSubOffset;

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="ChatView"/>
    /// </summary>
    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChangedEvent;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeFromViewModel();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnDataContextChangedEvent(object? sender, EventArgs e)
    {
        UnsubscribeFromViewModel();
        chatVm = DataContext as ChatViewModel;

        if (chatVm == null)
        {
            return;
        }

        chatVm.HistoryPrependStarted += OnHistoryPrependStarted;
        chatVm.HistoryPrependCompleted += OnHistoryPrependCompleted;
        chatVm.ScrollRestoreRequested += OnScrollRestoreRequested;
    }

    private void UnsubscribeFromViewModel()
    {
        if (chatVm == null)
        {
            return;
        }

        chatVm.HistoryPrependStarted -= OnHistoryPrependStarted;
        chatVm.HistoryPrependCompleted -= OnHistoryPrependCompleted;
        chatVm.ScrollRestoreRequested -= OnScrollRestoreRequested;
        chatVm = null;
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

    private void OnHistoryPrependStarted()
    {
        (prependAnchorCard, prependAnchorSubOffset) = FindTopVisibleCard();
    }

    private void OnHistoryPrependCompleted()
    {
        var card = prependAnchorCard;
        if (card is null)
        {
            return;
        }

        var subOffset = prependAnchorSubOffset;

        Dispatcher.UIThread.Post(() =>
        {
            if (chatVm is null || chatVm.Messages.IndexOf(card) < 0)
            {
                prependAnchorCard = null;
                return;
            }

            MessagesControl.ScrollIntoView(card);

            Dispatcher.UIThread.Post(() =>
            {
                if (prependAnchorCard != card)
                {
                    return;
                }

                ApplyAnchorOffset(card, subOffset);
                prependAnchorCard = null;
            }, DispatcherPriority.Loaded);
        }, DispatcherPriority.Loaded);
    }

    private void OnScrollRestoreRequested(BaseChatMessageViewModel card, double subOffset)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (chatVm is null || chatVm.Messages.IndexOf(card) < 0)
            {
                return;
            }

            MessagesControl.ScrollIntoView(card);

            Dispatcher.UIThread.Post(() => ApplyAnchorOffset(card, subOffset), DispatcherPriority.Loaded);
        }, DispatcherPriority.Loaded);
    }

    private void OnMessagesScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (chatVm is null)
        {
            return;
        }

        if (e.ViewportDelta.Y < 0 && wasAtBottom)
        {
            chatVm.IsAtBottom = true;
            chatVm.UpdateScrollAnchor(null, 0);
            wasAtBottom = true;

            Dispatcher.UIThread.Post(() =>
            {
                var max = Math.Max(0, MessagesScroller.Extent.Height - MessagesScroller.Viewport.Height);
                MessagesScroller.Offset = MessagesScroller.Offset.WithY(max);
            }, DispatcherPriority.Loaded);
            return;
        }

        var sv = MessagesScroller;
        var threshold = BottomThresholdPercent * sv.Viewport.Height;
        threshold = Math.Min(threshold, sv.Viewport.Height * 0.5);

        var atBottom = sv.Offset.Y + sv.Viewport.Height
                       >= sv.Extent.Height - threshold;

        chatVm.IsAtBottom = atBottom;

        if (atBottom)
        {
            chatVm.UpdateScrollAnchor(null, 0);
        }
        else if (prependAnchorCard is null)
        {
            var (card, subOffset) = FindTopVisibleCard();
            chatVm.UpdateScrollAnchor(card, subOffset);
        }

        wasAtBottom = atBottom;
    }

    private (BaseChatMessageViewModel? card, double subOffset) FindTopVisibleCard()
    {
        BaseChatMessageViewModel? topCard = null;
        var topY = double.MaxValue;

        foreach (var container in MessagesControl.GetRealizedContainers())
        {
            if (MessagesControl.ItemFromContainer(container) is not BaseChatMessageViewModel card
                || card.Message is null)
            {
                continue;
            }

            var y = container.TranslatePoint(default, MessagesScroller)?.Y;
            if (y is null || y.Value + container.Bounds.Height <= 0)
            {
                continue;
            }

            if (y.Value < topY)
            {
                topY = y.Value;
                topCard = card;
            }
        }

        return topCard is null ? (null, 0) : (topCard, Math.Max(0, -topY));
    }

    private void ApplyAnchorOffset(BaseChatMessageViewModel card, double subOffset)
    {
        var container = MessagesControl.ContainerFromItem(card);

        var y = container?.TranslatePoint(default, MessagesScroller)?.Y;
        if (y is null)
        {
            return;
        }

        var delta = y.Value - subOffset;
        if (Math.Abs(delta) > AnchorTolerance)
        {
            MessagesScroller.Offset = MessagesScroller.Offset.WithY(
                Math.Max(0, MessagesScroller.Offset.Y + delta));
        }
    }
}
