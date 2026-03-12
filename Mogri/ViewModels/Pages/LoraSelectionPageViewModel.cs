using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;
using Mogri.Models;
using System.Collections.ObjectModel;

namespace Mogri.ViewModels;

public partial class LoraSelectionPageViewModel : PageViewModel, ILoraSelectionPageViewModel
{
    private readonly IImageGenerationService _stableDiffusionService;
    private readonly IPopupService _popupService;

    private List<ILoraViewModel> _allLoras = new();
    private PromptSettings? _settings;

    [ObservableProperty]
    public partial List<ILoraViewModel> AvailableLoras { get; set; } = new();

    [ObservableProperty]
    public partial ObservableCollection<ILoraViewModel> SelectedLoras { get; set; } = new();

    [ObservableProperty]
    public partial ILoraViewModel? LoraToAdd { get; set; }

    public LoraSelectionPageViewModel(
        IImageGenerationService stableDiffusionService,
        IPopupService popupService,
        ILoadingService loadingService) : base(loadingService)
    {
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
        _popupService = popupService ?? throw new ArgumentNullException(nameof(popupService));
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

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        try
        {
            _allLoras = await _stableDiffusionService.GetLorasAsync();
            AvailableLoras = _allLoras.ToList();

            if (_settings?.Loras?.Any() == true)
            {
                var matchingLoras = AvailableLoras.Where(a => _settings.Loras.Any(p => p.Name.Equals(a.Name, StringComparison.Ordinal))).ToList();

                foreach (var lora in matchingLoras)
                {
                    var savedLora = _settings.Loras.First(p => p.Name.Equals(lora.Name, StringComparison.Ordinal));
                    lora.Strength = savedLora.Strength;
                }

                SelectedLoras = new ObservableCollection<ILoraViewModel>(matchingLoras);
            }
        }
        catch (Exception ex)
        {
            await _popupService.DisplayAlertAsync("Error", $"Failed to load Loras: {ex.Message}", "OK");
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

        await Shell.Current.GoToAsync("..", parameters);
    }

    [RelayCommand]
    private async Task Confirm()
    {
        if (SelectedLoras != null && _settings != null)
        {
            var newSettings = _settings.Clone();
            newSettings.Loras = SelectedLoras
                .Distinct()
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
    private void Remove(ILoraViewModel loraViewModel)
    {
        if (loraViewModel == null)
        {
            return;
        }

        SelectedLoras.Remove(loraViewModel);
    }


    [RelayCommand]
    private void Reset()
    {
        SelectedLoras.Clear();
    }

    public override bool OnBackButtonPressed()
    {
        ConfirmCommand.Execute(null);

        return true;
    }

    partial void OnLoraToAddChanged(ILoraViewModel? value)
    {
        if (value == null)
        {
            return;
        }

        SelectedLoras.Add(value);

        LoraToAdd = null;
    }
}
