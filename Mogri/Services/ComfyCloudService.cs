namespace Mogri.Services;

public class ComfyCloudService : ComfyUiService
{
    public override string Name => "Comfy Cloud";

    public ComfyCloudService(IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider) 
        : base(httpClientFactory, serviceProvider)
    {
    }
}
