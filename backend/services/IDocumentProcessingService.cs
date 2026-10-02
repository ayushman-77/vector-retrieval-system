using Microsoft.AspNetCore.Http;

namespace VectorRetrievalSystem.Api.services
{
    public interface IDocumentProcessingService
    {
        Task<string> ExtractTextAsync(IFormFile file);
        List<string> ChunkText(string text, int chunkSize = 500, int overlap = 50);
    }
}
