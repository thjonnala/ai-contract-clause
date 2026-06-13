import type { ContractInfo } from '../types'

export function ContractList({ contracts, onSeed, seeding }: Readonly<{
  contracts: ContractInfo[]
  onSeed: () => void
  seeding: boolean
}>) {
  return (
    <div className="contract-list">
      <div className="contract-list-header">
        <h2>Contract portfolio</h2>
        <button className="seed-button" onClick={onSeed} disabled={seeding}>
          {seeding ? 'Seeding…' : 'Load samples'}
        </button>
      </div>
      {contracts.length === 0 && (
        <p className="contract-list-empty">
          No contracts yet — upload a PDF or load the samples.
        </p>
      )}
      {contracts.map(c => (
        <div key={c.id} className={`contract-row contract-${c.status.toLowerCase()}`}>
          <div className="contract-name" title={c.name}>{c.name}</div>
          <div className="contract-status">
            {c.status === 'Processing' && <><span className="spinner" /> Processing…</>}
            {c.status === 'Ready' && <>Ready · {c.clauseCount} clauses</>}
            {c.status === 'Failed' && <span title={c.error}>Failed</span>}
          </div>
        </div>
      ))}
    </div>
  )
}
