import type { TranscriptSpeaker } from '../../types/transcript'

/** Diarization labels as people read them. A label belongs to one recording only — it is not a person. */

/** "SPEAKER_00" → "Người nói 1". */
export function speakerName(label: string) {
  const match = /^SPEAKER_(\d+)$/.exec(label)
  return match ? `Người nói ${Number(match[1]) + 1}` : 'Chưa rõ người nói'
}

/**
 * The eight colours a record tells its speakers apart by, as AI-eCabinet does: a dot on the rail and the name in the
 * matching ink (4.5:1 on white). Ordered so neighbours sit far apart; red and the live green are left out.
 */
export const SPEAKER_SLOTS = 8

export function speakerSlot(index: number) {
  const n = (((index % SPEAKER_SLOTS) + SPEAKER_SLOTS) % SPEAKER_SLOTS) + 1
  return { dot: `var(--speaker-${n}-dot)`, ink: `var(--speaker-${n}-ink)` }
}

/** "SPEAKER_n" of a room recording takes colour n; anything else the last one. */
export function speakerSlotOf(label: string) {
  return speakerSlot(Number(/^SPEAKER_(\d+)$/.exec(label)?.[1] ?? SPEAKER_SLOTS - 1))
}

/** The voice's dot, the same in the voice list and on the transcript's rail. */
export function speakerColor(label: string) {
  return speakerSlotOf(label).dot
}

/**
 * Each voice's label → the first label of the same person, as AI-eCabinet's voiceOf: two voices the Secretary gave one
 * name are one speaker, with one cell, one colour and one run of lines, however the machine split them. So is a voice
 * the machine heard as the same enrolled person as a surer one (`sameAsLabel`, PO 29/10/2026): it is gathered under
 * that voice at once rather than left for the Secretary to join; picking a name names them all.
 */
export function personOf(speakers: readonly TranscriptSpeaker[]): Map<string, string> {
  const first = new Map<string, string>()
  const out = new Map<string, string>()
  const byLabel = new Map(speakers.map((s) => [s.label, s]))
  const keyOf = (s: TranscriptSpeaker): string => {
    if (s.assignedUserId) return `p:${s.assignedUserId}`
    const lead = s.sameAsLabel ? byLabel.get(s.sameAsLabel) : undefined
    return lead && lead !== s && !lead.sameAsLabel ? keyOf(lead) : `l:${s.label}`
  }
  for (const s of [...speakers].sort((a, b) => a.label.localeCompare(b.label))) {
    const key = keyOf(s)
    if (!first.has(key)) first.set(key, s.label)
    out.set(s.label, first.get(key)!)
  }
  return out
}

/** One speaker as read: the first of its voices, and every voice named as the same person. */
export type Voice = { speaker: TranscriptSpeaker; labels: string[]; speechMs: number }

export function voicesOf(speakers: readonly TranscriptSpeaker[]) {
  const one = personOf(speakers)
  const voices = new Map<string, Voice>()
  for (const s of [...speakers].sort((a, b) => a.label.localeCompare(b.label))) {
    const head = one.get(s.label)!
    const voice = voices.get(head)
    if (voice) {
      voice.labels.push(s.label)
      voice.speechMs += s.speechMs
    } else voices.set(head, { speaker: s, labels: [s.label], speechMs: s.speechMs })
  }
  return { voices, one: (label: string) => one.get(label) ?? label }
}

/** How a voice reads: the name the Secretary gave it, else "Người nói N". */
export const voiceName = (speaker: Pick<TranscriptSpeaker, 'label' | 'assignedName'>) =>
  speaker.assignedName ?? speakerName(speaker.label)
