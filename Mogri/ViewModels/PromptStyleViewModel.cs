using CommunityToolkit.Mvvm.ComponentModel;
using Mogri.Interfaces.ViewModels;
using Mogri.ViewModels;

namespace Mogri.ViewModels;

public partial class PromptStyleViewModel : BaseViewModel, IPromptStyleViewModel
{
    [ObservableProperty]
    public partial object? EntityId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Prompt { get; set; }

    [ObservableProperty]
    public partial string NegativePrompt { get; set; }
}
