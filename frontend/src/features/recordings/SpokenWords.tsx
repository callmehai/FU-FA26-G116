/**
 * A transcript sentence read along with its voice, karaoke-style: while it plays, the word being said is marked.
 *
 * The word times are the speech worker's own (`wordStartsMs`, one per word of the text). A sentence without them — said
 * before the worker measured words, or corrected by the Secretary since — is followed as a whole sentence only; its
 * words are never timed by spreading the sentence's length over them.
 *
 * Words the recogniser was not sure of (`wordConfidence` under {@link DOUBTFUL_WORD}, team test 21/11/2026) get a pale
 * yellow wash, so the Secretary knows where to listen again before correcting the line.
 */

/**
 * Below this sureness (per cent) a word is washed as doubtful: 6 % of the words of the team's room recording (PO 22/11/2026;
 * 50 marked 12 %, too many correct words among them).
 */
const DOUBTFUL_WORD = 40

/** The word being said at `ms`: the last one starting at or before it, or -1 before the first. */
function wordAt(starts: readonly number[], ms: number) {
  let found = -1
  for (let i = 0; i < starts.length && starts[i] <= ms; i++) found = i
  return found
}

/** The words of `text` (split on whitespace, as the server counts them) with the spaces between them kept. */
const parts = (text: string) => text.split(/(\s+)/)

export function SpokenWords({
  text,
  starts,
  confidence,
  nowMs,
  marked,
  markClass,
}: {
  text: string
  /** When each word begins, on the same clock as `nowMs`; ignored unless there is one per word. */
  starts?: readonly number[] | null
  /** How sure the recogniser was of each word, in per cent; ignored unless there is one per word. */
  confidence?: readonly number[] | null
  /** Where playback is within this sentence, or null when it is not the one being heard. */
  nowMs: number | null
  /** Words to mark for another reason (another mic's speaker overheard), by their index. */
  marked?: (word: number) => boolean
  markClass?: string
}) {
  const split = parts(text)
  const words = split.filter((p) => p.trim()).length
  const now = nowMs !== null && starts && starts.length === words ? wordAt(starts, nowMs) : -1
  const sure = confidence && confidence.length === words ? confidence : null
  const doubtful = (word: number) => sure !== null && sure[word] < DOUBTFUL_WORD
  if (now < 0 && !marked && !sure?.some((c) => c < DOUBTFUL_WORD)) return <>{text}</>
  let index = -1
  return (
    <>
      {split.map((part, i) => {
        if (!part.trim()) return part
        index++
        const classes = [index === now ? 'tl-word is-now' : '', doubtful(index) ? 'tl-word-unsure' : '']
          .filter(Boolean)
          .join(' ')
        if (marked?.(index))
          return (
            <mark key={i} className={[markClass ?? '', classes].filter(Boolean).join(' ')}>
              {part}
            </mark>
          )
        return classes ? (
          <span key={i} className={classes}>
            {part}
          </span>
        ) : (
          part
        )
      })}
    </>
  )
}
