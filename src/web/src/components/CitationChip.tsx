import { useState } from 'react'
import type { Citation } from '../types'

/** Clause-level citation: click to expand the exact clause text + page number. */
export function CitationChip({ citation }: Readonly<{ citation: Citation }>) {
  const [open, setOpen] = useState(false)
  const label = citation.clauseNumber
    ? `${citation.contractName} — Clause ${citation.clauseNumber}`
    : `${citation.contractName} — ${citation.clauseTitle}`
  return (
    <div className={`citation ${open ? 'citation-open' : ''}`}>
      <button className="citation-chip" onClick={() => setOpen(!open)} title="Show clause text">
        <span className="citation-icon">§</span>
        {label}
        <span className="citation-page">p. {citation.pageNumber}</span>
      </button>
      {open && (
        <div className="citation-detail">
          <div className="citation-detail-title">
            {citation.clauseNumber ? `${citation.clauseNumber}. ` : ''}
            {citation.clauseTitle}
            <span className="citation-detail-page">page {citation.pageNumber}</span>
          </div>
          <p>{citation.excerpt}</p>
        </div>
      )}
    </div>
  )
}
