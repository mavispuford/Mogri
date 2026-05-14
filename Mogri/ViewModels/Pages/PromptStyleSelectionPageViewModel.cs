using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiteDB;
using Microsoft.Extensions.DependencyInjection;
using Mogri.Interfaces.Coordinators;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using Mogri.Models;
using System.Collections.ObjectModel;

namespace Mogri.ViewModels;

internal partial class PromptStyleSelectionPageViewModel : PageViewModel, IPromptStyleSelectionPageViewModel
{
    private readonly IPromptStyleService _promptStyleService;
    private readonly IPopupService _popupService;
    private readonly IServiceProvider _serviceProvider;
    private List<IPromptStyleViewModel> _allPromptStyles = new();
    private PromptSettings? _settings;

    [ObservableProperty]
    public partial string FilterText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial List<IPromptStyleViewModel> AvailablePromptStyles { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<object> SelectedPromptStyles { get; set; } = new();

    public PromptStyleSelectionPageViewModel(
        IPromptStyleService promptStyleService,
        IPopupService popupService,
        ILoadingCoordinator loadingCoordinator,
        IServiceProvider serviceProvider,
        INavigationService navigationService) : base(loadingCoordinator, navigationService)
    {
        _promptStyleService = promptStyleService ?? throw new ArgumentNullException(nameof(promptStyleService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }


    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        try
        {
            await loadPromptStyles();
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Failed to load prompt styles: {ex.Message}", "OK");
        }
    }

    private async Task loadPromptStyles()
    {
        var selectedStyleNames = SelectedPromptStyles
            .OfType<IPromptStyleViewModel>()
            .Select(ps => ps.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);

        if (selectedStyleNames.Count == 0 && _settings?.PromptStyles?.Any() == true)
        {
            selectedStyleNames = _settings.PromptStyles
                .Select(ps => ps.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.Ordinal);
        }

        var entities = await _promptStyleService.GetAllAsync();
        _allPromptStyles = entities.Select(mapEntityToPromptStyleViewModel).ToList();

        AvailablePromptStyles = _allPromptStyles.ToList();

        if (selectedStyleNames.Count > 0)
        {
            var matchingStyles = AvailablePromptStyles.Where(style => selectedStyleNames.Contains(style.Name));
            SelectedPromptStyles = new ObservableCollection<object>(matchingStyles);
        }
        else
        {
            SelectedPromptStyles = new ObservableCollection<object>();
        }
    }

    private IPromptStyleViewModel mapEntityToPromptStyleViewModel(PromptStyleEntity entity)
    {
        var promptStyle = _serviceProvider.GetRequiredService<IPromptStyleViewModel>();
        promptStyle.EntityId = entity.Id;
        promptStyle.Name = entity.Name;
        promptStyle.Prompt = entity.Prompt;
        promptStyle.NegativePrompt = entity.NegativePrompt;
        return promptStyle;
    }

    private async Task<bool> showPromptStyleEditPopup(IPromptStyleViewModel promptStyleViewModel)
    {
        var parameters = new Dictionary<string, object>()
        {
            { NavigationParams.PromptStyle, promptStyleViewModel }
        };

        var popupResult = await _popupService.ShowPopupForResultAsync("PromptStyleEditPopup", parameters);
        var wasSaved = popupResult is bool saved && saved;

        if (wasSaved)
        {
            await loadPromptStyles();
        }

        return wasSaved;
    }

    public override void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.PromptSettings, out var promptSettingsParam) &&
            promptSettingsParam is PromptSettings promptSettings)
        {
            _settings = promptSettings;
        }
        else
        {
            throw new ArgumentException(nameof(NavigationParams.PromptSettings));
        }

        query.Clear();
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedPromptStyles.Clear();
    }

    [RelayCommand]
    private async Task CreatePromptStyle()
    {
        try
        {
            var styleName = await _popupService.DisplayPromptAsync("Create Prompt Style", "Enter a name for this prompt style:");

            if (string.IsNullOrWhiteSpace(styleName))
            {
                return;
            }

            var entity = new PromptStyleEntity
            {
                Name = styleName.Trim(),
                Prompt = string.Empty,
                NegativePrompt = string.Empty
            };

            await _promptStyleService.SaveAsync(entity);

            var promptStyle = mapEntityToPromptStyleViewModel(entity);
            var wasSaved = await showPromptStyleEditPopup(promptStyle);

            if (!wasSaved)
            {
                await loadPromptStyles();
            }
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Failed to create prompt style: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task DeletePromptStyle(IPromptStyleViewModel promptStyle)
    {
        if (promptStyle == null)
        {
            return;
        }

        var confirmed = await _popupService.DisplayAlertAsync(
            "Delete Prompt Style",
            $"Are you sure you want to delete '{promptStyle.Name}'?",
            "DELETE",
            "Cancel");

        if (!confirmed)
        {
            return;
        }

        if (promptStyle.EntityId is not ObjectId id)
        {
            await _popupService.DisplayAlertAsync("Error", "Unable to delete prompt style because its identifier is missing.", "OK");
            return;
        }

        try
        {
            await _promptStyleService.DeleteAsync(id);

            _allPromptStyles = _allPromptStyles
                .Where(style => style.EntityId is not ObjectId entityId || entityId != id)
                .ToList();

            AvailablePromptStyles = AvailablePromptStyles
                .Where(style => style.EntityId is not ObjectId entityId || entityId != id)
                .ToList();

            var selectedStyles = SelectedPromptStyles
                .OfType<IPromptStyleViewModel>()
                .Where(style => style.EntityId is not ObjectId entityId || entityId != id);

            SelectedPromptStyles = new ObservableCollection<object>(selectedStyles);
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Failed to delete prompt style: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task Cancel()
    {
        // Not modifying the settings, so just send the same ones back
        var parameters = new Dictionary<string, object?>
            {
                { NavigationParams.PromptSettings, _settings }
            };

        await NavigationService.GoBackAsync(parameters!);
    }

    [RelayCommand]
    private async Task Confirm()
    {
        if (SelectedPromptStyles != null && _settings != null)
        {
            var newSettings = _settings.Clone();
            newSettings.PromptStyles = SelectedPromptStyles
                .OfType<IPromptStyleViewModel>()
                .Distinct()
                .ToList();

            var parameters = new Dictionary<string, object>
            {
                { NavigationParams.PromptSettings, newSettings }
            };

            await NavigationService.GoBackAsync(parameters);
        }
        else
        {
            await Cancel();
        }
    }

    partial void OnFilterTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AvailablePromptStyles = _allPromptStyles.ToList();
            return;
        }

        AvailablePromptStyles = _allPromptStyles.Where(ps =>
                ps.Name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(ps.Prompt) && ps.Prompt.Contains(value, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    [RelayCommand]
    private async Task EditPromptStyle(IPromptStyleViewModel promptStyleViewModel)
    {
        if (promptStyleViewModel == null)
        {
            return;
        }

        await showPromptStyleEditPopup(promptStyleViewModel);
    }

    public override bool OnBackButtonPressed()
    {
        ConfirmCommand.Execute(null);

        return true;
    }

}