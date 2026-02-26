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
- 🧪 **RAG Evaluation** — Automatically scores answer quality using BLEU, GLEU, F1, and LLM Judge
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
├── ChatBot.Share/                        # Shared DTOs, Enums, Constants
│   ├── Constants/
│   │   ├── HubMethods.cs                 # SignalR method name constants
│   │   └── HubRoutes.cs                  # SignalR hub URL constant
│   ├── DTOs/
│   │   ├── ChatMessageDto.cs             # Single chat message model
│   │   ├── ChatRequest.cs                # User question + history
│   │   ├── DocumentChunkResult.cs        # Retrieved RAG chunk with score
│   │   ├── DocumentDto.cs                # Document metadata for UI
│   │   ├── EvaluationRequest.cs          # Evaluation question + document selection
│   │   ├── EvaluationResult.cs           # All 4 scores + generated answers
│   │   ├── StreamToken.cs                # Single streamed token
│   │   └── UploadResponse.cs             # Upload result response
│   └── Enums/
│       ├── ChunkingStrategy.cs           # FixedSize / ContentAware / Semantic
│       ├── DocumentStatus.cs             # Uploading / Processing / Ready / Failed
│       └── MessageRole.cs                # User / Assistant / System
│
├── ChatBot.Server/                       # ASP.NET Core Backend
│   ├── Controllers/
│   │   ├── DocumentsController.cs        # Upload, list, delete, cache-stats API
│   │   └── EvaluationController.cs       # POST /api/evaluation endpoint
│   ├── Data/
│   │   └── ChatbotDbContext.cs           # EF Core DbContext
│   ├── Evaluators/                       # NLP utility classes (pure math, no DI deps)
│   │   ├── BLEUEvaluator.cs             # N-gram precision scoring
│   │   ├── GLEUEvaluator.cs             # Balanced precision + recall scoring
│   │   └── F1Evaluator.cs               # Token overlap F1 scoring
│   ├── Hubs/
│   │   └── ChatHub.cs                    # SignalR streaming hub
│   ├── Models/
│   │   ├── Document.cs                   # EF Core document entity
│   │   └── DocumentChunk.cs              # EF Core chunk + vector entity
│   ├── Services/
│   │   ├── BM25Service.cs                # Multilingual BM25 keyword scoring
│   │   ├── ContentAwareChunkingStrategy.cs  # Sentence/markdown-aware chunking
│   │   ├── DocumentService.cs            # Document CRUD + cache invalidation
│   │   ├── EmbeddingCacheService.cs      # In-memory vector cache (Singleton)
│   │   ├── EmbeddingService.cs           # Chunking + Ollama embedding + cache update
│   │   ├── EvaluationService.cs          # Full evaluation pipeline orchestration
│   │   ├── FixedSizeChunkingStrategy.cs  # Simple fixed-size chunking
│   │   ├── HybridSearchService.cs        # Vector + BM25 fusion search
│   │   ├── IChunkingStrategy.cs          # Chunking strategy interface
│   │   ├── RagService.cs                 # RAG pipeline orchestration
│   │   └── SemanticChunkingStrategy.cs   # Topic-coherence chunking
│   ├── Program.cs
│   └── appsettings.json
│
└── ChatBot.Client/                       # Blazor WebAssembly Frontend
    ├── Pages/
    │   ├── Chat.razor                    # Chat conversation UI
    │   ├── Chat.razor.cs                 # SignalR + message state
    │   ├── Chat.razor.css                # Chat styling
    │   ├── Evaluation.razor              # RAG evaluation UI
    │   ├── Evaluation.razor.cs           # Evaluation state + API calls
    │   ├── Evaluation.razor.css          # Evaluation styling
    │   ├── Rag.razor                     # Document upload UI
    │   ├── Rag.razor.cs                  # Upload + delete logic
    │   └── Rag.razor.css                 # RAG page styling
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

1. **Upload** → Document is split into chunks using the selected strategy (⚙️ Processing state)
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
  "MinSimilarityThreshold": 0.6
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

### Evaluation Multilingual Support

| Metric | English | Thai | Khmer |
|---|---|---|---|
| **BLEU** | ✅ Accurate | ⚠️ Approximate (trigrams) | ⚠️ Approximate (trigrams) |
| **GLEU** | ✅ Accurate | ⚠️ Approximate (trigrams) | ⚠️ Approximate (trigrams) |
| **F1** | ✅ Accurate | ⚠️ Approximate (trigrams) | ⚠️ Approximate (trigrams) |
| **LLM Judge** | ✅ Accurate | ✅ Accurate | ✅ Accurate |

For Thai and Khmer documents, always trust the **LLM Judge** score most — it understands all languages natively.

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
  → Add only new document's chunks to existing cache (no full reload)

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

### Recommendations

- **Start with FixedSize** if unsure — it's fast and works for most use cases
- **Use ContentAware** for markdown/structured documents (README.md, wikis, formatted guides)
- **Use Semantic** for dense content where context is critical (academic papers, technical manuals)

---

## RAG Evaluation 🧪

The Evaluation page lets you test how well your RAG pipeline answers questions from your uploaded documents. No reference answer needed — the system generates one automatically.

### How It Works

```
You type a question only

Step 1 → Hybrid search retrieves relevant chunks from selected document
Step 2 → LLM generates an ideal reference answer from those chunks
Step 3 → LLM generates the actual RAG answer (normal pipeline)
Step 4 → BLEU, GLEU, F1 compare RAG answer vs LLM-generated reference
Step 5 → LLM Judge independently scores semantic quality
Step 6 → All 4 scores + both answers displayed in UI
```

### NLP Evaluation Metrics

