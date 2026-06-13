export interface ContractInfo {
  id: string
  name: string
  status: 'Processing' | 'Ready' | 'Failed'
  clauseCount: number
  uploadedAtUtc: string
  processedAtUtc?: string
  error?: string
}

export interface Citation {
  contractName: string
  clauseNumber: string
  clauseTitle: string
  excerpt: string
  pageNumber: number
}

export type Confidence = 'high' | 'medium' | 'low' | 'none'

export interface QueryResponse {
  answer: string
  citations: Citation[]
  confidence: Confidence
  insufficientContext: boolean
  latencyMs: { retrieval: number; generation: number; total: number }
}

export interface ChatMessage {
  id: string
  role: 'user' | 'assistant'
  text: string
  response?: QueryResponse
  error?: boolean
}
