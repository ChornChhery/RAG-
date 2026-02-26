# ChatBotRAG 🤖

A Retrieval-Augmented Generation (RAG) Chatbot built with .NET 10, Blazor WebAssembly, SignalR, and local Ollama models. Upload documents and chat with them in real time.

---

## Features ✨

- 📄 **Multi-Format Document Support** — Upload PDF, TXT, and Markdown files
- 💬 **Real-Time Streaming Chat** — Token-by-token responses via SignalR
- 🔍 **Vector Search** — Semantic document retrieval using embeddings
- 🏗️ **Multiple Chunking Strategies** — FixedSize, ContentAware, and Semantic methods
- 📌 **Source Attribution** — View which document chunks were used in responses
- 🎯 **Local LLM** — No cloud dependency, full privacy with Ollama
- 🚀 **Full-Stack .NET** — Type-safe end-to-end architecture
- 📊 **SQL Server Backend** — Persistent storage of documents and embeddings

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
│   │   └── DocumentController.cs    # Upload, list, delete API
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

## How It Works (RAG Architecture)

**Retrieval-Augmented Generation** combines document retrieval with generative AI:

1. **Upload** → Document is split into chunks (⚙️ Processing state)
2. **Embed** → Each chunk converted to vector via `mxbai-embed-large` 
3. **Store** → Vectors saved in SQL Server with chunk text
4. **Query** → User question embedded and compared (cosine similarity)
5. **Retrieve** → Top matching chunks retrieved from database
6. **Generate** → Retrieved chunks sent to `llama3.2:3b` as context
7. **Stream** → LLM response sent token-by-token to client

This ensures responses are grounded in your uploaded documents.

---

## Chunking Strategies 🔀

Choose the best chunking method for your documents during upload:

| Strategy | How It Works | Best For | Speed |
|--|--|--|--|
| **FixedSize** (Default) | Splits text into fixed 500-char chunks with 100-char overlap | Quick indexing, simple documents, prototyping | ⚡ Fast |
| **ContentAware** | Respects sentence boundaries, markdown structure, paragraphs; variable-size chunks (100-1000 chars) | Markdown docs, mixed content, structured text | 🔶 Medium |
| **Semantic** | Groups sentences by topic coherence, maximizes meaning preservation; word-overlap based similarity | Research papers, long-form content, high accuracy needed | 🟡 Slower |

### How to Select a Chunking Method

1. Go to the RAG page: `http://localhost:5105/rag`
2. Use the **"Chunking Method"** dropdown before uploading
3. Select your preferred strategy
4. Upload your document — it will be chunked accordingly

### Recommendations

- **Start with FixedSize** if unsure — it's fast and works for most use cases
- **Use ContentAware** for markdown/structured documents (README.md, wikis, formatted guides)
- **Use Semantic** for dense content where context is critical (academic papers, technical manuals)

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
dotnet ef database update
```

This applies the initial schema and creates the `Documents`, `DocumentChunks`, and related tables.

**Note:** If this is your first time running, Entity Framework automatically applies all migrations including:
- `InitialCreate` — Initial database schema
- `AddChunkingMethod` — Adds chunking strategy tracking to document chunks

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

## API Reference

### Document Endpoints

**Upload Document**
```
POST /api/document
Content-Type: multipart/form-data

