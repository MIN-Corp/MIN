using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using MIN.Desktop.Contracts.Enums;
using MIN.Desktop.Contracts.Interfaces;
using MIN.Desktop.Contracts.Models;
using MIN.Desktop.ViewModels.Base;
using MIN.Desktop.ViewModels.Modals;
using MIN.Helpers.Contracts.Interfaces;
using MIN.Helpers.Contracts.Models.Enums;

namespace MIN.Desktop.Infrastructure.Services;

/// <inheritdoc cref="IDialogService"/>
public sealed class DialogService : IDialogService
{
    private readonly ILoggerProvider logger;
    private readonly Window dialogOwnerProvider;
    private readonly Dictionary<Type, ModalMapping> viewModelToWindowMap;
    private readonly object viewModelToWindowMapLocker = new();

    /// <summary>
    /// Инициализирует новый экземпляр <see cref="DialogService"/>
    /// </summary>
    public DialogService(Window dialogOwnerProvider,
        IEnumerable<ModalMapping> viewModelToWindowMap,
        ILoggerProvider logger)
    {
        this.logger = logger;
        this.dialogOwnerProvider = dialogOwnerProvider;
        this.viewModelToWindowMap = viewModelToWindowMap.ToDictionary(m => m.ViewModelType);
    }

    /// <inheritdoc />
    public async Task<TViewModel?> ShowAsync<TViewModel>(Action<TViewModel>? viewModelSetup = null)
       where TViewModel : ModalViewModelBase
    {
        try
        {
            ModalMapping? mapping;
            lock (viewModelToWindowMapLocker)
            {
                if (!viewModelToWindowMap.TryGetValue(typeof(TViewModel), out mapping))
                {
                    throw new Exception($"No dialog known for {typeof(TViewModel).Name}");
                }
            }
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Window dialog = mapping.WindowFactory(typeof(TViewModel));
                var viewModel = (TViewModel)dialog.DataContext!;
                viewModelSetup?.Invoke(viewModel);
                dialog.Show(dialogOwnerProvider);
                return viewModel;
            });
        }
        catch (Exception ex)
        {
            logger.Log($"Failed to show dialog for ViewModel {typeof(TViewModel).FullName}", LogLevel.Error);
            InAppNotifier.Error(ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<TViewModel?> ShowDialogAsync<TViewModel>(Action<TViewModel>? viewModelSetup = null)
        where TViewModel : ModalViewModelBase
    {
        try
        {
            ModalMapping? mapping;
            lock (viewModelToWindowMapLocker)
            {
                if (!viewModelToWindowMap.TryGetValue(typeof(TViewModel), out mapping))
                {
                    throw new Exception($"No dialog known for {typeof(TViewModel).Name}");
                }
            }
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Window dialog = mapping.WindowFactory(typeof(TViewModel));
                viewModelSetup?.Invoke((TViewModel)dialog.DataContext!);
                return dialog.ShowDialog<TViewModel>(dialogOwnerProvider);
            });
        }
        catch (Exception ex)
        {
            logger.Log($"Failed to show dialog for ViewModel {typeof(TViewModel).FullName}", LogLevel.Error);
            InAppNotifier.Error(ex.Message);
            return null;
        }
    }

    Task IDialogService.ShowErrorAsync(Exception exception, string? title, string? description) =>
        ShowDialogAsync<DialogBoxViewModel>(model =>
        {
            model.Title = title ?? "Error";
            model.Description = string.IsNullOrWhiteSpace(description) ? exception.ToString() : $"[b][i]{description}[/b][/i]{Environment.NewLine}{Environment.NewLine}[#f94239]{exception}";
            model.ButtonOptions = ButtonOptions.Ok;
        });
}
