using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VectorRetrievalSystem.Api.data;
using VectorRetrievalSystem.Api.models;
using VectorRetrievalSystem.Api.services;

namespace VectorRetrievalSystem.Api.controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IDocumentProcessingService _docService;
        private readonly ILLMService _llmService;
        private readonly IVectorDatabaseService _vectorService;

        public DocumentsController(
            AppDbContext context,
            IDocumentProcessingService docService,
            ILLMService llmService,
            IVectorDatabaseService vectorService)
        {
            _context = context;
            _docService = docService;
            _llmService = llmService;
            _vectorService = vectorService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadDocument(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "temp_uploads");
            if (!Directory.Exists(uploadDir))
                Directory.CreateDirectory(uploadDir);

            var tempFilePath = Path.Combine(uploadDir, Guid.NewGuid().ToString() + "_" + file.FileName);
            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var kafkaProducer = HttpContext.RequestServices.GetRequiredService<IKafkaProducerService>();
            
            var jobMessage = new { FilePath = tempFilePath, FileName = file.FileName };
            await kafkaProducer.ProduceAsync("document-jobs", jobMessage);

            return Accepted(new { Message = "Document uploaded successfully and queued for processing." });
        }

        [HttpGet]
        public async Task<IActionResult> GetDocuments()
        {
            var documents = await _context.Documents
                .OrderByDescending(d => d.UploadDate)
                .Select(d => new {
                    d.Id,
                    d.Filename,
                    d.UploadDate,
                    ChunkCount = _context.DocumentChunks.Count(c => c.DocumentId == d.Id)
                })
                .ToListAsync();

            return Ok(documents);
        }
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearDocuments()
        {
            // Clear SQL Server
            var docs = await _context.Documents.ToListAsync();
            _context.Documents.RemoveRange(docs);
            await _context.SaveChangesAsync();

            // Clear Qdrant
            try
            {
                await _vectorService.DeleteCollectionAsync();
            }
            catch { /* Ignore if it doesn't exist */ }

            return Ok(new { message = "History and vectors cleared successfully." });
        }
    }
}
