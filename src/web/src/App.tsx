import { useCallback, useEffect, useState } from 'react'
import './App.css'
import { api } from './api'
import type { ContractInfo } from './types'
import { UploadZone } from './components/UploadZone'
import { ContractList } from './components/ContractList'
import { ChatPanel } from './components/ChatPanel'

export default function App() {
  const [contracts, setContracts] = useState<ContractInfo[]>([])
  const [seeding, setSeeding] = useState(false)

  const refresh = useCallback(() => {
    api.contracts().then(setContracts).catch(() => { /* API warming up */ })
  }, [])

  // poll while anything is processing so the async pipeline is visible live
  const anyProcessing = contracts.some(c => c.status === 'Processing')
  useEffect(() => {
    refresh()
    const interval = setInterval(refresh, anyProcessing ? 2500 : 8000)
    return () => clearInterval(interval)
  }, [refresh, anyProcessing])

  async function seed() {
    setSeeding(true)
    try {
      await api.seed()
      refresh()
    } finally {
      setSeeding(false)
    }
  }

  const anyReady = contracts.some(c => c.status === 'Ready')

  return (
    <div className="app">
      <header className="app-header">
        <h1>Contract Clause Intelligence</h1>
        <span className="app-tagline">
          Hybrid retrieval · semantic reranking · clause-anchored answers
        </span>
      </header>
      <main className="app-main">
        <aside className="sidebar">
          <UploadZone onUploaded={refresh} />
          <ContractList contracts={contracts} onSeed={seed} seeding={seeding} />
        </aside>
        <ChatPanel disabled={!anyReady} />
      </main>
    </div>
  )
}
