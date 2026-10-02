using Qdrant.Client;
using Qdrant.Client.Grpc;
using VectorRetrievalSystem.Api.models;

namespace VectorRetrievalSystem.Api.services
{
    public class VectorDatabaseService : IVectorDatabaseService
    {
        private readonly QdrantClient _client;
        private const string CollectionName = "documents";
        private const ulong VectorSize = 768; // nomic-embed-text size

        public VectorDatabaseService(IConfiguration config)
        {
            var host = config["Qdrant:Host"] ?? "vectordb";
            _client = new QdrantClient(host);
        }

        public async Task InitializeCollectionAsync()
        {
            var collections = await _client.ListCollectionsAsync();
            if (!collections.Contains(CollectionName))
            {
                await _client.CreateCollectionAsync(CollectionName, new VectorParams
                {
                    Size = VectorSize,
                    Distance = Distance.Cosine
                });
            }
        }

        public async Task UpsertVectorsAsync(IEnumerable<DocumentChunk> chunks, IEnumerable<float[]> embeddings)
        {
            var points = chunks.Zip(embeddings, (chunk, embedding) => new PointStruct
            {
                Id = new PointId { Uuid = chunk.Id.ToString() },
                Vectors = embedding,
                Payload = {
                    ["documentId"] = chunk.DocumentId.ToString(),
                    ["chunkIndex"] = chunk.ChunkIndex,
                    ["text"] = chunk.Text
                }
            }).ToList();

            if (points.Any())
            {
                await _client.UpsertAsync(CollectionName, points);
            }
        }

        public async Task<List<Guid>> SearchSimilarChunksAsync(float[] queryEmbedding, int topK = 5)
        {
            var searchResult = await _client.SearchAsync(
                CollectionName,
                queryEmbedding,
                limit: (ulong)topK
            );

            return searchResult.Select(r => Guid.Parse(r.Id.Uuid)).ToList();
        }

        public async Task DeleteCollectionAsync()
        {
            await _client.DeleteCollectionAsync(CollectionName);
        }

        public async Task<List<(Guid Id, float Score)>> SearchSimilarChunksWithScoresAsync(float[] queryEmbedding, int topK = 3)
        {
            var searchResult = await _client.SearchAsync(
                CollectionName,
                queryEmbedding,
                limit: (ulong)topK
            );

            return searchResult.Select(r => (Guid.Parse(r.Id.Uuid), r.Score)).ToList();
        }
    }
}
