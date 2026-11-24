import type {
  DocumentComment,
  DocumentCommentList,
  DocumentUploadResponse,
  MeetingDocumentList,
  MeetingDocumentsAdded,
} from '../types/document'
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

/** UC-35a: every agenda item with its documents, plus the general ones. 500 MSG157 on a read error. */
export const getMeetingDocuments = (meetingId: string) => api.get<MeetingDocumentList>(base(meetingId))

/**
 * UC-35b: the original file. `download` is the AF-1 download — 403 MSG159 for a Secret meeting;
 * a view is audited for Secret meetings. 503 MSG158 (view) / MSG160 (download): retry.
 */
export const getMeetingDocumentContent = (meetingId: string, meetingDocumentId: string, download = false) =>
  api.blob(`${base(meetingId)}/${meetingDocumentId}/content${download ? '?download=true' : ''}`)

/**
 * UC-36a AF-1: all files or none (UC-36a). The server counts a PDF's pages itself (ADR-0044).
 * Chair or Secretary of an unended meeting only: 403 MSG162, 409 MSG172, 404 MSG174.
 */
export function uploadMeetingDocuments(meetingId: string, files: Upload, options: UploadOptions = {}) {
  return api.upload<DocumentUploadResponse>(`${base(meetingId)}/upload`, filesForm(files), options)
}

export type Upload = File[]
export type UploadOptions = { onProgress?: (loaded: number, total: number) => void; signal?: AbortSignal }

/** The multipart body every document upload sends: one `files` part per file. */
export function filesForm(files: Upload) {
  const form = new FormData()
  for (const file of files) form.append('files', file)
  return form
}

/**
 * SCRUM-58. Every change below is the current Chair's or Secretary's until the meeting ends (409, code per action)
 * or is cancelled (409 MSG349); 404 MSG174 when the meeting can no longer be read. All or nothing.
 */

/** UC-36a: library documents of the caller into the general group; 400 MSG249, 409 MSG165 already there. */
export const addFromLibrary = (meetingId: string, documentIds: string[]) =>
  api.post<MeetingDocumentsAdded>(base(meetingId), { documentIds })

/** UC-36c: comments stay as history but are no longer shown (BR-074–BR-076). 403 MSG169, 409 MSG248, 404 MSG170. */
export const removeMeetingDocument = (meetingId: string, meetingDocumentId: string) =>
  api.delete<{ code: string; message: string }>(`${base(meetingId)}/${meetingDocumentId}`)

const itemBase = (meetingId: string, itemId: string) => `/api/meetings/${meetingId}/agenda/${itemId}/documents`

/** UC-36b main flow: documents already in the meeting. 409 MSG243 when one is already linked to this item. */
export const linkToAgendaItem = (meetingId: string, itemId: string, meetingDocumentIds: string[]) =>
  api.post<MeetingDocumentsAdded>(itemBase(meetingId, itemId), { documentIds: meetingDocumentIds })

/**
 * UC-36b AF-2: library documents, added to the meeting and linked in one go; one already in the meeting is linked as it
 * is. 400 MSG249, 409 MSG243 already linked, 503 MSG166.
 */
export const linkFromLibrary = (meetingId: string, itemId: string, documentIds: string[]) =>
  api.post<MeetingDocumentsAdded>(`${itemBase(meetingId, itemId)}/from-library`, { documentIds })

/** UC-36b AF-1: files from the computer, as the meeting upload. */
export const uploadToAgendaItem = (meetingId: string, itemId: string, files: Upload, options: UploadOptions = {}) =>
  api.upload<MeetingDocumentsAdded>(`${itemBase(meetingId, itemId)}/upload`, filesForm(files), options)

/** UC-36d: this one link; the document stays in the meeting (UC-36b, UC-36d). 404 MSG246 when it was not linked. */
export const unlinkFromAgendaItem = (meetingId: string, itemId: string, meetingDocumentId: string) =>
  api.delete<{ code: string; message: string }>(`${itemBase(meetingId, itemId)}/${meetingDocumentId}`)

/** SCRUM-23 (UC-38): anyone who can read the meeting; 404 MSG156 otherwise, 500 MSG204 on a read error. */
export const getDocumentComments = (meetingId: string, meetingDocumentId: string) =>
  api.get<DocumentCommentList>(`${base(meetingId)}/${meetingDocumentId}/comments`)

/**
 * 400 MSG200 blank, MSG348 past 4,000 characters; 409 MSG251 ended, MSG349 cancelled; 503 MSG202.
 * A reply to a reply joins the top-level thread; `pageNumber` is kept on a top-level PDF comment only.
 */
export const postDocumentComment = (
  meetingId: string,
  meetingDocumentId: string,
  body: { body: string; pageNumber: number | null; parentCommentId: string | null },
) =>
  api.post<{ code: string; message: string; comment: DocumentComment }>(
    `${base(meetingId)}/${meetingDocumentId}/comments`,
    body,
  )
