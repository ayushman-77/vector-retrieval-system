using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VectorRetrievalSystem.Api.models
{
    public class DocumentChunk
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public string Text { get; set; } = string.Empty;
        
        public int ChunkIndex { get; set; }
        
        public Guid DocumentId { get; set; }
        
        [ForeignKey("DocumentId")]
        public Document? Document { get; set; }
    }
}
