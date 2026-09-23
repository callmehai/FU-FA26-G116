import type { ReactNode } from 'react'

/** Semantic tones for soft status chips, mapped to colours in global.css. */
export type BadgeTone =
  | 'slate'
  | 'amber'
  | 'red'
  | 'emerald'
  | 'blue'
  | 'indigo'
  | 'violet'
  | 'lacquer'
  | 'outline'

export function Badge({ tone = 'slate', children }: { tone?: BadgeTone; children: ReactNode }) {
  return <span className={`badge badge-${tone}`}>{children}</span>
}
