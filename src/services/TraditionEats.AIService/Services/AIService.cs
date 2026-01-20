using TraditionEats.BuildingBlocks.Redis;
using System.Text.Json;
using System.Text;

namespace TraditionEats.AIService.Services;

public interface IAIService
{
    Task<string> GetRecommendationAsync(Guid userId, string context);
    Task<string> AnswerQuestionAsync(string question, string? context = null);
    Task<string> GenerateDescriptionAsync(string itemName, string? category = null);
    Task<bool> DetectFraudAsync(Guid orderId, decimal amount, Dictionary<string, object>? metadata = null);
}

public class AIService : IAIService
{
    private readonly IRedisService _redis;
    private readonly ILogger<AIService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public AIService(
        IRedisService redis,
        ILogger<AIService> logger,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _redis = redis;
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<string> GetRecommendationAsync(Guid userId, string context)
    {
        // Check cache first
        var cacheKey = $"ai:recommendation:{userId}:{context.GetHashCode()}";
        var cached = await _redis.GetAsync<string>(cacheKey);
        if (cached != null)
        {
            return cached;
        }

        try
        {
            var ollamaUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            var prompt = $"Based on the user's context: {context}, recommend traditional food items they might like. Be concise and helpful.";

            var requestBody = new
            {
                model = "llama2",
                prompt = prompt,
                stream = false
            };

            var response = await _httpClient.PostAsync(
                $"{ollamaUrl}/api/generate",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaResponse>(content);
                var recommendation = result?.Response ?? "I recommend exploring our traditional dishes section.";

                // Cache for 1 hour
                await _redis.SetAsync(cacheKey, recommendation, TimeSpan.FromHours(1));

                return recommendation;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get AI recommendation");
        }

        return "I recommend exploring our traditional dishes section.";
    }

    public async Task<string> AnswerQuestionAsync(string question, string? context = null)
    {
        try
        {
            var ollamaUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            var prompt = context != null
                ? $"Context: {context}\n\nQuestion: {question}\n\nAnswer:"
                : $"Question: {question}\n\nAnswer:";

            var requestBody = new
            {
                model = "llama2",
                prompt = prompt,
                stream = false
            };

            var response = await _httpClient.PostAsync(
                $"{ollamaUrl}/api/generate",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaResponse>(content);
                return result?.Response ?? "I'm sorry, I couldn't process that question.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to answer question");
        }

        return "I'm sorry, I couldn't process that question at the moment.";
    }

    public async Task<string> GenerateDescriptionAsync(string itemName, string? category = null)
    {
        try
        {
            var ollamaUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            var prompt = $"Generate a brief, appetizing description for a traditional food item called '{itemName}'" +
                (category != null ? $" in the {category} category" : "") + ". Keep it under 100 words.";

            var requestBody = new
            {
                model = "llama2",
                prompt = prompt,
                stream = false
            };

            var response = await _httpClient.PostAsync(
                $"{ollamaUrl}/api/generate",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaResponse>(content);
                return result?.Response ?? $"{itemName} - A delicious traditional dish.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate description");
        }

        return $"{itemName} - A delicious traditional dish.";
    }

    public async Task<bool> DetectFraudAsync(Guid orderId, decimal amount, Dictionary<string, object>? metadata = null)
    {
        // Simple fraud detection logic
        // In production, this would use ML models or external fraud detection services

        // Check for suspicious patterns
        var suspicious = false;

        // Check amount threshold
        if (amount > 1000)
        {
            suspicious = true;
            _logger.LogWarning("High-value order detected: {OrderId}, Amount: {Amount}", orderId, amount);
        }

        // Check for rapid repeated orders (would need order history)
        // This is a placeholder

        // Store fraud check result
        await _redis.SetAsync($"fraud:check:{orderId}", suspicious, TimeSpan.FromHours(24));

        return suspicious;
    }
}

internal class OllamaResponse
{
    public string? Response { get; set; }
}
