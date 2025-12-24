using MobileDiffusion.Models;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using MobileDiffusion.Models.LStein;
using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Interfaces.ViewModels;

namespace MobileDiffusion.Services
{
    public class LSteinStableDiffusionService : IImageGenerationService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public bool Initialized { get; private set; }

        public List<PromptStyleViewModel> PromptStyles { get; private set; } = new();

        public LSteinStableDiffusionService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public Task InitializeAsync()
        {
            Initialized = true;

            return Task.CompletedTask;
        }

        public async Task<bool> CheckServerAsync()
        {
            var client = _httpClientFactory.CreateClient();

            var baseUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);

            var response = await client.GetAsync($"{baseUrl}");

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;
        }

        public async IAsyncEnumerable<ApiResponse> SubmitImageRequestAsync(PromptSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            // Because there are images in the request, this can hang the UI for a second or two
            var requestMessage = await Task.Run(() =>
            {
                var convertedRequest = LSteinRequest.FromSettings(settings);
                var stringRequest = JsonSerializer.Serialize(convertedRequest);

                var requestBody = new StringContent(
                    stringRequest,
                    Encoding.UTF8,
                    MediaTypeNames.Application.Json);

                var baseUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);

                var url = $"{baseUrl}";

                var requestMessage = new HttpRequestMessage
                {
                    RequestUri = new Uri(url),
                    Content = requestBody,
                    Method = HttpMethod.Post,
                };

                return requestMessage;
            });

            var apiResponse = new ApiResponse
            {
                StableDiffusionApi = Enums.StableDiffusionApi.InvokeAI
            };

            await foreach (var item in submitTextToImageRequestToInvokeAi(requestMessage))
            {
                apiResponse.ResponseObject = item;

                yield return apiResponse;
            }
        }

        private async IAsyncEnumerable<LSteinResponseItem> submitTextToImageRequestToInvokeAi(HttpRequestMessage requestMessage)
        {
            var client = _httpClientFactory.CreateClient();

            using (var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync();

                var reader = new StreamReader(stream);

                string line;

                while ((line = await Task.Run(reader.ReadLine)) != null)
                {
                    var responseItem = JsonSerializer.Deserialize<LSteinResponseItem>(line);

                    if (responseItem == null)
                    {
                        continue;
                    }

                    yield return responseItem;
                }

            }
        }

        public async Task<byte[]> GetImageBytesAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            var client = _httpClientFactory.CreateClient();

            var baseUrl = Preferences.Default.Get(Constants.PreferenceKeys.ServerUrl, string.Empty);

            var imageResponse = await client.GetAsync($"{baseUrl}/{url.Replace("./", "/")}");

            var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();

            return imageBytes;
        }

        public Task RefreshResourcesAsync()
        {
            // Not implemented...

            return Task.CompletedTask;
        }

        public Task<Dictionary<string, string>> GetSamplersAsync()
        {
            var result = new Dictionary<string, string>() 
            {
                { "k_lms",""},
                { "ddim",""},
                { "plms",""},
                { "k_dpm_2",""},
                { "k_dpm_2_a",""},
                { "k_euler",""},
                { "k_euler_a",""},
                { "k_heun",""}
            };

            return Task.FromResult(result);
        }

        public Task<List<IPromptStyleViewModel>> GetPromptStylesAsync()
        {
            return Task.FromResult(new List<IPromptStyleViewModel>());
        }

        public Task<PromptSettings> GetImageInfoAsync(string base64EncodedImage)
        {
            throw new NotImplementedException();
        }

        public Task<List<ILoraViewModel>> GetLorasAsync()
        {
            throw new NotImplementedException();
        }

        public Task<List<IUpscalerViewModel>> GetUpscalersAsync()
        {
            throw new NotImplementedException();
        }

        public Task SaveSettingsAsync(PromptSettings settings)
        {
            throw new NotImplementedException();
        }

        public Task<List<IModelViewModel>> GetModelsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<IModelViewModel> GetSelectedModelAsync()
        {
            throw new NotImplementedException();
        }
    }
}
