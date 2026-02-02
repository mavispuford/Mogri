using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Pages;
using MobileDiffusion.Models;
using System.Collections.ObjectModel;

namespace MobileDiffusion.ViewModels;

internal partial class PromptStyleSelectionPageViewModel : PageViewModel, IPromptStyleSelectionPageViewModel
{
    private readonly IImageGenerationService _stableDiffusionService;
    private readonly IPopupService _popupService;
    private List<IPromptStyleViewModel> _allPromptStyles = new();
    private PromptSettings _settings;

    [ObservableProperty]
    public partial List<IPromptStyleViewModel> AvailablePromptStyles { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<object> SelectedPromptStyles { get; set; } = new();

    public PromptStyleSelectionPageViewModel(
        IImageGenerationService stableDiffusionService,
        IPopupService popupService,
        ILoadingService loadingService) : base(loadingService)
    {
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
    }


    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        try
        {
            _allPromptStyles = await _stableDiffusionService.GetPromptStylesAsync();
            AvailablePromptStyles = _allPromptStyles.ToList();

            if (_settings?.PromptStyles?.Any() == true)
            {
                var matchingStyles = AvailablePromptStyles.SelectMany(a => _settings.PromptStyles.Where(p => p.Name.Equals(a.Name, StringComparison.Ordinal)));
                SelectedPromptStyles = new ObservableCollection<object>(matchingStyles);
            }
        }
        catch
        {
            // TODO - Handle this
        }
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
    private async Task Cancel()
    {
        // Not modifying the settings, so just send the same ones back
        var parameters = new Dictionary<string, object>
            {
                { NavigationParams.PromptSettings, _settings }
            };

        await Shell.Current.GoToAsync("..", parameters);
    }

    [RelayCommand]
    private async Task Confirm()
    {
        if (SelectedPromptStyles != null)
        {
            var newSettings = _settings.Clone();
            newSettings.PromptStyles = SelectedPromptStyles
                .Distinct()
                .Select(ps => ps as PromptStyleViewModel)
                .Where(ps => !string.IsNullOrEmpty(ps.Prompt) || !string.IsNullOrEmpty(ps.NegativePrompt))
                .ToList();

            var parameters = new Dictionary<string, object>
            {
                { NavigationParams.PromptSettings, newSettings }
            };

            await Shell.Current.GoToAsync("..", parameters);
        }
        else
        {
            await Cancel();
        }
    }

    [RelayCommand]
    private async Task Filter(string filter)
    {
        var filteredStyles = await Task.Run(() =>
        {
            return _allPromptStyles.Where(ps =>
                    ps.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(ps.Name) && ps.Prompt.Contains(filter, StringComparison.OrdinalIgnoreCase)));
        });

        AvailablePromptStyles = filteredStyles.ToList();
    }

    [RelayCommand]
    private async Task ShowPromptStyleInfo(IPromptStyleViewModel promptStyleViewModel)
    {
        if (promptStyleViewModel == null || 
            (string.IsNullOrEmpty(promptStyleViewModel.Prompt) && string.IsNullOrEmpty(promptStyleViewModel.NegativePrompt)))
        {
            await Shell.Current.DisplayAlertAsync("No Style Info", "This style has no prompts.", "OK");

            return;
        }

        var parameters = new Dictionary<string, object>()
        {
            { NavigationParams.PromptStyle, promptStyleViewModel }
        };

        await _popupService.ShowPopupForResultAsync("PromptStyleInfoPopup", parameters);
    }

    public override bool OnBackButtonPressed()
    {
        ConfirmCommand.Execute(null);

        return true;
    }
}
