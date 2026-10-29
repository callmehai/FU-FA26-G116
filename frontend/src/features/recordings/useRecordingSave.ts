import { useCallback } from 'react'
import { uploadRecording } from '../../api/recordings'
import { isApiError } from '../../lib/errors'
import { routes } from '../../lib/routes'
import type { RecordingSource } from '../../types/recording'
import { useUploads } from '../uploads/uploadsContext'

export const DEFAULT_PURPOSE = 'Lập biên bản cuộc họp'

/** A picked file, for the upload queue. */
export interface PendingUpload {
  blob: Blob
  fileName: string
  source: RecordingSource
  durationMs?: number
  purpose: string
}

/** The meeting ended for recording, or the Secretary role moved: the list says so once re-read. */
const isExpected = (err: unknown) => isApiError(err, 409, 'MSG294') || isApiError(err, 403, 'MSG295')

function saveToComputer(upload: PendingUpload) {
  const url = URL.createObjectURL(upload.blob)
  const link = document.createElement('a')
  link.href = url
  link.download = upload.fileName
  link.click()
  window.setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/** The upload target the recordings list listens on, to re-read when a save settles. */
export const recordingsTarget = (meetingId: string) => `recordings:${meetingId}`

/**
 * Saving goes through the upload queue: it goes on with the panel closed or the page left, a network hiccup is tried
 * again, and a file that still fails stays in the tray with "Lưu về máy", so it is never lost. The room mic streams
 * its takes instead (ADR-0041).
 */
export function useRecordingSave(meetingId: string) {
  const uploads = useUploads()
  return useCallback(
    (upload: PendingUpload) =>
      uploads.enqueue([
        {
          name: upload.fileName,
          kind: 'audio',
          where: 'Bản ghi cuộc họp',
          href: `${routes.meeting(meetingId)}?tab=recordings`,
          target: recordingsTarget(meetingId),
          isExpected,
          keep: () => saveToComputer(upload),
          send: (options) =>
            uploadRecording(
              meetingId,
              {
                file: upload.blob,
                fileName: upload.fileName,
                source: upload.source,
                consentPurpose: upload.purpose,
                durationMs: upload.durationMs,
              },
              options,
            ),
        },
      ]),
    [meetingId, uploads],
  )
}
