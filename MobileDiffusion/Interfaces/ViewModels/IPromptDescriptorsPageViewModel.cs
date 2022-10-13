using CommunityToolkit.Mvvm.Input;
using MobileDiffusion.Models;
using MobileDiffusion.ViewModels;
using System.Collections.ObjectModel;

namespace MobileDiffusion.Interfaces.ViewModels;

internal interface IPromptDescriptorsPageViewModel : IPageViewModel
{
    List<PromptDescriptorGroup> DescriptorGroups { get; set; }

    ObservableCollection<object> SelectedDescriptors { get; set; }

    IRelayCommand ResetCommand { get; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmCommand { get; }
}
