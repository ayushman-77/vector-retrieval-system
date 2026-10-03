using System.Text.Json;

namespace VectorRetrievalSystem.Api.services
{
    public interface ILLMService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
        Task<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts);
        Task<ChatResult> GenerateChatResponseAsync(string systemPrompt, string userPrompt, object? tools = null);
    }

    public class ChatResult
    {
        public string Content { get; set; } = string.Empty;
        public List<ToolCall> ToolCalls { get; set; } = new();
    }

    public class ToolCall
    {
        public string Name { get; set; } = string.Empty;
        public JsonElement Arguments { get; set; }
    }
}
