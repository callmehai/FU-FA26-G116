import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { useDocumentPath } from '../guest/guestContext'
import { opensInPlace, useOpenDocument, type ViewerTarget } from './viewerOverlay'

/**
 * A meeting document's name, opening it in the in-app viewer over the current page. Its href is still the
 * viewer's full-screen route (`?back=` for the tab it returns to), so Ctrl/⌘-click and "Open in new tab" work.
 */
export function DocumentLink({
  meetingId,
  documentId,
  read = false,
  back,
  className,
  tooltip,
  children,
}: {
  meetingId: string
  documentId: string
  /** Opened from a vote still waiting on this reader (UC-42). */
  read?: boolean
  /** The meeting tab the full-screen route returns to; Tài liệu without one. */
  back?: 'agenda' | 'votes'
  className?: string
  tooltip?: string
  children: ReactNode
}) {
  const documentPath = useDocumentPath()
  const open = useOpenDocument()
  const query = new URLSearchParams()
  if (back) query.set('back', back)
  if (read) query.set('read', '1')
  const target: ViewerTarget = { kind: 'meeting', meetingId, documentId, read }
  return (
    <Link
      className={className}
      to={`${documentPath(meetingId, documentId)}${query.size ? `?${query}` : ''}`}
      data-tooltip={tooltip}
      onClick={(event) => {
        if (!open || !opensInPlace(event)) return
        event.preventDefault()
        open(target)
      }}
    >
      {children}
    </Link>
  )
}
