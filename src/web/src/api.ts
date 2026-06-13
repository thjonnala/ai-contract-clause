import type { ContractInfo, QueryResponse } from './types'

async function json<T>(response: Response): Promise<T> {
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`)
  return response.json() as Promise<T>
}

export const api = {
  contracts: () => fetch('/api/contracts').then(r => json<ContractInfo[]>(r)),

  upload: (file: File) => {
    const form = new FormData()
    form.append('file', file)
    return fetch('/api/upload', { method: 'POST', body: form }).then(r => json<unknown>(r))
  },

  seed: () => fetch('/api/seed', { method: 'POST' }).then(r => json<unknown>(r)),

  query: (question: string) =>
    fetch('/api/query', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question }),
    }).then(r => json<QueryResponse>(r)),
}
