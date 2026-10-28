import { useCallback, useState } from 'react'
import { getMeetingDocumentContent } from '../../api/documents'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { useAccessLost } from '../auth/useAccessLost'
import { saveBlob } from './documentFile'

/**
 * UC-32c AF-1: download the original file through the API (the token is in JS, so never by URL).
 * `onStale` re-reads what the screen shows when the answer says it is out of date: MSG159 means the
 * meeting has just become Secret, MSG156 that the document is gone or no longer readable.
 */
export function useDocumentDownload(meetingId: string, onStale: () => void) {
  const toast = useToast()
  const handleAccessLost = useAccessLost()
  const [busyId, setBusyId] = useState<string | null>(null)

  const download = useCallback(
    async (doc: { id: string; fileName: string }) => {
      setBusyId(doc.id)
      try {
        saveBlob(await getMeetingDocumentContent(meetingId, doc.id, true), doc.fileName)
      } catch (err) {
        // MSG159 is a 403 about this file, not about the account: handle it before useAccessLost.
        if (isApiError(err, 403, 'MSG159') || isApiError(err, 404, 'MSG156')) {
          toast.show(errorMessage(err), 'error')
          onStale()
        } else if (!handleAccessLost(err)) {
          toast.show(errorMessage(err), 'error')
        }
      } finally {
        setBusyId(null)
      }
    },
    [meetingId, onStale, toast, handleAccessLost],
  )

  return { download, busyId }
}
