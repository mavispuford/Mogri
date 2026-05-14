using SkiaSharp;

namespace Mogri.Models;

/// <summary>
/// Holds the merged bitmap produced by flattening canvas paint and masks plus ownership information for disposal.
/// </summary>
public sealed record CanvasFlattenWorkflowResult(SKBitmap MergedBitmap, bool TransfersPreparedSourceBitmapOwnership);