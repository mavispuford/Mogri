using MobileDiffusion.Models;
using System.Net.Mime;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using MobileDiffusion.Models.LStein;
using MobileDiffusion.Interfaces.Services;

namespace MobileDiffusion.Services
{
    public class LSteinStableDiffusionService : IStableDiffusionService
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public LSteinStableDiffusionService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task<bool> CheckServer()
        {
            var client = _httpClientFactory.CreateClient();

            var response = await client.GetAsync($"{Constants.BaseUrl}");

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;
        }

        public async IAsyncEnumerable<LSteinResponseItem> SubmitTextToImageRequest(Settings settings)
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

                var url = $"{Constants.BaseUrl}";

                var requestMessage = new HttpRequestMessage
                {
                    RequestUri = new Uri(url),
                    Content = requestBody,
                    Method = HttpMethod.Post,
                };

                return requestMessage;
            });

            var client = _httpClientFactory.CreateClient();

            using (var response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync();

                var reader = new StreamReader(stream);

                string line;

                while ((line = await Task.Run(() => reader.ReadLine())) != null)
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

        public async Task<byte[]> GetImageBytesAsync(LSteinResponseItem responseItem)
        {
            if (string.IsNullOrEmpty(responseItem?.Url))
            {
                throw new ArgumentNullException(nameof(responseItem));
            }

            var client = _httpClientFactory.CreateClient();

            var imageResponse = await client.GetAsync($"{Constants.BaseUrl}/{responseItem.Url.Replace("./", "/")}");

            var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();

            return imageBytes;
        }
    }
}
