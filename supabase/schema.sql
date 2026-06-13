-- Contract Clause Intelligence Assistant — Supabase schema.
-- Run this once in the Supabase SQL editor (Database → SQL Editor → New query).
-- Replaces Azure SQL (relational rows) AND Azure AI Search (vector + keyword
-- retrieval). PDFs are processed in-memory by the API; only clauses persist.
--
-- The app uses EF Core (snake_case) for both writes and hybrid retrieval, so no
-- stored function is required here — this file just creates the extension,
-- tables, and the indexes that make dense + full-text retrieval fast.

-- pgvector powers the dense (vector) half of hybrid retrieval.
create extension if not exists vector;

-- ── Tables ──────────────────────────────────────────────────────────────────

create table if not exists contracts (
  id               uuid primary key default gen_random_uuid(),
  name             text        not null,
  status           text        not null default 'Processing', -- Processing | Ready | Failed
  error            text,
  clause_count     int         not null default 0,
  uploaded_at_utc  timestamptz not null default now(),
  processed_at_utc timestamptz
);

-- Embedding dimension (768) must match the embedding model (Gemini
-- text-embedding-004) and ContractClauseDbContext.EmbeddingDimensions.
-- Change all three together if you swap models.
create table if not exists clauses (
  id            uuid primary key default gen_random_uuid(),
  contract_id   uuid not null references contracts(id) on delete cascade,
  clause_number text not null default '',
  clause_title  text not null default '',
  text          text not null default '',
  page_number   int  not null default 0,
  embedding     vector(768)
);

create table if not exists query_logs (
  id                   uuid primary key default gen_random_uuid(),
  question             text        not null,
  confidence           text        not null default '',
  insufficient_context boolean     not null default false,
  retrieval_ms         int         not null default 0,
  generation_ms        int         not null default 0,
  total_ms             int         not null default 0,
  asked_at_utc         timestamptz not null default now()
);

-- ── Indexes ─────────────────────────────────────────────────────────────────
-- HNSW for fast approximate nearest-neighbour (cosine) vector search.
create index if not exists clauses_embedding_idx
  on clauses using hnsw (embedding vector_cosine_ops);
-- GIN over a full-text vector for the sparse (BM25-like) keyword half; the
-- expression matches the EF query (to_tsvector('english', text)).
create index if not exists clauses_text_fts_idx
  on clauses using gin (to_tsvector('english', text));
create index if not exists clauses_contract_id_idx
  on clauses (contract_id);
