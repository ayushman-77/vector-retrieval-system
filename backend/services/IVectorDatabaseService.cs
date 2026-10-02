using VectorRetrievalSystem.Api.models;

namespace VectorRetrievalSystem.Api.services
{
    public interface IVectorDatabaseService
    {
        Task InitializeCollectionAsync();
        Task UpsertVectorsAsync(IEnumerable<DocumentChunk> chunks, IEnumerable<float[]> embeddings);
        Task<List<Guid>> SearchSimilarChunksAsync(float[] queryEmbedding, int topK = 5);
        Task<List<(Guid Id, float Score)>> SearchSimilarChunksWithScoresAsync(float[] queryEmbedding, int topK = 3);
        Task DeleteCollectionAsync();
    }
}
