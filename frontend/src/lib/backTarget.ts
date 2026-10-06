import { useLocation, type Location } from 'react-router-dom'

/** Where a record page's back link leads, and what it says. */
export interface BackTarget {
  to: string
  label: string
}

interface BackState {
  back?: unknown
}

/**
 * Router state for a link into a record page, remembering the screen it was opened from — path
 * and query, so the calendar comes back on the same week and view, and a list on the same
 * filters and page. Pass it as `<Link state={…}>` or `navigate(to, { state })`.
 */
export function backState(label: string, from: Pick<Location, 'pathname' | 'search'>): BackState {
  return { back: { to: from.pathname + from.search, label } satisfies BackTarget }
}

/**
 * The back target this page was opened with, or `fallback` when there is none — a notification,
 * a typed URL, a reload in a new tab. Only an in-app path is accepted, never another origin.
 */
export function useBackTarget(fallback: BackTarget): BackTarget {
  const state = useLocation().state as BackState | null
  const back = state?.back as Partial<BackTarget> | undefined
  if (
    typeof back?.to === 'string' &&
    typeof back.label === 'string' &&
    back.to.startsWith('/') &&
    !back.to.startsWith('//')
  ) {
    return { to: back.to, label: back.label }
  }
  return fallback
}

/** Forwards the back target a page was opened with, for a link that goes one level deeper. */
export function useForwardedBackState(): BackState | undefined {
  const state = useLocation().state as BackState | null
  return state?.back ? { back: state.back } : undefined
}
