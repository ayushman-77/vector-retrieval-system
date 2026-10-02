using UglyToad.PdfPig;

namespace VectorRetrievalSystem.Api.services
{
    public class DocumentProcessingService : IDocumentProcessingService
    {
        public async Task<string> ExtractTextAsync(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            using var stream = file.OpenReadStream();
            
            if (extension == ".txt")
            {
                using var reader = new StreamReader(stream);
                return await reader.ReadToEndAsync();
            }
            else if (extension == ".pdf")
            {
                using var document = PdfDocument.Open(stream);
                var text = new System.Text.StringBuilder();
                foreach (var page in document.GetPages())
                {
                    text.Append(page.Text);
                    text.Append(" ");
                }
                return text.ToString();
            }
            
            throw new NotSupportedException($"File extension {extension} is not supported.");
        }

        public List<string> ChunkText(string text, int chunkSize = 1000, int overlap = 100)
        {
            var words = text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var chunks = new List<string>();
            
            int i = 0;
            while (i < words.Length)
            {
                var chunkWords = words.Skip(i).Take(chunkSize);
                var chunkText = string.Join(" ", chunkWords);
                
                // Extremely long words/data dumps can exceed token limits, so we enforce a hard character limit
                // 4000 chars is safely under the 2048 token limit of Ollama embeddings.
                if (chunkText.Length > 4000) 
                {
                    chunkText = chunkText.Substring(0, 4000);
                }
                
                chunks.Add(chunkText);
                i += chunkSize - overlap;
            }
            
            return chunks;
        }
    }
}
