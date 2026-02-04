using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Interfaces.ViewModels.Pages;
using MobileDiffusion.Models;
using System.Collections.ObjectModel;

namespace MobileDiffusion.ViewModels;

public partial class LoraSelectionPageViewModel : PageViewModel, ILoraSelectionPageViewModel
{
    private readonly IImageGenerationService _stableDiffusionService;

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
        ILoadingService loadingService) : base(loadingService)
    {
        _stableDiffusionService = stableDiffusionService ?? throw new ArgumentNullException(nameof(stableDiffusionService));
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
                var matchingLoras = AvailableLoras.Where(a => _settings.Loras.Any(p => p.Name.Equals(a.Name, StringComparison.Ordinal)));
                SelectedLoras = new ObservableCollection<ILoraViewModel>(matchingLoras);
            }
        }
        catch
        {
            // TODO - Handle this
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
                .OfType<LoraViewModel>()
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
