using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MyFirstMVCApp.Services
{
    public interface IEmployeeDescriptionService
    {
        Task<string> GenerateDescriptionAsync(
            string department,
            string designation,
            CancellationToken cancellationToken = default);
    }

    public class OpenAiEmployeeDescriptionService
        : IEmployeeDescriptionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public OpenAiEmployeeDescriptionService(
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
            // Read the API key from the server environment
            var apiKey =
                Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? _configuration["OpenAI:ApiKey"];

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "The OpenAI API key is not configured.");
            }

            // The model is configurable
            var model = _configuration["OpenAI:Model"]
                ?? "gpt-4.1-mini";

            var instructions =
                """
                You are an HR writing assistant.
                Write a professional employee job description using
                only the supplied department and designation.
                Write 2 to 4 concise sentences.
                Describe typical responsibilities and duties.
                Do not invent company-specific policies, qualifications,
                personal details, or facts that were not provided.
                Return only the job description, without a heading.
                """;

            var input =
                $"Department: {department}\n" +
                $"Designation: {designation}\n\n" +
                "Generate a professional employee job description.";

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/responses");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            request.Content = JsonContent.Create(new
            {
                model,
                instructions,
                input,
                max_output_tokens = 180
            });

            using var response = await _httpClient.SendAsync(
                request,
                cancellationToken);


            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(
                    cancellationToken);

                throw new HttpRequestException(
                    $"OpenAI API returned HTTP {(int)response.StatusCode}: " +
                    errorBody);
            }


            using var responseStream =
                await response.Content.ReadAsStreamAsync(
                    cancellationToken);

            using var document = await JsonDocument.ParseAsync(
                responseStream,
                cancellationToken: cancellationToken);

            // Extract generated text from the Responses API result
            if (document.RootElement.TryGetProperty(
                    "output", out var output))
            {
                foreach (var item in output.EnumerateArray())
                {
                    if (!item.TryGetProperty(
                            "content", out var content))
                    {
                        continue;
                    }

                    foreach (var part in content.EnumerateArray())
                    {
                        if (part.TryGetProperty(
                                "type", out var type) &&
                            type.GetString() == "output_text" &&
                            part.TryGetProperty(
                                "text", out var text))
                        {
                            var result = text.GetString()?.Trim();

                            if (!string.IsNullOrWhiteSpace(result))
                            {
                                return result;
                            }
                        }
                    }
                }
            }

            throw new InvalidOperationException(
                "The AI service returned no description.");
        }
    }
}
