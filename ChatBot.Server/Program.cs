using ChatBot.Server.Data;
using ChatBot.Server.Hubs;
using ChatBot.Server.Services;
using ChatBot.Share.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OllamaSharp;

var builder = WebApplication.CreateBuilder(args);

// ── Database ───────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ChatbotDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Ollama / AI ────────────────────────────────────────────────────────────
var ollamaEndpoint = new Uri(
    builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");

var chatModelName = builder.Configuration["Ollama:ChatModel"] ?? "llama3.2:3b";
var embedModelName = builder.Configuration["Ollama:EmbedModel"] ?? "mxbai-embed-large:latest";

builder.Services.AddSingleton<IChatClient>(_ =>
    new OllamaApiClient(ollamaEndpoint, chatModelName));

builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
    new OllamaApiClient(ollamaEndpoint, embedModelName));

// ── Application Services ───────────────────────────────────────────────────
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<VectorSearchService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<RagService>();
builder.Services.AddScoped<BM25Service>();
builder.Services.AddScoped<HybridSearchService>();

// ── SignalR ────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 1024 * 1024;
});

// ── Controllers + CORS ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5105")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ── Migrate DB on startup ──────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatbotDbContext>();
    await db.Database.MigrateAsync();
}

// ── Middleware ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>(HubRoutes.Chat);

app.Run();