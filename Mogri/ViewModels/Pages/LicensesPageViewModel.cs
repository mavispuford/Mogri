using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Pages;

namespace Mogri.ViewModels.Pages;

/// <summary>
///     Implementation of the licenses page ViewModel, loading and preparing license entries for display.
/// </summary>
public partial class LicensesPageViewModel : PageViewModel, ILicensesPageViewModel
{
    private readonly ILicenseService _licenseService;
    private readonly IServiceProvider _serviceProvider;

    public ObservableCollection<ILicenseItemViewModel> Licenses { get; } = new();

    public LicensesPageViewModel(
        ILoadingService loadingService,
        ILicenseService licenseService,
        IServiceProvider serviceProvider) 
        : base(loadingService)
    {
        _licenseService = licenseService;
        _serviceProvider = serviceProvider;
    }

    public override async Task OnNavigatedToAsync()
    {
        await base.OnNavigatedToAsync();

        try
        {
            var entries = await _licenseService.GetLicensesAsync();
            
            Licenses.Clear();
            
            foreach (var entry in entries)
            {
                var viewModel = _serviceProvider.GetService<ILicenseItemViewModel>();
                if (viewModel != null)
                {
                    if (viewModel is LicenseItemViewModel impl)
                    {
                        impl.InitWith(entry);
                    }

                    Licenses.Add(viewModel);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load licenses in ViewModel: {ex.Message}");
        }
    }

}