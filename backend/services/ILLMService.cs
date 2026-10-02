namespace VectorRetrievalSystem.Api.services
{
    public interface ILLMService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
        Task<List<float[]>> GenerateEmbeddingsAsync(IEnumerable<string> texts);
        Task<string> GenerateChatResponseAsync(string systemPrompt, string userPrompt, Func<string, Task<string>>? sqlQueryTool = null);
    }
}
