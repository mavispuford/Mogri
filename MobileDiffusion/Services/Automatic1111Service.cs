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

        public bool Initialized { get; private set; }

        public Dictionary<string, string> Samplers { get; private set; } = new();

        public Automatic1111Service(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task Initialize()
        {
            try
            {
                await RefreshResources();

                Initialized = true;
            }
            catch
            {
                // Unable to initialize
            }
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

        public async IAsyncEnumerable<ApiResponse> SubmitImageRequest(Settings settings)
        {
            if (settings is null)
            {
                yield break;
            }

            if (string.IsNullOrEmpty(settings.InitImage))
            {
                // No image - Just do txt2img

                await foreach (ApiResponse apiResponse in sendTextToImageRequest(settings))
                {
                    yield return apiResponse;
                }
            }
            else
            {
                // Image included - Do img2img

                await foreach (ApiResponse apiResponse in sendImageToImageRequest(settings))
                {
                    yield return apiResponse;
                }
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

                Samplers.Clear();
                foreach (var sampler in _samplers)
                {
                    Samplers.TryAdd(sampler.Name, sampler.Aliases.FirstOrDefault());
                }
            }
            catch (Exception e)
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

        private static StableDiffusionProcessingTxt2Img txt2ImageRequestFromSettings(Settings settings)
        {
            var request = new StableDiffusionProcessingTxt2Img();

            //request.N_iter = 1; // Number of Batches
            request.Batch_size = settings.NumOutputs;
            request.Cfg_scale = settings.GuidanceScale;
            request.Restore_faces = settings.EnableGfpgan;
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.Denoising_strength = settings.DenoisingStrength;
            request.Steps = settings.Steps;
            request.Seed = settings.Seed;
            request.Tiling = settings.Seamless == Enums.OnOff.on;
            request.Hr_scale = settings.UpscaleLevel;
            request.Prompt = settings.Prompt;
            request.Negative_prompt = settings.NegativePrompt;
            request.Sampler_name = settings.Sampler;

            // TODO - Use steps in the UI instead of calculating from a strength value?
            request.Hr_second_pass_steps = (int)(settings.UpscaleStrength * settings.Steps);

            return request;
        }

        private static StableDiffusionProcessingImg2Img image2ImageRequestFromSettings(Settings settings)
        {
            var request = new StableDiffusionProcessingImg2Img();

            //request.N_iter = 1; // Number of Batches
            request.Batch_size = settings.NumOutputs;
            request.Cfg_scale = settings.GuidanceScale;
            request.Restore_faces = settings.EnableGfpgan;
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.Denoising_strength = settings.DenoisingStrength;
            request.Steps = settings.Steps;
            request.Seed = (int)settings.Seed;
            request.Tiling = settings.Seamless == Enums.OnOff.on;
            request.Prompt = settings.Prompt;
            request.Negative_prompt = settings.NegativePrompt;
            request.Sampler_name = settings.Sampler;
            request.Init_images = new List<object>
            {
                settings.InitImage
            };
            request.Mask = settings.Mask;

            return request;
        }

        private async IAsyncEnumerable<ApiResponse> sendTextToImageRequest(Settings settings)
        {
            var client = getClient();

            var request = txt2ImageRequestFromSettings(settings);

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
            var finished = false;

            while (true)
            {
                try
                {
                    apiResponse = await getCurrentProgress(client, progressToken, skipCurrentImage);
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

                    finished = true;
                }

                yield return apiResponse;

                if (finished)
                {
                    break;
                }

                // Skip current image every other time
                skipCurrentImage = !skipCurrentImage;
            }
        }

        private async IAsyncEnumerable<ApiResponse> sendImageToImageRequest(Settings settings)
        {
            var client = getClient();

            var request = image2ImageRequestFromSettings(settings);

            var cancellationTokenSource = new CancellationTokenSource();
            var progressToken = cancellationTokenSource.Token;

            ImageToImageResponse img2ImgResponse = null;

            var textToImageTask = Task.Run(async () =>
            {
                try
                {
                    img2ImgResponse = await client.Img2imgapi_sdapi_v1_img2img_postAsync(request);
                }
                finally
                {
                    cancellationTokenSource.Cancel();
                }
            });

            ApiResponse apiResponse = null;
            var skipCurrentImage = false;
            var finished = false;

            while (true)
            {
                try
                {
                    apiResponse = await getCurrentProgress(client, progressToken, skipCurrentImage);
                }
                catch (OperationCanceledException)
                {
                    // Set the final response
                    apiResponse = new ApiResponse
                    {
                        StableDiffusionApi = Enums.StableDiffusionApi.Automatic1111,
                        ResponseObject = img2ImgResponse,
                        Progress = 1f
                    };

                    finished = true;
                }

                yield return apiResponse;

                if (finished)
                {
                    break;
                }

                // Skip current image every other time
                skipCurrentImage = !skipCurrentImage;
            }
        }

        private async Task<ApiResponse> getCurrentProgress(Client client, CancellationToken token, bool skipCurrentImage)
        {
            return await Task.Run(async () =>
            {
                await Task.Delay(500, token);

                token.ThrowIfCancellationRequested();

                var progressGetResponse = await client.Progressapi_sdapi_v1_progress_getAsync(true, token);
                //var progressGetResponse = await client.Progressapi_sdapi_v1_progress_getAsync(skipCurrentImage, token);

                var progress = progressGetResponse.Eta_relative > 0 ? progressGetResponse.Progress : 1d;

                var progressApiResponse = new ApiResponse
                {
                    StableDiffusionApi = Enums.StableDiffusionApi.Automatic1111,
                    ResponseObject = progressGetResponse,
                    Progress = progress
                };

                return progressApiResponse;
            });
        }
    }
}