Body: file (PDF, TXT, or Markdown)
Response: { documentId: string, fileName: string, status: "Uploading" }
```

**List Documents**
```
GET /api/document
Response: DocumentDto[]
```

**Delete Document**
```
DELETE /api/document/{id}
Response: 204 No Content
```

### SignalR Chat Hub

**Connect to Hub**
```
WebSocket: ws://localhost:5087/hubs/chat
```

**Send Message** (Client → Server)
```csharp
await hubConnection.SendAsync("SendMessage", new ChatRequest 
{ 
    UserMessage = "Your question", 
    DocumentId = "doc-id or null for all",
    ChatHistory = previousMessages
});
```

**Receive Token** (Server → Client)
```csharp
hubConnection.On<StreamToken>("ReceiveToken", token =>
{
    // token.Content contains streamed text
    // token.IsComplete indicates end of response
});
```

---

## Troubleshooting

| Error | Fix |
|---|---|
| `ERR_CONNECTION_REFUSED` on port 5087 | Make sure `ChatBot.Server` is running first |
| `Failed to connect to SignalR hub` | Ensure `Program.cs` client points to `http://localhost:5087` |
| `dotnet-ef not found` | Run `dotnet tool install --global dotnet-ef` then reopen terminal |
| `Unable to create DbContext` | Check connection string in `appsettings.json` matches your SQL Server |
| Ollama connection error | Run `ollama serve` in a separate terminal and verify port 11434 is open |
| Model not found error | Run `ollama pull mxbai-embed-large` and `ollama pull llama3.2:3b` |
| `Database already exists` during migration | Run `dotnet ef database drop --force` then `dotnet ef database update` |
| Slow embedding generation | Reduce chunk size in `EmbeddingService.cs` or switch to faster model |
| Out of memory errors | Reduce `llama3.2:3b` to `llama2:7b-chat` (smaller model) in `appsettings.json` |
| Upload fails with `413 Payload Too Large` | Increase `MaxRequestBodySize` in `Program.cs` |
| `wwwroot not found` warning | Normal in Blazor projects — can be safely ignored |

---

## Environment Configuration

You can override settings via environment variables:

```bash
# Linux/Mac
export ConnectionStrings__DefaultConnection="Server=...";
export Ollama__BaseUrl="http://localhost:11434";
export Ollama__ChatModel="llama3.2:3b";
export Ollama__EmbedModel="mxbai-embed-large:latest";

# Windows PowerShell
$env:ConnectionStrings__DefaultConnection = "Server=..."
$env:Ollama__BaseUrl = "http://localhost:11434"
```

Configuration priority:
1. Environment variables (highest)
2. `appsettings.{Environment}.json`
3. `appsettings.json` (default)

---

## Development Tips

### Change the LLM Model
Edit `appsettings.json`:
```json
"Ollama": {
  "ChatModel": "llama2:13b-chat"  // Larger, better quality
  "EmbedModel": "mxbai-embed-large:latest"
}
```

Available models: `ollama list` or visit [ollama.com/library](https://ollama.com/library)

### Hot Reload During Development
Run with `dotnet watch`:
```bash
cd ChatBot.Server
dotnet watch run
```

### Debug SignalR Communication
Add logging to `Program.cs`:
```csharp
builder.Services.AddSignalR()
    .AddHubOptions<ChatHub>(options =>
    {
        options.EnableDetailedErrors = true;
    });
```

### Test API Endpoints
Use Swagger UI: `http://localhost:5087/swagger`

---

## Performance Tuning

| Setting | Impact |
|---|---|
| **Chunk Size** (in `EmbeddingService.cs`) | Smaller = more precise retrieval, slower embedding |
| **Vector Search Threshold** | Higher = fewer results, more relevant |
| **SignalR MessagePack** | Add `AddMessagePackProtocol()` for compression |
| **Ollama Model Size** | Larger models = better answers, slower generation |
| **Database Indexes** | Add indexes on `DocumentId` and embeddings for faster search |

---

## Architecture Notes

- **Blazor WASM** handles UI rendering in browser (no server-side rendering)
- **SignalR** maintains persistent WebSocket connection for streaming responses
- **Cosine Similarity** used for vector search: `1 - (A·B / ‖A‖‖B‖)`
- **Chunk Overlap** prevents context loss at boundaries
- **Entity Framework** handles all database operations (migrations included)

---

## Common Use Cases

- 📚 **Knowledge Base Chatbot** — Upload company docs, answer employee questions
- 📖 **Research Assistant** — Index papers, get summaries with source citations
- 🏥 **Medical Documentation** — Query patient records with privacy (local Ollama)
- ⚖️ **Legal Document Search** — Find relevant clauses across contracts
- 🎓 **Educational Tool** — Personalized tutoring with textbook references

---

## Contributing

Feel free to submit issues and enhancement requests!

### Local Development Workflow
1. Create a feature branch: `git checkout -b feature/your-feature`
2. Make changes and test thoroughly
3. Submit a pull request with description

---

## License

This project is licensed under Mr.Chhery Chorn — see the LICENSE file for details.

---

## Support

For issues, questions, or suggestions, please open an issue on this repository.

**Happy chatting!** 🚀