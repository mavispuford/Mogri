using System.Text.RegularExpressions;

namespace Mogri;

public static class Constants
{
    public static Regex ImageDataRegex = new Regex("^data:((?<type>[\\w\\/]+))?;base64,(?<data>.+)$", RegexOptions.Compiled);
    public const string ImageDataFormat = "data:{0};base64,{1}";
    public const string ImageDataCaptureGroupType = "type";
    public const string ImageDataCaptureGroupData = "data";
    public const string ThumbnailPrefix = "t.";
    public const double MinimumWidthHeight = 64;
    public const double MaximumWidthHeight = 2048;
    public const double MaximumDisplayWidthHeight = 2048;
    public const double ResolutionValueDivisor = 8;

    public static class PreferenceKeys
    {
        public const string ServerUrl = nameof(ServerUrl);
        public const string ComfyCloudApiKey = nameof(ComfyCloudApiKey);
        public const string ComfyUiModelType = nameof(ComfyUiModelType);
        public const string ComfyUiSelectedModel = nameof(ComfyUiSelectedModel);
        public const string ComfyCloudSelectedModel = nameof(ComfyCloudSelectedModel);
        public const string SelectedBackend = nameof(SelectedBackend);
        public const string AuthHeaderName = nameof(AuthHeaderName);
        public const string AuthHeaderValue = nameof(AuthHeaderValue);
    }
}
