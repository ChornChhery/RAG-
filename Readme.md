# ChatBotRAG 🤖

A Retrieval-Augmented Generation (RAG) Chatbot built with .NET 10, Blazor WebAssembly, SignalR, and local Ollama models. Upload documents and chat with them in real time.

---

## Features ✨

- 📄 **Multi-Format Document Support** — Upload PDF, TXT, and Markdown files
- 💬 **Real-Time Streaming Chat** — Token-by-token responses via SignalR
- 🔍 **Hybrid Search (Vector + BM25)** — Combines semantic vector search with keyword-based BM25 retrieval for more accurate results
- 🌍 **Multilingual Support** — English, Thai, and Khmer with language-aware tokenization
- 🏗️ **Multiple Chunking Strategies** — FixedSize, ContentAware, and Semantic methods
- ⚡ **Embedding Cache** — In-memory vector cache eliminates repeated DB round trips at query time
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
├── ChatBot.Share/                    # Shared DTOs, Enums, Constants
│   ├── Constants/
│   │   ├── HubMethods.cs             # SignalR method name constants
│   │   └── HubRoutes.cs              # SignalR hub URL constant
│   ├── DTOs/
│   │   ├── ChatMessageDto.cs         # Single chat message model
│   │   ├── ChatRequest.cs            # User question + history
│   │   ├── DocumentChunkResult.cs    # Retrieved RAG chunk with score
│   │   ├── DocumentDto.cs            # Document metadata for UI
│   │   ├── StreamToken.cs            # Single streamed token
│   │   └── UploadResponse.cs         # Upload result response
│   └── Enums/
│       ├── ChunkingStrategy.cs       # FixedSize / ContentAware / Semantic
│       ├── DocumentStatus.cs         # Uploading/Processing/Ready/Failed
│       └── MessageRole.cs            # User/Assistant/System
│
├── ChatBot.Server/                   # ASP.NET Core Backend
│   ├── Controllers/
│   │   └── DocumentsController.cs   # Upload, list, delete, cache-stats API
│   ├── Data/
│   │   └── ChatbotDbContext.cs       # EF Core DbContext
│   ├── Hubs/
│   │   └── ChatHub.cs                # SignalR streaming hub
│   ├── Models/
│   │   ├── Document.cs               # EF Core document entity
│   │   └── DocumentChunk.cs          # EF Core chunk + vector entity
│   ├── Services/
│   │   ├── BM25Service.cs            # Multilingual BM25 keyword scoring
│   │   ├── ContentAwareChunkingStrategy.cs  # Sentence/markdown-aware chunking
│   │   ├── DocumentService.cs        # Document CRUD + cache invalidation
│   │   ├── EmbeddingCacheService.cs  # In-memory vector cache
│   │   ├── EmbeddingService.cs       # Chunking + Ollama embedding + cache update
│   │   ├── FixedSizeChunkingStrategy.cs     # Simple fixed-size chunking
│   │   ├── HybridSearchService.cs    # Vector + BM25 fusion search
│   │   ├── IChunkingStrategy.cs      # Chunking interface
│   │   ├── RagService.cs             # RAG pipeline orchestration
│   │   └── SemanticChunkingStrategy.cs      # Topic-coherence chunking
│   ├── Program.cs
│   └── appsettings.json
│
└── ChatBot.Client/                   # Blazor WebAssembly Frontend
    ├── Pages/
    │   ├── Chat.razor                # Chat conversation UI
    │   ├── Chat.razor.cs             # SignalR + message state
    │   ├── Chat.razor.css            # Chat styling
    │   ├── Rag.razor                 # Document upload UI
    │   ├── Rag.razor.cs              # Upload + delete logic
    │   └── Rag.razor.css             # RAG page styling
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