| Metric | What It Measures | Best For |
|---|---|---|
| **BLEU** | N-gram precision — how many generated phrases match the reference | Checking factual phrase accuracy |
| **GLEU** | Balanced precision + recall — penalizes both wrong additions and missing content | Short answers, sentence-level QA |
| **F1** | Token overlap — balances precision and recall at token level | Standard QA metric (SQuAD benchmark) |
| **LLM Judge** | Semantic quality scored by the LLM itself (0–10 normalized to 0–1) | Multilingual, meaning-based evaluation |

### Score Interpretation

| Score | Label | Meaning |
|---|---|---|
| > 0.7 | ✅ Excellent | RAG is retrieving and answering correctly |
| 0.4–0.7 | 🔵 Good | Mostly correct, minor differences in phrasing |
| 0.2–0.4 | ⚠️ Fair | Partial match — check chunking strategy or threshold |
| < 0.2 | ❌ Poor | RAG is not finding the right content — re-upload or re-chunk |

### Practical Workflow

```
1. Upload a document on the RAG page
2. Go to the Evaluation page
3. Select the document you want to test
4. Ask a question answerable from that document
5. Check all 4 scores + LLM judge explanation
6. If scores are low:
   → Try a different chunking strategy (re-upload)
   → Lower MinSimilarityThreshold in appsettings.json
   → Try a larger Ollama model
7. Re-test until scores are consistently high
```

### Evaluator Architecture

The three NLP evaluators live in `ChatBot.Server/Evaluators/` — separate from `Services/` because they are pure utility classes with no DI dependencies:

```
ChatBot.Server/
  Evaluators/              ← pure math utility classes
    BLEUEvaluator.cs
    GLEUEvaluator.cs
    F1Evaluator.cs
  Services/
    EvaluationService.cs   ← real service (has DI dependencies)
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
    "MinSimilarityThreshold": 0.60
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

### Evaluate RAG Quality (Evaluation Page)

1. Go to `http://localhost:5105/evaluation`
2. Select a document to test (only Ready documents are shown)
3. Type a question you know is answerable from that document
4. Click **Run Evaluation**
5. Wait for all 5 steps to complete (search → reference → answer → NLP scores → LLM judge)
6. Review all 4 scores and the side-by-side answer comparison

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

### Evaluation Endpoint

| Method | Route | Description | Response |
|---|---|---|---|
| POST | `/api/evaluation` | Run full evaluation pipeline | `EvaluationResult` |

**Request body:**
```json
{
  "question": "What is the main topic of this document?",
  "documentId": "optional-guid-or-null-for-all",
  "topK": 5
}
```

**Response:**
```json
{
  "question": "...",
  "autoGeneratedReference": "Ideal answer from chunks...",
  "generatedAnswer": "Actual RAG answer...",
  "sourceDocuments": ["doc1.pdf"],
  "bleuScore": 0.72,
  "gleuScore": 0.68,
  "f1Score": 0.81,
  "llmJudgeScore": 0.90,
  "llmJudgeExplanation": "The answer correctly covers...",
  "overallScore": 0.78
}
```

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
| Slow first query after restart | Cache cold start | Add `WarmUpAsync()` call in `Program.cs` to pre-load on startup |
| BM25 returns 0 for Thai/Khmer | Expected — n-gram overlap may be low | Vector search (70%) still works; rely on LLM Judge for multilingual scoring |
| Evaluation stuck at "Generating..." | LLM taking long or timed out | Wait — evaluation makes 3 LLM calls; larger models take longer |
| Evaluation scores all 0 | No chunks found | Ensure selected document status is Ready and has content |
| BLEU/GLEU/F1 low for Thai/Khmer | Trigram approximation limitation | Trust LLM Judge score instead — it understands Thai/Khmer natively |
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
| `TopK` in evaluation | `EvaluationRequest` | Higher = more context for reference generation, slower |
| Ollama model | `appsettings.json` | Larger model = better answers and evaluation, slower |

---

## Architecture Notes

- **Blazor WASM** handles all UI rendering in the browser
- **SignalR** maintains a persistent WebSocket for token-by-token streaming
- **Hybrid Search** runs entirely in RAM after first cache load — no DB at query time
- **EmbeddingCacheService** is registered as `Singleton` — it holds shared RAM state across all requests
- **EmbeddingService** is registered as `Singleton` — it manages its own `DbContext` scope for background processing
- **Partial cache updates** — upload adds only new chunks, delete removes only that document's chunks
- **Evaluators vs Services** — `BLEUEvaluator`, `GLEUEvaluator`, `F1Evaluator` are pure utility classes with no DI dependencies kept in `Evaluators/`; `EvaluationService` is a real service with DI and lives in `Services/`
- **Evaluation makes 3 LLM calls** — reference generation, RAG answer generation, LLM judge scoring
- **Cosine similarity:** `similarity = dot(a,b) / (‖a‖ × ‖b‖)` — result in [-1, 1], higher = more similar
- **BM25 formula:** `score = IDF × (tf × (k1+1)) / (tf + k1 × (1 - b + b × docLen/avgDocLen))`

---

## Common Use Cases

- 📚 **Knowledge Base Chatbot** — Upload company docs, answer employee questions
- 📖 **Research Assistant** — Index papers, get summaries with source citations
- 🏥 **Medical Documentation** — Query patient records with full privacy (local Ollama)
- ⚖️ **Legal Document Search** — Find relevant clauses across contracts
- 🎓 **Educational Tool** — Personalized tutoring with textbook references
- 🌏 **Multilingual RAG** — Upload Thai, Khmer, or English documents and query in any language
- 🔬 **RAG Quality Testing** — Use the Evaluation page to benchmark chunking strategies and model quality

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