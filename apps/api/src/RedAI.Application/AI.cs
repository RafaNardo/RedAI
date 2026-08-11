using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace RedAI.Application;
public interface IAIClient { string Mode { get; } Task<T> CompleteAsync<T>(string operation, object input, CancellationToken cancellationToken = default); }
public sealed class MockAIClient : IAIClient { public string Mode => "mock"; public Task<T> CompleteAsync<T>(string operation, object input, CancellationToken cancellationToken = default) => throw new NotSupportedException("Mock outputs are deterministic domain fixtures."); }
public sealed class OpenAIResponsesClient(HttpClient http, IConfiguration configuration) : IAIClient
{
    public string Mode => "openai";
    public async Task<T> CompleteAsync<T>(string operation, object input, CancellationToken cancellationToken = default)
    {
        var key = configuration["ai-api-key"] ?? throw new InvalidOperationException("The server-side user secret 'ai-api-key' is required when AI_MODE=openai.");
        var model = configuration["AI:Models:Reasoning"] ?? throw new InvalidOperationException("AI:Models:Reasoning must be configured when AI_MODE=openai.");
        using var message = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        message.Content = JsonContent.Create(new { model, input = $"Operation: {operation}\nInput: {System.Text.Json.JsonSerializer.Serialize(input)}", text = new { format = new { type = "json_object" } } });
        using var response = await http.SendAsync(message, cancellationToken); response.EnsureSuccessStatusCode();
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var json = body.RootElement.GetProperty("output").EnumerateArray().SelectMany(x => x.GetProperty("content").EnumerateArray()).First(x => x.GetProperty("type").GetString() == "output_text").GetProperty("text").GetString() ?? "{}";
        return System.Text.Json.JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException("OpenAI returned an empty structured output.");
    }
}
