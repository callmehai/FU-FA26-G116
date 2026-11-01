import { createContext, useCallback, useContext } from 'react'
import { routes } from '../../lib/routes'
import type { GuestSession } from '../../types/guest'

/**
 * A verified guest on one invited meeting (UC-04). Present only on the guest pages, so the shared
 * panels (agenda, documents, votes, roster) can tell they are showing a guest: their links stay on the
 * guest's own routes, and a refusal never sends anyone to the internal screens.
 */
export interface GuestScope {
  invitationId: string
  session: GuestSession
  /** Re-reads the session; once the backend says it is over, the meeting gives way to the gate. Stable. */
  recheck: () => void
}

export const GuestContext = createContext<GuestScope | null>(null)

export const useGuest = () => useContext(GuestContext)

/** Where a meeting's document opens: the guest's own viewer on a guest page, the internal one elsewhere. */
export function useDocumentPath() {
  const invitationId = useGuest()?.invitationId
  return useCallback(
    (meetingId: string, documentId: string) =>
      invitationId ? routes.guestDocument(invitationId, documentId) : routes.meetingDocument(meetingId, documentId),
    [invitationId],
  )
}

/** The meeting page a document's "‹ Tài liệu" returns to, on the given tab. */
export function useMeetingPagePath() {
  const invitationId = useGuest()?.invitationId
  return useCallback(
    (meetingId: string, tab: string) =>
      `${invitationId ? routes.guest(invitationId) : routes.meeting(meetingId)}?tab=${tab}`,
    [invitationId],
  )
}
