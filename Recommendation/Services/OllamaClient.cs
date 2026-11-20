using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Recommendation.Services
{
    /// <summary>Minimal client for Ollama's /api/chat endpoint.</summary>
    public sealed class OllamaClient
    {
        private readonly HttpClient _http;
        private readonly string _model;

        public OllamaClient(HttpClient http, IConfiguration config)
        {
            _http = http;
            _model = config["Ollama:Model"] ?? "llama3.2";
        }

        /// <summary>
        /// Sends a system + user prompt and returns the model's reply.
        /// format=json makes Ollama constrain the output to valid JSON.
        /// </summary>
        public async Task<string> ChatJsonAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken)
        {
            var request = new ChatRequest(
                Model: _model,
                Messages:
                [
                    new ChatMessage("system", systemPrompt),
                    new ChatMessage("user", userPrompt)
                ],
                Stream: false,
                Format: "json",
                Options: new ChatOptions(Temperature: 0.3));

            using var response = await _http.PostAsJsonAsync(
                "api/chat", request, cancellationToken);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<ChatResponse>(
                cancellationToken: cancellationToken);

            return body?.Message?.Content
                ?? throw new InvalidOperationException("Empty response from Ollama");
        }

        private sealed record ChatRequest(
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
            [property: JsonPropertyName("stream")] bool Stream,
            [property: JsonPropertyName("format")] string Format,
            [property: JsonPropertyName("options")] ChatOptions Options);

        private sealed record ChatMessage(
            [property: JsonPropertyName("role")] string Role,
            [property: JsonPropertyName("content")] string Content);

        private sealed record ChatOptions(
            [property: JsonPropertyName("temperature")] double Temperature);

        private sealed record ChatResponse(
            [property: JsonPropertyName("message")] ChatMessage? Message);
    }
}
