using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.Services;
using Mogri.Interfaces.ViewModels;
using Mogri.Interfaces.ViewModels.Popups;

namespace Mogri.ViewModels;

public partial class PromptStyleInfoPopupViewModel : PopupBaseViewModel, IPromptStyleInfoPopupViewModel
{
    [ObservableProperty]
    public partial IPromptStyleViewModel PromptStyle { get; set; }

    public PromptStyleInfoPopupViewModel(IPopupService popupService) : base(popupService)
    { }

    public override async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        base.ApplyQueryAttributes(query);

        if (query.TryGetValue(NavigationParams.PromptStyle, out var promptStyleParam) &&
            promptStyleParam is IPromptStyleViewModel promptStyle)
        {
            PromptStyle = promptStyle;
        }
        else
        {
            // Wrap in Task.Run() so we don't crash if an exception is thrown because we are in an async void
            try
            {
                await Task.Run(async () =>
                {
                    await ClosePopupAsync();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to close popup: {ex}");
            }
        }

        // Workaround for https://github.com/dotnet/maui/issues/10294
        query.Clear();
    }

    [RelayCommand]
    private async Task Close()
    {
        await ClosePopupAsync();
    }

}