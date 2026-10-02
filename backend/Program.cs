using Microsoft.EntityFrameworkCore;
using VectorRetrievalSystem.Api.data;
using VectorRetrievalSystem.Api.services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Entity Framework Core with SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Dependency Injection for Services
builder.Services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
builder.Services.AddHttpClient<ILLMService, LLMService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10); // Local LLMs can be slow on CPU
});
builder.Services.AddScoped<IVectorDatabaseService, VectorDatabaseService>();
builder.Services.AddSingleton<IKafkaProducerService, KafkaProducerService>();
builder.Services.AddHostedService<VectorRetrievalSystem.Api.workers.DocumentProcessingWorker>();

// Configure CORS for Angular Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

// app.UseHttpsRedirection(); // Removed for simplicity in local docker

app.UseAuthorization();

app.MapControllers();

// Apply migrations automatically on startup with retry logic
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var maxRetries = 20; // Increased retries since SQL Server can take >1min to start in Docker
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            dbContext.Database.EnsureCreated(); // Ensure DB and schema are created
            break;
        }
        catch (Exception ex)
        {
            if (i == maxRetries - 1) throw;
            Console.WriteLine($"SQL Server not ready yet. Retrying in 5 seconds... ({ex.Message})");
            Thread.Sleep(5000);
        }
    }
}

app.Run();
