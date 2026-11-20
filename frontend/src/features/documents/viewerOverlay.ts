import { createContext, useContext, type MouseEvent } from 'react'

/** What the in-app viewer shows: a meeting's document, or one of a library's (one's own, or a deactivated person's). */
export type ViewerTarget =
  | {
      kind: 'meeting'
      meetingId: string
      documentId: string
      /** Opened from a vote still waiting on this reader (UC-74): the viewer offers the read confirmation. */
      read?: boolean
    }
  | { kind: 'library'; documentId: string; folderId: string | null; userId?: string }

/** Opens the viewer over the current page; null outside a provider, where a link simply follows its route. */
export const DocumentOverlayContext = createContext<((target: ViewerTarget) => void) | null>(null)

export const useOpenDocument = () => useContext(DocumentOverlayContext)

/**
 * Fired on `window` when the viewer closes, as the browser fired `focus` when the viewer was a tab of its own:
 * a panel that waits on what the reader did there (a vote's read confirmation) re-reads.
 */
export const VIEWER_CLOSED_EVENT = 'trustcabinet:viewer-closed'

/** The key in the history entry's state that holds the open document, so Back closes the viewer. */
export const VIEWER_STATE_KEY = 'viewer'

/** A plain click opens the viewer over the page; a click meant for a new tab or window keeps the link's own route. */
export function opensInPlace(event: MouseEvent) {
  return event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey
}
