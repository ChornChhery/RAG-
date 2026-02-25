# ChatBotRAG 🤖

A Retrieval-Augmented Generation (RAG) Chatbot built with .NET 10, Blazor WebAssembly, SignalR, and local Ollama models. Upload documents and chat with them in real time.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Frontend | Blazor WebAssembly (.NET 10) |
| Backend | ASP.NET Core Web API (.NET 10) |
| Real-time | SignalR |
| Database | SQL Server / LocalDB |
| ORM | Entity Framework Core 10 |
| AI Chat | Ollama (llama3.2:3b / qwen3) |
| AI Embedding | Ollama (mxbai-embed-large) |
| AI Client | OllamaSharp + Microsoft.Extensions.AI |
| PDF Parsing | PdfPig |
| Markdown | Markdig |
| API Docs | Swashbuckle (Swagger) |

---

## Project Structure
```
ChatBotRAG/
├── ChatBot.Share/                   # Shared DTOs, Enums, Constants
│   ├── Constants/
│   │   ├── HubMethods.cs            # SignalR method name constants
│   │   └── HubRoutes.cs             # SignalR hub URL constant
│   ├── DTOs/
│   │   ├── ChatMessageDto.cs        # Single chat message model
│   │   ├── ChatRequest.cs           # User question + history
│   │   ├── DocumentChunkResult.cs   # Retrieved RAG chunk with score
│   │   ├── DocumentDto.cs           # Document metadata for UI
│   │   ├── StreamToken.cs           # Single streamed token
│   │   └── UploadResponse.cs        # Upload result response
│   └── Enums/
│       ├── DocumentStatus.cs        # Uploading/Processing/Ready/Failed
│       └── MessageRole.cs           # User/Assistant/System
│
├── ChatBot.Server/                  # ASP.NET Core Backend
│   ├── Controllers/
│   │   └── DocumentsController.cs   # Upload, list, delete API
│   ├── Data/
│   │   └── ChatbotDbContext.cs      # EF Core DbContext
│   ├── Hubs/
│   │   └── ChatHub.cs               # SignalR streaming hub
│   ├── Models/
│   │   ├── Document.cs              # EF Core document entity
│   │   └── DocumentChunk.cs         # EF Core chunk + vector entity
│   ├── Services/
│   │   ├── DocumentService.cs       # Document CRUD
│   │   ├── EmbeddingService.cs      # Chunking + Ollama embedding
│   │   ├── RagService.cs            # RAG pipeline orchestration
│   │   └── VectorSearchService.cs   # Cosine similarity search
│   ├── Program.cs
│   └── appsettings.json
│
└── ChatBot.Client/                  # Blazor WebAssembly Frontend
    ├── Pages/
    │   ├── Chat.razor               # Chat conversation UI
    │   ├── Chat.razor.cs            # SignalR + message state
    │   ├── Chat.razor.css           # Chat styling
    │   ├── Rag.razor                # Document upload UI
    │   ├── Rag.razor.cs             # Upload + delete logic
    │   └── Rag.razor.css            # RAG page styling
    └── Program.cs
```

---

## Prerequisites

Before running this project make sure you have the following installed:

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) or [SQL Server LocalDB](https://aka.ms/sqllocaldb)
- [Ollama](https://ollama.com/download)
- [VS Code](https://code.visualstudio.com) with the **C# Dev Kit** extension

### Required Ollama Models

Pull these models before running the project:
```bash
ollama pull mxbai-embed-large
ollama pull llama3.2:3b
```

---

## Setup Guide

### 1. Clone or Download the Project
```bash
git clone <your-repo-url>
cd ChatBotRAG
```

### 2. Install EF Core Tools
```bash
dotnet tool install --global dotnet-ef
```

Close and reopen your terminal after this so the PATH refreshes.

### 3. Restore NuGet Packages
```bash
dotnet restore
```

### 4. Configure the Connection String

Open `ChatBot.Server/appsettings.json` and update the connection string to match your SQL Server:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ChatBotRag;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "ChatModel": "llama3.2:3b",
    "EmbedModel": "mxbai-embed-large:latest"
  }
}
```

Common connection string formats:

| SQL Server Type | Server Value |
|---|---|
| LocalDB | `Server=(localdb)\\MSSQLLocalDB` |
| SQL Express | `Server=.\\SQLEXPRESS` |
| Full SQL Server | `Server=localhost` |

### 5. Run Database Migrations
```bash
cd ChatBot.Server
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
dotnet ef database update
```

This creates the `Documents` and `DocumentChunks` tables automatically.

### 6. Start Ollama

Open a separate terminal and run:
```bash
ollama serve
```

Leave this terminal running in the background.

---

## Running the Project

Always start in this order:

**Terminal 1 — Start Ollama (if not already running):**
```bash
ollama serve
```

**Terminal 2 — Start the Server:**
```bash
cd ChatBot.Server
dotnet run
```

Wait until you see:
```
Now listening on: http://localhost:5087
```

**Terminal 3 — Start the Client:**
```bash
cd ChatBot.Client
dotnet run
```

Wait until you see:
```
Now listening on: http://localhost:5105
```

**Then open your browser to:**
```
http://localhost:5105
```

---

## How to Use

### Upload Documents (RAG Page)

1. Go to `http://localhost:5105/rag`
2. Drag and drop or click to upload a PDF, TXT, or MD file
3. Wait for the status to change from ⚙️ Processing to ✅ Ready
4. The document is now embedded and ready for chat

### Chat with Documents (Chat Page)

1. Go to `http://localhost:5105/chat`
2. Select a specific document in the sidebar or leave on All Documents
3. Type a question and press **Enter** or click **Send**
4. The answer streams in token by token
5. Sources used are shown below each assistant response

---

## Port Reference

| Service | URL | Notes |
|---|---|---|
| ChatBot.Client | `http://localhost:5105` | Open this in your browser |
| ChatBot.Server | `http://localhost:5087` | Backend API |
| Swagger UI | `http://localhost:5087/swagger` | API documentation |
| Ollama | `http://localhost:11434` | Local LLM server |

---

## Troubleshooting

| Error | Fix |
|---|---|
| `ERR_CONNECTION_REFUSED` on port 5087 | Make sure `ChatBot.Server` is running first |
| `405 Method Not Allowed` on `/hubs/chat` | Check `Chat.razor.cs` — hub URL must point to port 5087 |
| `dotnet-ef not found` | Run `dotnet tool install --global dotnet-ef` then reopen terminal |
| `Unable to create DbContext` | Check `Program.cs` uses `ChatbotDbContext` everywhere |
| Ollama connection error | Run `ollama serve` in a separate terminal |
| Model not found | Run `ollama pull mxbai-embed-large` and `ollama pull llama3.2:3b` |
| `wwwroot not found` warning | Normal — can be ignored, does not affect anything |
| `Failed to determine HTTPS port` | Remove `app.UseHttpsRedirection()` from `Program.cs` |