using MobileDiffusion.Clients.Automatic1111;
using MobileDiffusion.Helpers;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;
using MobileDiffusion.Models;
using MobileDiffusion.Models.Automatic1111;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace MobileDiffusion.Services
{
    internal class Automatic1111Service : IStableDiffusionService
    {
        private static class RegexConstants
        {
            public const string Prompt = nameof(Prompt);
            public const string NegativePrompt = nameof(NegativePrompt);
            public const string Steps = nameof(Steps);
            public const string Sampler = nameof(Sampler);
            public const string CfgScale = nameof(CfgScale);
            public const string Seed = nameof(Seed);
            public const string Width = nameof(Width);
            public const string Height = nameof(Height);
            public const string ModelHash = nameof(ModelHash);
            public const string Model = nameof(Model);
            public const string DenoisingStrength = nameof(DenoisingStrength);
            public const string Eta = nameof(Eta);
            public const string Version = nameof(Version);
        }

        private static readonly Regex PngInfoRegex = new(@"^(?<Prompt>.*)(\n)*(Negative\s*prompt:\s*)*(?<NegativePrompt>.*)(\n)*Steps:\s*(?<Steps>\d*),\s*Sampler:\s*(?<Sampler>.*),\s*CFG\s*scale:\s*(?<CfgScale>\d*\.*\d*),\s*Seed:\s*(?<Seed>\d*),\s*Size:\s*(?<Width>\d*)x(?<Height>\d*),\s*Model\s*hash:\s*(?<ModelHash>.*),\s*Model:\s*(?<Model>.*),\s*Denoising\s*strength:\s(?<DenoisingStrength>\d*\.*\d*),\s*(Eta:\s)*(?<Eta>\d*\.*\d*)*,*\s*Version:\s*(?<Version>v\d*\.*\d*\.*\d*)$", RegexOptions.Compiled);
        //private static readonly Regex PngInfoRegex = new(@"(?<Prompt>.*)\n", RegexOptions.Compiled);

        /// <summary>
        ///     Some operations take several minutes to complete, especially with older hardware.
        /// </summary>
        private const int RequestTimeoutMinutes = 15;

        private readonly IHttpClientFactory _httpClientFactory;

        private ICollection<SamplerItem> _samplers;
        private ICollection<PromptStyleItem> _promptStyles;
        private ICollection<LoraItem> _loras;

        private Task _initializeTask;
        private CancellationTokenSource _mainRequestCancellationSource;

        public bool Initialized { get; private set; }

        public Automatic1111Service(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task InitializeAsync()
        {
            if (_initializeTask == null || _initializeTask.Status != TaskStatus.Running)
            { 
                _initializeTask = RefreshResourcesAsync();
            }

            await _initializeTask;

            _initializeTask = null;

            Initialized = true;
        }

        public async Task<bool> CheckServerAsync()
        {
            var client = getClient(TimeSpan.FromSeconds(5));

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

        public async IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(Settings settings)
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

        public async Task<Settings> GetImageInfoAsync(string base64EncodedImage)
        {
            var client = getClient(TimeSpan.FromSeconds(10));

            var request = new PNGInfoRequest()
            {
                Image = base64EncodedImage
            };

            var imageInfoResult = await client.Pnginfoapi_sdapi_v1_png_info_postAsync(request);

            if (!string.IsNullOrEmpty(imageInfoResult.Info) && PngInfoRegex.Match(imageInfoResult.Info) is { Success: true } match)
            {
                var settings = new Settings
                {
                    Prompt = match.Groups[RegexConstants.Prompt].Value,
                    NegativePrompt = match.Groups[RegexConstants.NegativePrompt].Value,
                    Steps = int.Parse(match.Groups[RegexConstants.Steps].Value),
                    Sampler = match.Groups[RegexConstants.Sampler].Value,
                    GuidanceScale = double.Parse(match.Groups[RegexConstants.CfgScale].Value),
                    Seed = long.Parse(match.Groups[RegexConstants.Seed].Value),
                    Width = double.Parse(match.Groups[RegexConstants.Width].Value),
                    Height = double.Parse(match.Groups[RegexConstants.Height].Value),
                    DenoisingStrength = double.Parse(match.Groups[RegexConstants.DenoisingStrength].Value),
                };

                return settings;
            }

            return null;
        }

        public async Task RefreshResourcesAsync()
        {
            var client = getClient(TimeSpan.FromSeconds(10));

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

        private Client getClient(TimeSpan? timeout = null)
        {
            var httpClient = _httpClientFactory.CreateClient();

            if (timeout != null)
            {
                httpClient.Timeout = timeout.Value;
            }

            var baseUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);

            var automaticClient = new Client(baseUrl, httpClient);

            return automaticClient;
        }

        private static StableDiffusionProcessingTxt2Img txt2ImageRequestFromSettings(Settings settings)
        {
            var request = new StableDiffusionProcessingTxt2Img();

            request.N_iter = settings.NumOutputs; // Number of Batches
            //request.Batch_size = settings.NumOutputs;
            request.Cfg_scale = settings.GuidanceScale;
            request.Restore_faces = settings.EnableGfpgan;
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.Denoising_strength = settings.DenoisingStrength;
            request.Steps = settings.Steps;
            request.Seed = settings.Seed;
            request.Tiling = settings.Seamless == Enums.OnOff.on;
            request.Hr_scale = settings.UpscaleLevel;

            var combinedPromptAndStyles = settings.GetCombinedPromptAndPromptStyles();
            request.Prompt = combinedPromptAndStyles.Prompt;
            request.Negative_prompt = combinedPromptAndStyles.NegativePrompt;

            request.Sampler_name = settings.Sampler;

            // TODO - Use steps in the UI instead of calculating from a strength value?
            request.Hr_second_pass_steps = (int)(settings.UpscaleStrength * settings.Steps);

            return request;
        }

        private static StableDiffusionProcessingImg2Img image2ImageRequestFromSettings(Settings settings)
        {
            var request = new StableDiffusionProcessingImg2Img();

            request.N_iter = settings.NumOutputs; // Number of Batches
            //request.Batch_size = settings.NumOutputs;
            request.Cfg_scale = settings.GuidanceScale;
            request.Restore_faces = settings.EnableGfpgan;
            request.Width = (int)settings.Width;
            request.Height = (int)settings.Height;
            request.Denoising_strength = settings.DenoisingStrength;
            request.Steps = settings.Steps;
            request.Seed = (int)settings.Seed;
            request.Tiling = settings.Seamless == Enums.OnOff.on;

            var combinedPromptAndStyles = settings.GetCombinedPromptAndPromptStyles();
            request.Prompt = combinedPromptAndStyles.Prompt;
            request.Negative_prompt = combinedPromptAndStyles.NegativePrompt;

            request.Sampler_name = settings.Sampler;
            request.Init_images = new List<object>
            {
                settings.InitImage
            };
            request.Mask = settings.Mask;
            request.Inpainting_fill = 1;

            // Because we colorize the image, blurring the mask would cause some of the colorized pixels to stay
            request.Mask_blur = 0;
            request.Mask_blur_x = 0;
            request.Mask_blur_y = 0;

            return request;
        }

        private async IAsyncEnumerable<ApiResponse> sendTextToImageRequest(Settings settings)
        {
            var client = getClient(TimeSpan.FromMinutes(RequestTimeoutMinutes));

            var request = txt2ImageRequestFromSettings(settings);

            if (_mainRequestCancellationSource?.IsCancellationRequested ?? false)
            {
                _mainRequestCancellationSource.Cancel();
            }

            _mainRequestCancellationSource = new CancellationTokenSource();
            var progressCancellationTokenSource = new CancellationTokenSource();
            var progressToken = progressCancellationTokenSource.Token;

            TextToImageResponse txt2ImgResponse = null;

            var textToImageTask = Task.Run(async () =>
            {
                try
                {
                    txt2ImgResponse = await client.Text2imgapi_sdapi_v1_txt2img_postAsync(request, _mainRequestCancellationSource?.Token ?? CancellationToken.None);
                }
                finally
                {
                    _mainRequestCancellationSource = null;
                    progressCancellationTokenSource.Cancel();
                }
            });

            ApiResponse apiResponse = null;
            var skipCurrentImage = false;
            var finished = false;

            while (!finished)
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

                // Skip current image every other time
                skipCurrentImage = !skipCurrentImage;
            }
        }

        private async IAsyncEnumerable<ApiResponse> sendImageToImageRequest(Settings settings)
        {
            var client = getClient(TimeSpan.FromMinutes(RequestTimeoutMinutes));

            var request = image2ImageRequestFromSettings(settings);

            if (_mainRequestCancellationSource?.IsCancellationRequested ?? false)
            {
                _mainRequestCancellationSource.Cancel();
            }

            _mainRequestCancellationSource = new CancellationTokenSource();
            var progressCancellationTokenSource = new CancellationTokenSource();
            var progressToken = progressCancellationTokenSource.Token;

            ImageToImageResponse img2ImgResponse = null;

            var textToImageTask = Task.Run(async () =>
            {
                try
                {
                    img2ImgResponse = await client.Img2imgapi_sdapi_v1_img2img_postAsync(request, _mainRequestCancellationSource?.Token ?? CancellationToken.None);
                }
                finally
                {
                    _mainRequestCancellationSource = null;
                    progressCancellationTokenSource.Cancel();
                }
            });

            ApiResponse apiResponse = null;
            var skipCurrentImage = false;
            var finished = false;

            while (!finished)
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

        public Task<Dictionary<string, string>> GetSamplersAsync()
        {
            var result = new Dictionary<string, string>();

            if (_samplers == null)
            {
                return Task.FromResult(result);
            }

            foreach (var sampler in _samplers)
            {
                result.TryAdd(sampler.Name, sampler.Aliases.FirstOrDefault());
            }

            return Task.FromResult(result);
        }

        public Task<List<IPromptStyleViewModel>> GetPromptStylesAsync()
        {
            var result = new List<IPromptStyleViewModel>();

            if (_promptStyles == null)
            {
                return Task.FromResult(result);
            }

            foreach (var item in _promptStyles)
            {
                result.Add(new PromptStyleViewModel
                {
                    Name = item.Name,
                    Prompt = item.Prompt,
                    NegativePrompt = item.Negative_prompt
                });
            }

            return Task.FromResult(result);
        }
    }
}
