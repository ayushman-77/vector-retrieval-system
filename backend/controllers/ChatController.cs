using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VectorRetrievalSystem.Api.data;
using VectorRetrievalSystem.Api.models;
using VectorRetrievalSystem.Api.services;

namespace VectorRetrievalSystem.Api.controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILLMService _llmService;
        private readonly IVectorDatabaseService _vectorService;

        public ChatController(
            AppDbContext context,
            ILLMService llmService,
            IVectorDatabaseService vectorService)
        {
            _context = context;
            _llmService = llmService;
            _vectorService = vectorService;
        }

        public class ChatRequest { public string Query { get; set; } = string.Empty; }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
                return BadRequest("Query is required.");

            // 1. Generate query embedding
            var queryEmbedding = await _llmService.GenerateEmbeddingAsync(request.Query);

            // 2. Search similar chunks WITH scores so we can judge relevance
            await _vectorService.InitializeCollectionAsync();
            var scoredResults = await _vectorService.SearchSimilarChunksWithScoresAsync(queryEmbedding, topK: 3);

            // 3. Determine if the query is actually related to documents
            //    Cosine similarity: 1.0 = perfect match, 0.0 = completely unrelated
            //    Threshold of 0.35 filters out greetings, casual chat, and off-topic questions
            float relevanceThreshold = 0.35f;
            var relevantResults = scoredResults.Where(r => r.Score >= relevanceThreshold).ToList();
            bool isDocumentRelated = relevantResults.Any();

            string responseText;
            List<string> sources = new();

            if (isDocumentRelated)
            {
                // Fetch text for the relevant chunks from DB
                var relevantIds = relevantResults.Select(r => r.Id).ToList();
                var chunks = await _context.DocumentChunks
                    .Include(c => c.Document)
                    .Where(c => relevantIds.Contains(c.Id))
                    .ToListAsync();

                // Trim each chunk to 800 chars max to keep the prompt lean
                var contextText = string.Join("\n\n", chunks.Select(c => {
                    var txt = c.Text.Length > 800 ? c.Text.Substring(0, 800) : c.Text;
                    return $"[Source: {c.Document?.Filename}]\n{txt}";
                }));

                var systemPrompt = $@"You are a friendly, conversational AI assistant. 
Answer the user's question naturally using ONLY the context below. Be concise.
If the context doesn't have the answer, say you don't have that information.

Context:
{contextText}";

                responseText = await _llmService.GenerateChatResponseAsync(systemPrompt, request.Query);
                sources = chunks.Select(c => c.Document?.Filename ?? "").Where(f => !string.IsNullOrEmpty(f)).Distinct().ToList();
            }
            else
            {
                // No relevant documents — respond naturally as a general assistant
                var systemPrompt = @"You are a friendly, conversational AI assistant. 
Chat naturally with the user. Be warm, helpful, and concise. 
You can have normal conversations — greetings, small talk, general knowledge questions.
If the user asks about something specific that would require uploaded documents, 
gently suggest they upload a relevant document first.";

                responseText = await _llmService.GenerateChatResponseAsync(systemPrompt, request.Query);
                // No sources for casual/unrelated responses
            }

            // Log Query
            var log = new QueryLog
            {
                Query = request.Query,
                Response = responseText
            };
            _context.QueryLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new { Response = responseText, Sources = sources });
        }
    }
}
