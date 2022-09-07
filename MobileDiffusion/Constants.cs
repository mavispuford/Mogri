using System.Text.RegularExpressions;

namespace MobileDiffusion;

public static class Constants
{
    //public const string BaseUrl = "http://192.168.86.35:9090";
    public const string BaseUrl = "http://10.254.0.1:9090";

    public static Regex ImageDataRegex = new Regex("^data:((?<type>[\\w\\/]+))?;base64,(?<data>.+)$", RegexOptions.Compiled);

    public const string ImageDataFormat = "data:{0};base64,{1}";

    public const string ImageDataCaptureGroupType = "type";
    public const string ImageDataCaptureGroupData = "data";

}
