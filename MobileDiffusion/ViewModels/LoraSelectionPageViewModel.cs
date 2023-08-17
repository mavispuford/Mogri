using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Java.Lang;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using System.Collections.ObjectModel;

namespace MobileDiffusion.ViewModels;

public partial class LoraSelectionPageViewModel : PageViewModel, ILoraSelectionPageViewModel
{
    private readonly IStableDiffusionService _stableDiffusionService;

    private List<ILoraViewModel> _allLoras;
    private PromptSettings _settings;

    [ObservableProperty]
    List<ILoraViewModel> _availableLoras = new();

    [ObservableProperty]
    ObservableCollection<ILoraViewModel> _selectedLoras = new();

    [ObservableProperty]
    ILoraViewModel _loraToAdd;

    public LoraSelectionPageViewModel(IStableDiffusionService stableDiffusionService)
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
                var matchingLoras = AvailableLoras.SelectMany(a => _settings.Loras.Where(p => p.Name.Equals(a.Name, StringComparison.Ordinal)));
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
        var parameters = new Dictionary<string, object>
            {
                { NavigationParams.PromptSettings, _settings }
            };

        await Shell.Current.GoToAsync("..", parameters);
    }

    [RelayCommand]
    private async Task Confirm()
    {
        if (SelectedLoras != null)
        {
            var newSettings = _settings.Clone();
            newSettings.Loras = SelectedLoras
                .Distinct()
                .Select(l => l as LoraViewModel)
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

    partial void OnLoraToAddChanged(ILoraViewModel value)
    {
        if (value == null)
        {
            return;
        }

        SelectedLoras.Add(value);

        LoraToAdd = null;
    }
}
