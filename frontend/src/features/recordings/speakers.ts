import type { BadgeTone } from '../../components/Badge'

/** Diarization labels as people read them. A label belongs to one recording only — it is not a person. */

// Six chips that read apart at a glance (indigo looked like blue); red stays for "Mật" and warnings.
const SPEAKER_TONES: BadgeTone[] = ['blue', 'violet', 'emerald', 'amber', 'slate', 'outline']

/** "SPEAKER_00" → "Người nói 1". */
export function speakerName(label: string) {
  const match = /^SPEAKER_(\d+)$/.exec(label)
  return match ? `Người nói ${Number(match[1]) + 1}` : 'Chưa rõ người nói'
}

export function speakerTone(label: string): BadgeTone {
  const match = /^SPEAKER_(\d+)$/.exec(label)
  return match ? SPEAKER_TONES[Number(match[1]) % SPEAKER_TONES.length] : 'outline'
}

/** The ink of the voice's chip, for a dot that ties a picker to the segments it names. */
export function speakerColor(label: string) {
  const tone = speakerTone(label)
  return tone === 'outline' ? 'var(--ink)' : `var(--chip-${tone}-fg)`
}