1. **Upload** → Document is split into chunks using selected strategy (⚙️ Processing state)
2. **Embed** → Each chunk converted to a 1024-dim vector via `mxbai-embed-large`
3. **Store** → Vectors saved in SQL Server with chunk text and chunking method
4. **Cache** → Vectors loaded into RAM on first query (stays in memory until restart)
5. **Query** → User question embedded and hybrid search runs:
   - **Vector search** — cosine similarity against cached embeddings (70% weight)
   - **BM25 search** — keyword matching against chunk text (30% weight)
   - **Fusion** — scores combined into a single ranked list
6. **Retrieve** → Top-K chunks above similarity threshold returned
7. **Generate** → Retrieved chunks sent to `llama3.2:3b` as context
8. **Stream** → LLM response sent token-by-token to client via SignalR

---

## Hybrid Search (Vector + BM25)

The system uses **weighted score fusion** to combine two complementary retrieval methods:

```
final_score = 0.7 × vector_score + 0.3 × bm25_score
```

| Method | Strength | Weakness |
|---|---|---|
| **Vector Search** | Semantic meaning, paraphrasing, concepts | Exact keywords, names, codes |
| **BM25** | Exact terms, technical names, IDs | Synonyms, semantic meaning |
| **Hybrid** | Both strengths combined | — |

### Configuration

Weights are configurable in `appsettings.json`:

```json
"HybridSearch": {
  "VectorWeight": 0.7,
  "MinSimilarityThreshold": 0.3
}
```

- `VectorWeight` — share given to vector score (BM25 gets `1 - VectorWeight`)
- `MinSimilarityThreshold` — chunks below this score are excluded from results

---

## Multilingual Support 🌍

The system supports **English, Thai, and Khmer** across all components:

### BM25 Tokenization

| Language | Method | Reason |
|---|---|---|
| **English** | Word tokenization (space split + stop words) | Words are space-separated |
| **Thai** | Character trigram (n-gram, n=3) | No spaces between words |
| **Khmer** | Character trigram (n-gram, n=3) | No spaces between words |
| **Mixed** | Auto-detects script per segment | Handles mixed-language text |

Thai example with n=3 on `"สวัสดีครับ"`:
```
→ ["สวั", "วัส", "ัสด", "สดี", "ดีค", "ีคร", "คร", "รับ"]
```

### Chunking Strategy Language Support

| Strategy | English | Thai | Khmer |
|---|---|---|---|
| **FixedSize** | ✅ | ✅ | ✅ |
| **ContentAware** | ✅ (markdown headings + paragraphs) | ✅ (newline boundaries) | ✅ (។ sentence terminator) |
| **Semantic** | ✅ (.!? sentence split) | ✅ (ๆ/ฯ markers + newline) | ✅ (។ U+17D4 terminator) |

---

## Embedding Cache ⚡

The in-memory cache eliminates repeated database round trips at query time.

### How It Works

```
App starts → cache empty

First query:
  → Load ALL ready chunks from SQL Server
  → Deserialize EmbeddingJson → float[] for each chunk
  → Store in ConcurrentDictionary<Guid, CachedChunk> in RAM
  → Answer query from RAM

Every query after:
  → Read directly from RAM (no DB, no JSON parsing)
  → Run vector + BM25 scoring
  → Return results

Upload new document:
  → Processing completes → Status = Ready
  → Add only new chunks to existing cache (no full reload)

Delete document:
  → Remove only that document's chunks from cache
  → Rest of cache untouched

App restart:
  → RAM cleared → cache empty
  → First query triggers fresh load from DB
```

### Cache Warm-Up on Startup

To avoid a slow first query, the cache pre-loads on app start:

```csharp
// Program.cs — runs before app.Run()
var cache = app.Services.GetRequiredService<EmbeddingCacheService>();
await cache.WarmUpAsync();
```

### Memory Estimate

| Chunks | Approximate RAM |
|---|---|
| 1,000 | ~4 MB |
| 10,000 | ~40 MB |
| 50,000 | ~200 MB |

