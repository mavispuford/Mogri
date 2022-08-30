using MobileDiffusion.Interfaces.Services;
using MobileDiffusion.Models;
using System.Text;
using System.Text.Json;
using MediaTypeNames = System.Net.Mime.MediaTypeNames;

namespace MobileDiffusion.Services
{
    public class StableDiffusionService : IStableDiffusionService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public StableDiffusionService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task<bool> CheckServer()
        {
            var client = _httpClientFactory.CreateClient();

            var response = await client.GetAsync($"{Constants.BaseUrl}/ping");

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;
        }

        public async Task<IEnumerable<string>> SubmitTextToImageRequest(TextToImageRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var client = _httpClientFactory.CreateClient();

            var requestBody = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                MediaTypeNames.Application.Json);

            var response = await client.PostAsync($"{Constants.BaseUrl}/image", requestBody);

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();

                var textToImageResponse = JsonSerializer.Deserialize<TextToImageResponse>(responseString);

                return textToImageResponse.Output;
            }

            return Enumerable.Empty<string>();
        }
    }
}
