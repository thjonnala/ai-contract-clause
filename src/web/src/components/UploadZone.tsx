import { useRef, useState } from 'react'
import { api } from '../api'

export function UploadZone({ onUploaded }: Readonly<{ onUploaded: () => void }>) {
  const [dragging, setDragging] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const fileInput = useRef<HTMLInputElement>(null)

  async function handleFiles(files: FileList | null) {
    if (!files?.length) return
    setError('')
    setBusy(true)
    try {
      for (const file of Array.from(files)) {
        if (!file.name.toLowerCase().endsWith('.pdf')) {
          setError(`${file.name}: only PDF contracts are supported`)
          continue
        }
        await api.upload(file)
      }
      onUploaded()
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Upload failed')
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      {/* a real button gets focus + Enter/Space natively; the file input
          lives outside because interactive content can't nest in a button */}
      <button
        type="button"
        className={`upload-zone ${dragging ? 'upload-zone-drag' : ''}`}
        onDragOver={e => { e.preventDefault(); setDragging(true) }}
        onDragLeave={() => setDragging(false)}
        onDrop={e => { e.preventDefault(); setDragging(false); handleFiles(e.dataTransfer.files) }}
        onClick={() => fileInput.current?.click()}
      >
        <div className="upload-zone-icon">{busy ? '…' : '⇪'}</div>
        <div>{busy ? 'Uploading…' : 'Drop contract PDFs here or click to browse'}</div>
        {error && <div className="upload-error">{error}</div>}
      </button>
      <input
        ref={fileInput}
        type="file"
        accept=".pdf"
        multiple
        hidden
        onChange={e => { handleFiles(e.target.files); e.target.value = '' }}
      />
    </>
  )
}
