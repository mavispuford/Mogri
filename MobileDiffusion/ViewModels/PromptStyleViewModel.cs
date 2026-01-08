using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;

namespace MobileDiffusion.ViewModels;

public partial class PromptStyleViewModel : BaseViewModel, IPromptStyleViewModel
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [ObservableProperty]
    public partial string Prompt { get; set; }

    [ObservableProperty]
    public partial string NegativePrompt { get; set; }
}
