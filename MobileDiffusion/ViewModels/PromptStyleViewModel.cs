using CommunityToolkit.Mvvm.ComponentModel;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.ViewModels;

namespace MobileDiffusion.Models;

public partial class PromptStyleViewModel : BaseViewModel, IPromptStyleViewModel
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _prompt;

    [ObservableProperty]
    private string _negativePrompt;
}
