using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Mogri.Interfaces.ViewModels;

namespace Mogri.Interfaces.ViewModels.Popups;

/// <summary>
/// ViewModel interface for the canvas history popup, showing all canvas actions
/// including mask strokes and snapshot checkpoints.
/// </summary>
public interface ICanvasHistoryPopupViewModel : IPopupBaseViewModel
{
    ObservableCollection<ICanvasHistoryItemViewModel> Items { get; }
    IAsyncRelayCommand ClearAllCommand { get; }
    IAsyncRelayCommand ClearMasksCommand { get; }
    IAsyncRelayCommand CloseCommand { get; }
}
