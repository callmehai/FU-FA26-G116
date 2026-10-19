import { useEffect, useRef, useState } from 'react'

/** Long enough that typing a word sends one request, short enough to feel live. */
const SEARCH_DELAY_MS = 300

/**
 * The search box keeps what is typed locally and hands it on after a pause. When the URL changes
 * from elsewhere — Back, "Xoá bộ lọc" — the box follows it.
 */
export function useSearchBox(committed: string, commit: (value: string) => void) {
  const [value, setValue] = useState(committed)
  const commitRef = useRef(commit)
  const lastCommitted = useRef(committed)

  useEffect(() => {
    commitRef.current = commit
  })

  // Follow the URL when it changes without the box being the cause.
  useEffect(() => {
    if (committed !== lastCommitted.current) {
      lastCommitted.current = committed
      setValue(committed)
    }
  }, [committed])

  useEffect(() => {
    if (value.trim() === lastCommitted.current.trim()) return
    const timer = setTimeout(() => {
      lastCommitted.current = value.trim()
      commitRef.current(value)
    }, SEARCH_DELAY_MS)
    return () => clearTimeout(timer)
  }, [value])

  return [value, setValue] as const
}
