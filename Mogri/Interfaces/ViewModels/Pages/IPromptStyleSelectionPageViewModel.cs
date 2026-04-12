using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Mogri.Interfaces.ViewModels.Pages;

public interface IPromptStyleSelectionPageViewModel : IPageViewModel
{
    List<IPromptStyleViewModel> AvailablePromptStyles { get; set; }

    /// <summary>
    /// The styles currently selected by the user.
    /// </summary>
    /// <remarks>Uses <see cref="object"/> to satisfy MAUI's CollectionView.SelectedItems TwoWay binding contract, which requires IList&lt;object&gt;. Cast with OfType&lt;IPromptStyleViewModel&gt;() when consuming.</remarks>
    ObservableCollection<object> SelectedPromptStyles { get; set; }

    IAsyncRelayCommand CreatePromptStyleCommand { get; }

    IAsyncRelayCommand<IPromptStyleViewModel> DeletePromptStyleCommand { get; }

    IAsyncRelayCommand CancelCommand { get; }

    IAsyncRelayCommand ConfirmCommand { get; }

    string FilterText { get; set; }

    IRelayCommand ResetCommand { get; }

    IAsyncRelayCommand<IPromptStyleViewModel> EditPromptStyleCommand { get; }
}
