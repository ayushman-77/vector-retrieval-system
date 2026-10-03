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

            // Define tools for the agent
            var tools = new[]
            {
                new {
                    type = "function",
                    function = new {
                        name = "get_database_stats",
                        description = "Use this tool to check how many documents or files are currently uploaded in the system."
                    }
                },
                new {
                    type = "function",
                    function = new {
                        name = "search_documents",
                        description = "Use this tool to search the vector database for facts or information contained in the user's uploaded documents.",
                        parameters = new {
                            type = "object",
                            properties = new { query = new { type = "string", description = "The search query to look for in the documents." } },
                            required = new[] { "query" }
                        }
                    }
                }
            };

            var systemPrompt = @"You are a helpful AI Assistant managing a Vector Retrieval System. 
You MUST use the provided tools to answer user questions when appropriate:
- If the user asks about system stats or uploaded files, use the `get_database_stats` tool.
- If the user asks for specific knowledge, use the `search_documents` tool to query their files.
- If the user is just saying hello or having casual chat, respond naturally WITHOUT using any tools.";

            // Step 1: Agent decides which tool to call
            var initialResponse = await _llmService.GenerateChatResponseAsync(systemPrompt, request.Query, tools);

            string finalResponseText = initialResponse.Content;
            List<string> sources = new();

            // Step 2: Execute tool if the agent requested one
            if (initialResponse.ToolCalls != null && initialResponse.ToolCalls.Any())
            {
                var tool = initialResponse.ToolCalls.First();
                string toolContext = "";

                if (tool.Name == "get_database_stats")
                {
                    int docCount = await _context.Documents.CountAsync();
                    int chunkCount = await _context.DocumentChunks.CountAsync();
                    toolContext = $"Database Stats: There are {docCount} documents uploaded, split into {chunkCount} vector chunks.";
                }
                else if (tool.Name == "search_documents")
                {
                    string searchQuery = request.Query; 
                    if (tool.Arguments.ValueKind == System.Text.Json.JsonValueKind.Object && tool.Arguments.TryGetProperty("query", out var queryProp))
                    {
                        searchQuery = queryProp.GetString() ?? request.Query;
                    }

                    var queryEmbedding = await _llmService.GenerateEmbeddingAsync(searchQuery);
                    await _vectorService.InitializeCollectionAsync();
                    var scoredResults = await _vectorService.SearchSimilarChunksWithScoresAsync(queryEmbedding, topK: 3);
                    
                    var relevantIds = scoredResults.Select(r => r.Id).ToList();
                    var chunks = await _context.DocumentChunks
                        .Include(c => c.Document)
                        .Where(c => relevantIds.Contains(c.Id))
                        .ToListAsync();

                    toolContext = string.Join("\n\n", chunks.Select(c => {
                        var txt = c.Text.Length > 800 ? c.Text.Substring(0, 800) : c.Text;
                        return $"[Source: {c.Document?.Filename}]\n{txt}";
                    }));

                    sources = chunks.Select(c => c.Document?.Filename ?? "").Where(f => !string.IsNullOrEmpty(f)).Distinct().ToList();
                    
                    if (string.IsNullOrWhiteSpace(toolContext))
                        toolContext = "No relevant information was found in the documents.";
                }

                // Step 3: Send tool results back to Agent to generate final answer
                var followupPrompt = $@"You are a helpful AI Assistant. Answer the user's question using the Tool Results below.
If the Tool Results contain the answer, use it and be concise.
If the Tool Results say no information found, tell the user.

Tool Results:
{toolContext}";
                
                var finalResponse = await _llmService.GenerateChatResponseAsync(followupPrompt, request.Query);
                finalResponseText = finalResponse.Content;
            }

            // Log Query
            var log = new QueryLog
            {
                Query = request.Query,
                Response = finalResponseText
            };
            _context.QueryLogs.Add(log);
            await _context.SaveChangesAsync();

            return Ok(new { Response = finalResponseText, Sources = sources });
        }
    }
}
