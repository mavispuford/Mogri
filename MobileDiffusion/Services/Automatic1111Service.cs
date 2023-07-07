using MobileDiffusion.Clients.Automatic1111;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Models;
using MobileDiffusion.Models.Automatic1111;
using Newtonsoft.Json;

namespace MobileDiffusion.Services
{
    internal class Automatic1111Service : IStableDiffusionService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        private ICollection<SamplerItem> _samplers;
        private ICollection<PromptStyleItem> _promptStyles;
        private ICollection<LoraItem> _loras;

        public Automatic1111Service(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task<bool> CheckServer()
        {
            var client = getClient();

            try
            {
                await client._lambda__internal_ping_getAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public Task<byte[]> GetImageBytesAsync(string url)
        {
            throw new NotImplementedException();
        }

        public async IAsyncEnumerable<ApiResponse> SubmitTextToImageRequest(Settings settings)
        {
            var client = getClient();

            var request = Txt2ImageRequestFromSettings(settings);

            var cancellationTokenSource = new CancellationTokenSource();
            var progressToken = cancellationTokenSource.Token;

            TextToImageResponse txt2ImgResponse = null;

            var textToImageTask = Task.Run(async () =>
            {
                try
                {
                    txt2ImgResponse = await client.Text2imgapi_sdapi_v1_txt2img_postAsync(request);
                }
                finally
                {
                    cancellationTokenSource.Cancel();
                }
            });

            ApiResponse apiResponse = null;
            var skipCurrentImage = false;

            while (true)
            {
                try
                {
                    apiResponse = await Task.Run(async () =>
                    {
                        await Task.Delay(500);

                        progressToken.ThrowIfCancellationRequested();

                        var progressGetResponse = await client.Progressapi_sdapi_v1_progress_getAsync(skipCurrentImage);

                        var progress = progressGetResponse.Eta_relative > 0 ? progressGetResponse.Progress : 1d;

                        var progressApiResponse = new ApiResponse
                        {
                            StableDiffusionApi = Enums.StableDiffusionApi.Automatic1111,
                            ResponseObject = progressGetResponse,
                            Progress = progress
                        };

                        return progressApiResponse;
                    }, progressToken);
                }
                catch (OperationCanceledException)
                {
                    // Set the final response
                    apiResponse = new ApiResponse
                    {
                        StableDiffusionApi = Enums.StableDiffusionApi.Automatic1111,
                        ResponseObject = txt2ImgResponse,
                        Progress = 1f
                    };
                }

                yield return apiResponse;

                if (textToImageTask.IsCompleted || progressToken.IsCancellationRequested)
                {
                    break;
                }

                // Skip current image every other time
                skipCurrentImage = !skipCurrentImage;
            }
        }

        public async Task RefreshResources()
        {
            var client = getClient();

            try
            {
                await Task.WhenAll(Task.Run(async () =>
                {
                    _samplers = await client.Get_samplers_sdapi_v1_samplers_getAsync();
                }),
                Task.Run(async () =>
                {
                    _promptStyles = await client.Get_prompt_styles_sdapi_v1_prompt_styles_getAsync();
                }),
                Task.Run(async () =>
                {
                    var loras = await client.Get_loras_sdapi_v1_loras_getAsync();

                    var lorasString = JsonConvert.SerializeObject(loras);

                    _loras = JsonConvert.DeserializeObject<ICollection<LoraItem>>(lorasString);
                }));
            }
            catch(Exception e)
            {

            }
        }

        private Client getClient()
        {
            var httpClient = _httpClientFactory.CreateClient();

            var baseUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);

            var automaticClient = new Client(baseUrl, httpClient);

            return automaticClient;
        }

        public static StableDiffusionProcessingTxt2Img Txt2ImageRequestFromSettings(Settings settings)
        {
            var request = new StableDiffusionProcessingTxt2Img();

            request.Batch_size = settings.NumOutputs;
            request.Cfg_scale = settings.GuidanceScale;
            request.Restore_faces = settings.EnableGfpgan;
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.Denoising_strength = settings.PromptStrength;
            request.Steps = settings.NumInferenceSteps;
            request.Seed = settings.Seed;
            request.Tiling = settings.Seamless == Enums.OnOff.on;
            request.Hr_scale = settings.UpscaleLevel;
            request.Prompt = settings.Prompt;

            // TODO - Use steps in the UI instead of calculating from a strength value?
            request.Hr_second_pass_steps = (int)(settings.UpscaleStrength * settings.NumInferenceSteps);
            //request.Sampler_name = settings.Sampler.ToString();

            return request;
        }
    }
}
