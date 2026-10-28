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
