using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VectorRetrievalSystem.Api.services
{
    public class LLMService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly string _ollamaUrl;
        
        // You can change these to any local model you have pulled in Ollama
        private readonly string _embeddingModel = "nomic-embed-text";
        private readonly string _chatModel = "llama3.2:1b"; 

        public LLMService(IConfiguration config, HttpClient httpClient)
        {
            _httpClient = httpClient;
            _ollamaUrl = config["Ollama:Url"] ?? "http://localhost:11434";
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            var requestBody = new
            {
                model = _embeddingModel,
                prompt = text
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_ollamaUrl}/api/embeddings", content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result?.Embedding ?? Array.Empty<float>();
        }

        public async Task<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts)
        {
            var allEmbeddings = new List<float[]>();
            var textArray = texts.ToArray();
            
            // Batch them in chunks of 20 so we don't block Ollama for 15 minutes straight.
            // This allows Chat requests to slip into the Ollama queue between batches!
            int batchSize = 20;
            for (int i = 0; i < textArray.Length; i += batchSize)
            {
                var batch = textArray.Skip(i).Take(batchSize).ToArray();
                
                var requestBody = new
                {
                    model = _embeddingModel,
                    input = batch
                };

                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_ollamaUrl}/api/embed", content);
                response.EnsureSuccessStatusCode();

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OllamaBatchEmbedResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (result?.Embeddings != null)
                {
                    allEmbeddings.AddRange(result.Embeddings);
                }
            }

            return allEmbeddings;
        }

        public async Task<string> GenerateChatResponseAsync(string systemPrompt, string userPrompt, Func<string, Task<string>>? sqlQueryTool = null)
        {
            // Note: Ollama's Llama 3.1 supports tool calling natively via the /api/chat endpoint, 
            // but to ensure broad compatibility with any free model (e.g. basic llama3, phi3), 
            // we will simulate the behavior or omit it if they don't explicitly ask to run tools.
            // For this implementation, we just do a standard generation based on context.

            var requestBody = new
            {
                model = _chatModel,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                stream = false,
                options = new { num_ctx = 2048 }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_ollamaUrl}/api/chat", content);
            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<OllamaChatResponse>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result?.Message?.Content ?? string.Empty;
        }

        private class OllamaEmbeddingResponse
        {
            public float[]? Embedding { get; set; }
        }

        private class OllamaBatchEmbedResponse
        {
            public List<float[]>? Embeddings { get; set; }
        }

        private class OllamaChatResponse
        {
            public OllamaMessage? Message { get; set; }
        }

        private class OllamaMessage
        {
            public string? Role { get; set; }
            public string? Content { get; set; }
        }
    }
}
