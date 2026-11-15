import { useEffect, useRef } from 'react'
import { useToast } from '../../components/toastContext'
import type { Speaking } from './useSpeaking'

/**
 * Tells the Chair in the call room "… giơ tay" when a hand goes up while the speaking panel is closed, as Meet does
 * (UC-79 step 3). Hands already up when the room opens, or when the caller becomes Chair, are not announced: the
 * count on the "Phát biểu" button shows them.
 */
export function useRaisedHandAlerts(speaking: Speaking, panelOpen: boolean) {
  const toast = useToast()
  const known = useRef<Set<string> | null>(null)
  const { data } = speaking

  useEffect(() => {
    if (!data?.canManage) {
      known.current = null
      return
    }
    const queued = data.requests.filter((r) => r.status === 'Queued' && !r.isMine)
    const before = known.current
    known.current = new Set(queued.map((r) => r.id))
    if (!before || panelOpen) return
    const raised = queued.filter((r) => !before.has(r.id))
    if (raised.length === 1) toast.show(`${raised[0].displayName} giơ tay`, 'info')
    else if (raised.length > 1) toast.show(`${raised.length} người giơ tay`, 'info')
  }, [data, panelOpen, toast])
}
