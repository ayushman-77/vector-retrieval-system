using System.ComponentModel.DataAnnotations;

namespace VectorRetrievalSystem.Api.models
{
    public class QueryLog
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [Required]
        public string Query { get; set; } = string.Empty;
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public string? Response { get; set; }
    }
}
