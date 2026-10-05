using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using MIN.Core.Events.Contracts.Models;
using MIN.Desktop.ViewModels.Base.Interfaces;

namespace MIN.Desktop.ViewModels.Base;

/// <summary>
/// Базовая view модель
/// </summary>
public abstract class ViewModelBase : ObservableObject, IViewModel
{
    /// <summary>
    /// Мешок с подписками
    /// </summary>
    protected SubscriptionBag Subscriptions = new();

    /// <summary>
    /// Освободить ресурсы
    /// </summary>
    public virtual void Dispose()
    {
        Subscriptions.Dispose();
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }
}
