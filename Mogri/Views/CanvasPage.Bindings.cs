using Mogri.Interfaces.ViewModels.Pages;
using Mogri.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Mogri.Views;

public partial class CanvasPage
{
    private void OnCanvasActionsChanged(ObservableCollection<CanvasActionViewModel>? oldValue, ObservableCollection<CanvasActionViewModel>? newValue)
    {
        if (oldValue != null)
        {
            oldValue.CollectionChanged -= CanvasActions_CollectionChanged;
            foreach (var item in oldValue)
            {
                item.PropertyChanged -= OnCanvasActionPropertyChanged;
            }
        }

        if (newValue != null)
        {
            newValue.CollectionChanged += CanvasActions_CollectionChanged;
            foreach (var item in newValue)
            {
                item.PropertyChanged += OnCanvasActionPropertyChanged;
            }
        }

        MaskCanvasView.InvalidateSurface();
    }

    private void CanvasActions_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (CanvasActionViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnCanvasActionPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (CanvasActionViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnCanvasActionPropertyChanged;
            }
        }

        MaskCanvasView.InvalidateSurface();
    }

    private void OnCanvasActionPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        MaskCanvasView.InvalidateSurface();
    }

    private void OnTextElementsChanged(ObservableCollection<TextElementViewModel>? oldValue, ObservableCollection<TextElementViewModel>? newValue)
    {
        if (oldValue != null)
        {
            oldValue.CollectionChanged -= TextElements_CollectionChanged;
            foreach (var item in oldValue)
            {
                item.PropertyChanged -= OnTextElementPropertyChanged;
            }
        }

        if (newValue != null)
        {
            newValue.CollectionChanged += TextElements_CollectionChanged;
            foreach (var item in newValue)
            {
                item.PropertyChanged += OnTextElementPropertyChanged;
            }
        }

        TextCanvasView.InvalidateSurface();
        TemporaryCanvasView.InvalidateSurface();
    }

    private void TextElements_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (TextElementViewModel item in e.OldItems)
            {
                item.PropertyChanged -= OnTextElementPropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (TextElementViewModel item in e.NewItems)
            {
                item.PropertyChanged += OnTextElementPropertyChanged;
            }
        }

        if (_textInteraction.SelectedTextElement != null && (TextElements == null || !TextElements.Contains(_textInteraction.SelectedTextElement)))
        {
            resetTextInteractionState(clearSelection: true, clearTapState: true);
        }

        TextCanvasView.InvalidateSurface();
        TemporaryCanvasView.InvalidateSurface();
    }

    private void OnTextElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (isTextElementRenderProperty(e.PropertyName))
        {
            TextCanvasView.InvalidateSurface();
        }

        if (sender is TextElementViewModel textElement && shouldInvalidateTemporaryCanvas(textElement, e.PropertyName))
        {
            TemporaryCanvasView.InvalidateSurface();
        }
    }

    private void OnSourceBitmapChanged()
    {
        SegmentationBitmap = null;

        UpdateCanvasSizes();

        if (BindingContext is ICanvasPageViewModel vm && vm.PreserveZoomOnNextBitmapChange)
        {
            vm.PreserveZoomOnNextBitmapChange = false;
        }
        else
        {
            ZoomContainer.Reset();
        }
    }

    private void OnSegmentationBitmapChanged()
    {
        SegmentationMaskCanvasView.InvalidateSurface();
    }

    private static bool isTextElementRenderProperty(string? propertyName)
    {
        return propertyName is nameof(TextElementViewModel.Text)
            or nameof(TextElementViewModel.X)
            or nameof(TextElementViewModel.Y)
            or nameof(TextElementViewModel.Scale)
            or nameof(TextElementViewModel.Rotation)
            or nameof(TextElementViewModel.Color)
            or nameof(TextElementViewModel.Alpha)
            or nameof(TextElementViewModel.Noise)
            or nameof(TextElementViewModel.IsSelected);
    }

    private static bool shouldInvalidateTemporaryCanvas(TextElementViewModel textElement, string? propertyName)
    {
        return propertyName == nameof(TextElementViewModel.IsSelected)
            || (textElement.IsSelected && isTextElementRenderProperty(propertyName));
    }
}