### Cache Stats Endpoint

Monitor cache state at runtime:

```
GET /api/documents/cache-stats

Response:
{
  "totalChunks": 1240,
  "totalDocuments": 8,
  "isLoaded": true,
  "estimatedMemoryMb": 4.9
}
```

---

## Chunking Strategies 🔀

Choose the best chunking method for your documents during upload:

| Strategy | How It Works | Best For | Speed |
|---|---|---|---|
| **FixedSize** (Default) | Splits text into fixed 500-char chunks with 100-char overlap | Quick indexing, simple documents, prototyping | ⚡ Fast |
| **ContentAware** | Respects sentence boundaries, markdown structure, paragraphs; variable-size chunks (100–1000 chars) | Markdown docs, mixed content, structured text | 🔶 Medium |
| **Semantic** | Groups sentences by topic coherence using word-overlap similarity; variable-size chunks (150–1200 chars) | Research papers, long-form content, high accuracy needed | 🟡 Slower |

### How to Select a Chunking Method

1. Go to the RAG page: `http://localhost:5105/rag`
2. Select a strategy from the **"Select Chunking Method"** cards
3. Upload your document — it will be chunked using the selected method
4. The method used is displayed as a badge in the document table

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

Open `ChatBot.Server/appsettings.json` and update:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=ChatBotRag;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "ChatModel": "llama3.2:3b",
    "EmbedModel": "mxbai-embed-large:latest"
  },
  "HybridSearch": {
    "VectorWeight": 0.7,
    "MinSimilarityThreshold": 0.3
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

This applies all migrations including:

- `InitialCreate` — Initial database schema (Documents, DocumentChunks tables)
- `AddChunkingMethod` — Adds `ChunkingMethod` column to DocumentChunks table

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
2. **Step 1** — Select a chunking strategy (Fixed Size, Content Aware, or Semantic)
3. **Step 2** — Drag and drop or click to select a PDF, TXT, or MD file
4. Click **Upload Files** to confirm
5. Wait for the status badge to change from ⚙️ Processing to ✅ Ready
6. The document is now embedded, cached, and ready for chat

### Chat with Documents (Chat Page)

1. Go to `http://localhost:5105/chat`
2. Type a question and press **Enter** or click **Send**
3. The answer streams in token by token via SignalR
4. Sources used are shown below each assistant response

---

## Port Reference

| Service | URL | Notes |
|---|---|---|
| ChatBot.Client | `http://localhost:5105` | Open this in your browser |
| ChatBot.Server | `http://localhost:5087` | Backend API only |
| Swagger UI | `http://localhost:5087/swagger` | API documentation |
| SignalR Hub | `http://localhost:5087/hubs/chat` | Internal WebSocket |
| Ollama | `http://localhost:11434` | Local LLM server |

---

## API Reference

### Document Endpoints

| Method | Route | Description | Response |
|---|---|---|---|
| GET | `/api/documents` | List all documents | `List<DocumentDto>` |
| GET | `/api/documents/{id}` | Get one document | `DocumentDto` |
| GET | `/api/documents/cache-stats` | Cache memory stats | `CacheStats` |
| POST | `/api/documents/upload?strategy=0` | Upload and process a file | `UploadResponse` |
| DELETE | `/api/documents/{id}` | Delete document and chunks | `204 No Content` |

**Chunking strategy query param values:**

| Value | Strategy |
|---|---|
| `0` | FixedSize (default) |
| `1` | ContentAware |
| `2` | Semantic |

### SignalR Chat Hub

**Connect to Hub:**
```
WebSocket: ws://localhost:5087/hubs/chat
```

**Send Message** (Client → Server):
```csharp
await hubConnection.InvokeAsync("SendMessage", chatRequest, messageId);
```

**Receive Token** (Server → Client):
```csharp
hubConnection.On<StreamToken>("ReceiveToken", token => {
    // token.Token — streamed text
    // token.IsFinal — true when stream complete
    // token.MessageId — matches the client-generated messageId
});
```

**Chat Complete** (Server → Client):
```csharp
hubConnection.On<List<DocumentChunkResult>>("ChatComplete", sources => {
    // sources — list of document chunks used as context
});
```

---

## Troubleshooting

| Error | Root Cause | Fix |
|---|---|---|
| `ERR_CONNECTION_REFUSED` on 5087 | Server not running | Start `ChatBot.Server` first |
| `Failed to connect to SignalR hub` | Wrong port in client | Ensure `Chat.razor.cs` uses port 5087 |
| `dotnet-ef not found` | EF tools not installed | `dotnet tool install --global dotnet-ef` then reopen terminal |
| `Unable to create DbContext` | Class name mismatch | Check `AddDbContext<ChatbotDbContext>` matches class name |
| Ollama connection error | Ollama not running | Run `ollama serve` in a separate terminal |
| Model not found error | Model not pulled | Run `ollama pull mxbai-embed-large` and `ollama pull llama3.2:3b` |
| Document stuck at Processing | Ollama not running at upload time | Check server logs, ensure `ollama serve` is running |
| Cache shows 0 chunks after upload | Document not yet Ready | Wait for status = Ready, then ask a question to trigger cache add |
| BM25 returns 0 scores for Thai/Khmer | Expected — n-gram overlap may be low | Vector search (70%) still works; hybrid score will be non-zero if semantic match exists |
| Slow first query after restart | Cache cold start | Add `WarmUpAsync()` call in `Program.cs` to pre-load on startup |
| `413 Payload Too Large` on upload | File exceeds limit | Increase `RequestSizeLimit` in `DocumentsController.cs` |
| `Database already exists` during migration | Previous DB exists | Run `dotnet ef database drop --force` then `dotnet ef database update` |

---

## Performance Tuning

| Setting | Location | Impact |
|---|---|---|
| `VectorWeight` | `appsettings.json` | Tune vector vs BM25 balance (default 0.7) |
| `MinSimilarityThreshold` | `appsettings.json` | Higher = fewer but more relevant chunks |
| `NgramSize` in BM25 | `BM25Service.cs` | Larger n-grams = more specific Thai/Khmer matching |
| Chunk size | `FixedSizeChunkingStrategy.cs` | Smaller = more precise retrieval, more chunks in cache |
| `batchSize` in embedding | `EmbeddingService.cs` | Higher batch = faster embedding, more memory |
| Ollama model | `appsettings.json` | Larger model = better answers, slower generation |

---

## Architecture Notes

- **Blazor WASM** handles all UI rendering in the browser
- **SignalR** maintains a persistent WebSocket for token-by-token streaming
- **Hybrid Search** runs entirely in RAM after first cache load — no DB at query time
- **EmbeddingCacheService** is registered as `Singleton` — it holds shared RAM state across all requests
- **EmbeddingService** is registered as `Singleton` — it manages its own `DbContext` scope for background processing
- **Partial cache updates** — upload adds only new chunks, delete removes only that document's chunks
- **Cosine similarity formula:** `similarity = dot(a,b) / (‖a‖ × ‖b‖)` — result in [-1, 1], higher = more similar
- **BM25 formula:** `score = IDF × (tf × (k1+1)) / (tf + k1 × (1 - b + b × docLen/avgDocLen))`

---

## Common Use Cases

- 📚 **Knowledge Base Chatbot** — Upload company docs, answer employee questions
- 📖 **Research Assistant** — Index papers, get summaries with source citations
- 🏥 **Medical Documentation** — Query patient records with full privacy (local Ollama)
- ⚖️ **Legal Document Search** — Find relevant clauses across contracts
- 🎓 **Educational Tool** — Personalized tutoring with textbook references
- 🌏 **Multilingual RAG** — Upload Thai, Khmer, or English documents and query in any language

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