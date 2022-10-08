using System.Text.RegularExpressions;

namespace MobileDiffusion;

public static class Constants
{
    public static Regex ImageDataRegex = new Regex("^data:((?<type>[\\w\\/]+))?;base64,(?<data>.+)$", RegexOptions.Compiled);

    public const string ImageDataFormat = "data:{0};base64,{1}";

    public const string ImageDataCaptureGroupType = "type";
    public const string ImageDataCaptureGroupData = "data";

    public static class PreferenceKeys
    {
        public const string ServerUrl = nameof(ServerUrl);
    }
}
