import { useEffect, useRef, useState } from 'react'
import type { ChatMessage } from '../types'
import { api } from '../api'
import { CitationChip } from './CitationChip'
import { ConfidenceBadge } from './ConfidenceBadge'

const SUGGESTIONS = [
  'What notice period is required to terminate the MSA for convenience?',
  'Compare the limitation of liability caps across the three contracts.',
  'How long do confidentiality obligations survive after the NDA terminates?',
]

export function ChatPanel({ disabled }: Readonly<{ disabled: boolean }>) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [question, setQuestion] = useState('')
  const [asking, setAsking] = useState(false)
  const scrollRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    scrollRef.current?.scrollTo({ top: scrollRef.current.scrollHeight, behavior: 'smooth' })
  }, [messages])

  async function ask(text: string) {
    const q = text.trim()
    if (!q || asking) return
    setQuestion('')
    setAsking(true)
    setMessages(m => [...m, { id: crypto.randomUUID(), role: 'user', text: q }])
    try {
      const response = await api.query(q)
      setMessages(m => [...m, { id: crypto.randomUUID(), role: 'assistant', text: response.answer, response }])
    } catch (e) {
      setMessages(m => [...m, {
        id: crypto.randomUUID(),
        role: 'assistant',
        text: e instanceof Error ? `Query failed: ${e.message}` : 'Query failed',
        error: true,
      }])
    } finally {
      setAsking(false)
    }
  }

  return (
    <div className="chat-panel">
      <div className="chat-history" ref={scrollRef}>
        {messages.length === 0 && (
          <div className="chat-empty">
            <h2>Ask about your contracts</h2>
            <p>Answers are drawn strictly from the ingested clauses, with clause-level citations.</p>
            <div className="chat-suggestions">
              {SUGGESTIONS.map(s => (
                <button key={s} onClick={() => ask(s)} disabled={disabled || asking}>{s}</button>
              ))}
            </div>
          </div>
        )}
        {messages.map(m => (
          <div key={m.id} className={`message message-${m.role}`}>
            {m.role === 'user' && <div className="message-bubble">{m.text}</div>}
            {m.role === 'assistant' && (
              <AssistantMessage message={m} />
            )}
          </div>
        ))}
        {asking && (
          <div className="message message-assistant">
            <div className="message-bubble message-thinking">
              <span className="spinner" /> Retrieving clauses…
            </div>
          </div>
        )}
      </div>
      <form
        className="chat-input"
        onSubmit={e => { e.preventDefault(); ask(question) }}
      >
        <input
          value={question}
          onChange={e => setQuestion(e.target.value)}
          placeholder={disabled ? 'Ingest a contract first…' : 'Ask about terms, clauses, obligations…'}
          disabled={disabled || asking}
        />
        <button type="submit" disabled={disabled || asking || !question.trim()}>Ask</button>
      </form>
    </div>
  )
}

function AssistantMessage({ message }: Readonly<{ message: ChatMessage }>) {
  const r = message.response
  if (message.error) {
    return <div className="message-bubble message-error">{message.text}</div>
  }
  if (r?.insufficientContext) {
    return (
      <div className="message-bubble message-insufficient">
        <strong>Not enough context in the contracts</strong>
        <p>
          None of the ingested clauses ground an answer to this question, so no
          answer was generated.
        </p>
        <div className="message-meta">
          <ConfidenceBadge confidence="none" />
          {r && <span className="latency">{(r.latencyMs.total / 1000).toFixed(1)} s</span>}
        </div>
      </div>
    )
  }
  return (
    <div className="message-bubble">
      <div className="message-answer">{message.text}</div>
      {r && r.citations.length > 0 && (
        <div className="citations">
          {r.citations.map(c => (
            <CitationChip key={`${c.contractName}-${c.clauseNumber}-${c.pageNumber}`} citation={c} />
          ))}
        </div>
      )}
      {r && (
        <div className="message-meta">
          <ConfidenceBadge confidence={r.confidence} />
          <span className="latency">
            {(r.latencyMs.total / 1000).toFixed(1)} s
            <span className="latency-split"> · retrieval {r.latencyMs.retrieval} ms · generation {r.latencyMs.generation} ms</span>
          </span>
        </div>
      )}
    </div>
  )
}
