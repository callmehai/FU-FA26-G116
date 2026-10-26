import type { DocumentUploadResponse, MeetingDocumentList } from '../types/document'
import { api } from './client'

/**
 * Meeting documents. Contract in docs/03-api/11-tai-lieu-cuoc-hop.md.
 *
 * Reading follows the right to see the meeting (404 MSG156 otherwise, same body for every reason).
 * The content endpoint answers with the original bytes and `Content-Disposition: attachment`, and
 * the token lives in JS, so a document is never opened by URL: fetch the Blob and hand it to the
 * viewer, or to a `download` link.
 */

const base = (meetingId: string) => `/api/meetings/${meetingId}/documents`

/** UC-32a: every agenda item with its documents, plus the general ones. 500 MSG157 on a read error. */
export const getMeetingDocuments = (meetingId: string) => api.get<MeetingDocumentList>(base(meetingId))

/**
 * UC-32c: the original file. `download` is the AF-1 download — 403 MSG159 for a Secret meeting;
 * a view is audited for Confidential/Secret meetings. 503 MSG158 (view) / MSG160 (download): retry.
 */
export const getMeetingDocumentContent = (meetingId: string, meetingDocumentId: string, download = false) =>
  api.blob(`${base(meetingId)}/${meetingDocumentId}/content${download ? '?download=true' : ''}`)

/**
 * UC-33a AF-1: all files or none (BR-194). `pageCounts[i]` goes with `files[i]`, empty when unknown.
 * Chair or Secretary of an unended meeting only: 403 MSG162, 409 MSG172, 404 MSG174.
 */
export function uploadMeetingDocuments(
  meetingId: string,
  files: { file: File; pageCount: number | null }[],
  options: { onProgress?: (loaded: number, total: number) => void; signal?: AbortSignal } = {},
) {
  const form = new FormData()
  for (const { file, pageCount } of files) {
    form.append('files', file)
    form.append('pageCounts', pageCount === null ? '' : String(pageCount))
  }
  return api.upload<DocumentUploadResponse>(`${base(meetingId)}/upload`, form, options)
}
