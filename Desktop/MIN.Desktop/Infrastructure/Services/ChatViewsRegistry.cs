using System;
using System.Collections.Generic;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.ViewModels.Pages.ChatViewModels;

namespace MIN.Desktop.Infrastructure.Services;

internal class ChatViewsRegistry : IChatViewsRegistry
{
    private readonly Dictionary<Guid, ChatViewModel> activeChatViews = [];

    void IChatViewsRegistry.Register(Guid roomId, ChatViewModel viewModel)
        => activeChatViews[roomId] = viewModel;

    bool IChatViewsRegistry.TryGet(Guid roomId, out ChatViewModel? viewModel)
        => activeChatViews.TryGetValue(roomId, out viewModel);

    void IChatViewsRegistry.Unregister(Guid roomId)
        => activeChatViews.Remove(roomId);
}
