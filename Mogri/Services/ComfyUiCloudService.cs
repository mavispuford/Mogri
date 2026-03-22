namespace Mogri.Services;

public class ComfyUiCloudService : ComfyUiService
{
    public override string Name => "ComfyUI Cloud";

    public ComfyUiCloudService(IHttpClientFactory httpClientFactory, IServiceProvider serviceProvider) 
        : base(httpClientFactory, serviceProvider)
    {
    }
}
