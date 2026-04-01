using System;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using Mogri.ViewModels;

namespace Mogri.Interfaces.ViewModels;

/// <summary>
/// Represents a single row in the canvas history popup.
/// Can be a mask stroke, segmentation mask, or snapshot checkpoint.
/// </summary>
public interface ICanvasHistoryItemViewModel
{
    string Icon { get; set; }
    bool IsColorVisible { get; }
    Color DisplayColor { get; set; }
    Color ColorWithAlpha { get; set; }
    double Alpha { get; set; }
    string Description { get; set; }
    CanvasActionViewModel? CanvasAction { get; set; }
    bool IsSnapshot { get; }
    bool IsEditable { get; }
    bool IsDuplicable { get; }
    bool IsDeletable { get; set; }
    IAsyncRelayCommand EditCommand { get; }
    IRelayCommand DeleteCommand { get; }
    IRelayCommand DuplicateCommand { get; }
    void InitWith(CanvasActionViewModel canvasAction, Action<ICanvasHistoryItemViewModel> deleteAction, Action<ICanvasHistoryItemViewModel> duplicateAction);
}
