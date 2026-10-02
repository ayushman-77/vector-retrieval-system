using System.Text.Json;
using Confluent.Kafka;
using VectorRetrievalSystem.Api.data;
using VectorRetrievalSystem.Api.models;
using VectorRetrievalSystem.Api.services;

namespace VectorRetrievalSystem.Api.workers
{
    public class DocumentJobMessage
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }

    public class DocumentProcessingWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _config;
        private readonly ILogger<DocumentProcessingWorker> _logger;

        public DocumentProcessingWorker(IServiceProvider serviceProvider, IConfiguration config, ILogger<DocumentProcessingWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Yield immediately so that the .NET WebHost can finish starting up.
            // Without this, the synchronous consumer.Consume() loop blocks Kestrel from ever starting!
            await Task.Yield();

            var config = new ConsumerConfig
            {
                BootstrapServers = _config["Kafka:BootstrapServers"] ?? "localhost:29092",
                GroupId = "document-processing-group",
                AutoOffsetReset = AutoOffsetReset.Earliest
            };

            using var consumer = new ConsumerBuilder<Null, string>(config).Build();
            consumer.Subscribe("document-jobs");

            _logger.LogInformation("Kafka DocumentProcessingWorker started.");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = consumer.Consume(stoppingToken);
                        if (consumeResult != null)
                        {
                            var message = JsonSerializer.Deserialize<DocumentJobMessage>(consumeResult.Message.Value);
                            if (message != null)
                            {
                                await ProcessDocumentAsync(message);
                            }
                        }
                    }
                    catch (ConsumeException e)
                    {
                        _logger.LogError($"Kafka consume error: {e.Error.Reason}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                consumer.Close();
            }
        }

        private async Task ProcessDocumentAsync(DocumentJobMessage message)
        {
            _logger.LogInformation($"Processing document: {message.FileName}");

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vectorService = scope.ServiceProvider.GetRequiredService<IVectorDatabaseService>();
            var llmService = scope.ServiceProvider.GetRequiredService<ILLMService>();
            var docService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();

            try
            {
                // We need an IFormFile equivalent to pass to ExtractTextAsync.
                // We will create a physical file stream and wrap it in a FormFile.
                using var stream = new FileStream(message.FilePath, FileMode.Open, FileAccess.Read);
                var formFile = new FormFile(stream, 0, stream.Length, "file", message.FileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "application/pdf"
                };

                var text = await docService.ExtractTextAsync(formFile);
                var chunks = docService.ChunkText(text);

                var document = new Document
                {
                    Filename = message.FileName,
                    UploadDate = DateTime.UtcNow
                };

                dbContext.Documents.Add(document);
                await dbContext.SaveChangesAsync();

                var documentChunks = new List<DocumentChunk>();
                for (int i = 0; i < chunks.Count; i++)
                {
                    documentChunks.Add(new DocumentChunk
                    {
                        Text = chunks[i],
                        ChunkIndex = i,
                        DocumentId = document.Id
                    });
                }

                dbContext.DocumentChunks.AddRange(documentChunks);
                await dbContext.SaveChangesAsync();

                var embeddings = await llmService.GenerateEmbeddingsAsync(chunks);

                await vectorService.InitializeCollectionAsync();
                await vectorService.UpsertVectorsAsync(documentChunks, embeddings);

                _logger.LogInformation($"Successfully processed document: {message.FileName}");
                
                // Cleanup file
                stream.Close();
                File.Delete(message.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to process document {message.FileName}");
            }
        }
    }
}
