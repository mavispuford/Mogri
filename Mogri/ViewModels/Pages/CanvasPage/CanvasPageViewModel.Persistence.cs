using Mogri.Enums;
using Mogri.Models;

namespace Mogri.ViewModels;

/// <summary>
/// Canvas page view model partial that persists and cleans up saved canvas overlay state for filesystem-backed source images.
/// </summary>
public partial class CanvasPageViewModel
{
    public override async Task OnDisappearingAsync()
    {
        await autoSaveOrDeleteMaskAsync();
        await base.OnDisappearingAsync();
    }

    /// <summary>
    /// Persists canvas overlay state to disk when the source image is from the filesystem,
    /// or deletes the stale state file if no masks or text elements remain.
    /// </summary>
    private async Task autoSaveOrDeleteMaskAsync()
    {
        if (string.IsNullOrEmpty(_sourceFileName))
        {
            return;
        }

        if (!await _autoMaskSaveLock.WaitAsync(0))
        {
            return;
        }

        try
        {
            var maskActions = CanvasActions?.Where(ca => ca.CanvasActionType == CanvasActionType.Mask).ToList() ?? new();
            var textElements = TextElements.Select(cloneTextElement).ToList();

            if (maskActions.Count > 0 || textElements.Count > 0)
            {
                await _fileService.WriteMaskFileToAppDataAsync(_sourceFileName,
                    new MaskViewModel
                    {
                        Lines = maskActions.OfType<MaskLineViewModel>().ToList(),
                        SegmentationMasks = maskActions.OfType<SegmentationMaskViewModel>().ToList(),
                        TextElements = textElements
                    });
            }
            else
            {
                await _fileService.DeleteMaskFileFromAppDataAsync(_sourceFileName);
            }
        }
        finally
        {
            _autoMaskSaveLock.Release();
        }
    }
}