
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MyFirstMVCApp.Services
{
    public class HuggingFaceEmployeeDescriptionService
        : IEmployeeDescriptionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public HuggingFaceEmployeeDescriptionService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> GenerateDescriptionAsync(
            string department,
            string designation,
            CancellationToken cancellationToken = default)
        {
            var token =
                Environment.GetEnvironmentVariable("HF_TOKEN");

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    "The Hugging Face token is not configured.");
            }

            var model =
                _configuration["HuggingFace:Model"]
                ?? "Qwen/Qwen2.5-3B-Instruct:fastest";

            var prompt =
                "Write a professional employee job description " +
                "in 2 to 4 concise sentences.\n" +
                $"Department: {department}\n" +
                $"Designation: {designation}\n" +
                "Describe typical responsibilities and duties. " +
                "Do not invent company-specific facts. " +
                "Return only the job description.";

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://router.huggingface.co/v1/chat/completions");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            request.Content = JsonContent.Create(new
            {
                model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                max_tokens = 180,
                temperature = 0.7
            });

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);

            var responseBody =
                await response.Content.ReadAsStringAsync(
                    cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Hugging Face returned HTTP " +
                    $"{(int)response.StatusCode}: {responseBody}");
            }

            using var document =
                JsonDocument.Parse(responseBody);

            if (document.RootElement.TryGetProperty(
                    "choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");

                if (message.TryGetProperty(
                        "content", out var content))
                {
                    var description = content.GetString()?.Trim();

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        return description;
                    }
                }
            }

            throw new InvalidOperationException(
                "Hugging Face returned no description.");
        }
    }
}
