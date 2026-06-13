import type { Confidence } from '../types'

const LABELS: Record<Confidence, string> = {
  high: 'High confidence',
  medium: 'Medium confidence',
  low: 'Low confidence',
  none: 'No grounding',
}

export function ConfidenceBadge({ confidence }: Readonly<{ confidence: Confidence }>) {
  return <span className={`confidence-badge confidence-${confidence}`}>{LABELS[confidence]}</span>
}
