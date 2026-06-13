# Contract Clause Intelligence Assistant (UC07.1)

GenAI-powered RAG assistant that surfaces, explains, and compares clauses across
a contract portfolio — with clause-level citations, a confidence signal, and an
honest "insufficient context" instead of hallucinations.

This build runs entirely on **free / open-source infrastructure** and deploys as
a single container to **Render** (migrated off Azure):

```
PDF upload → in-memory PdfPig extraction → clause-aware chunking
→ embeddings (Gemini text-embedding-004) → Postgres + pgvector
Query → hybrid retrieval (pgvector cosine + Postgres full-text, RRF-fused)
→ grounded answer (Groq / Llama) → citations + confidence → React UI
```

## Stack

| Concern | Service | Was (Azure) |
|---|---|---|
| API + SPA host | .NET 9 minimal API in a Docker container on **Render** | App Service |
| Chat / generation | **Groq** free API (open-source Llama, OpenAI-compatible) | Azure OpenAI |
| Embeddings | **Google Gemini** `text-embedding-004` (free, 768-dim) | Azure OpenAI |
| Relational store | **Supabase Postgres** (EF Core / Npgsql) | Azure SQL |
| Vector + keyword search | **pgvector** + Postgres full-text, in the same DB | Azure AI Search |
| Ingestion | **inline** in the API request | Azure Function + Service Bus |
| PDF storage | none — processed in-memory | Azure Blob Storage |

Chat and embeddings each fall back to a deterministic **mock** when their key is
absent, so the app runs locally with no keys.

## Repository layout

| Path | What |
|------|------|
| `src/api` | .NET 9 minimal API — upload, seed, contracts, query; serves the React build; runs ingestion inline |
| `src/shared` | clause chunker, AI layer (Groq chat + Gemini embeddings + mocks), pgvector search, EF Core model, ingestion pipeline |
| `src/web` | React + TypeScript (Vite) frontend |
| `supabase/schema.sql` | Postgres schema: tables, pgvector + full-text indexes |
| `Dockerfile` | multi-stage build (React → .NET publish → runtime) |
| `render.yaml` | Render Blueprint (one Docker web service) |
| `tools/SampleContractGenerator` | generates the 3 sample contract PDFs (QuestPDF) |
| `sample-contracts` | NDA, MSA, SaaS agreement fixtures used by `POST /api/seed` |

## One-time setup

1. **Supabase** — create a project, then run [`supabase/schema.sql`](supabase/schema.sql)
   in the SQL editor (creates the `vector` extension, tables, and indexes).
2. **Groq** — get a free key at <https://console.groq.com/keys>.
3. **Gemini** — get a free key at <https://aistudio.google.com/app/apikey>.
4. Copy [`.env.example`](.env.example) → `.env` and fill in `DATABASE_CONNECTION`,
   `GROQ_API_KEY`, `GEMINI_API_KEY`.

## Local development

Requirements: .NET SDK 9, Node 20+.

```powershell
# 1. API (loads .env automatically) → http://localhost:5266
dotnet run --project src/api/ContractClause.Api.csproj --launch-profile http
#    GET /api/ai/status → { "mode": "groq" | "mock", "embeddings": "gemini" | "mock" }

# 2. Frontend dev server (proxies /api to the API)
npm install --prefix src/web
npm run dev --prefix src/web

# 3. Tests
dotnet test
```

## API

| Endpoint | Purpose |
|----------|---------|
| `POST /api/upload` (multipart `file`) | PDF → inline ingest → clauses (`Ready`) |
| `POST /api/seed` | pushes the 3 bundled sample contracts through the pipeline |
| `GET /api/contracts` | list with status + clause counts |
| `GET /api/contracts/{id}/clauses` | clause chunks of one contract |
| `DELETE /api/contracts/{id}` | remove a contract and its clauses |
| `POST /api/query` `{ "question": "..." }` | hybrid retrieval + grounded answer |

`POST /api/query` response:

```json
{
  "answer": "…with inline [Contract — Clause N] citations…",
  "citations": [{ "contractName": "…", "clauseNumber": "9", "clauseTitle": "…", "excerpt": "…", "pageNumber": 2 }],
  "confidence": "high | medium | low | none",
  "insufficientContext": false,
  "latencyMs": { "retrieval": 0, "generation": 0, "total": 0 }
}
```

Retrieval fuses dense (pgvector cosine, HNSW) and sparse (Postgres `websearch`
full-text) candidates with Reciprocal Rank Fusion. There is no semantic
reranker, so confidence is derived from the answer's verifiable citation count
(`> 1` → high, `1` → medium, `0` → low, ungrounded → none). Every query is
logged to `query_logs`.

## Deploy to Render

1. Push this repo to GitHub.
2. Render → **New → Blueprint**, point it at the repo (uses [`render.yaml`](render.yaml)),
   or create a **Web Service** from the `Dockerfile` manually.
3. Set the secret env vars in the Render dashboard: `DATABASE_CONNECTION`,
   `GROQ_API_KEY`, `GEMINI_API_KEY`.
4. Render builds the Docker image and serves the API + SPA on its assigned
   `PORT`. Health check: `/api/ai/status`.

> The free Render plan spins the service down when idle, so the first request
> after a cold start is slow.

## Demo

Follow [demo-script.md](demo-script.md) — five questions including one
cross-contract comparison and one the corpus cannot answer.

> Note: the original Azure DevOps CI/CD pipelines, load-testing, and Playwright
> test tooling were removed during the migration. Deployment is now via Render
> (see above); the .NET unit tests remain under `tests/`.
