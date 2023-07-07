using MobileDiffusion.Enums;

namespace MobileDiffusion.Models;

/// <summary>
///     Generic API response object to handle more than one API (InvokeAI, Automatic1111, etc).
/// </summary>
public class ApiResponse
{
    public StableDiffusionApi StableDiffusionApi { get; set; }

    public object ResponseObject { get; set; }

    public double Progress { get; set; }
}