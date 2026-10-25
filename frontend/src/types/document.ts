/**
 * Meeting documents (SCRUM-107 upload, SCRUM-21 view). Contract in docs/03-api/11-tai-lieu-cuoc-hop.md.
 * Every `id` here is a **MeetingDocument ID** — the same one Agenda's `documentIds` carries.
 */

export const PDF_TYPE = 'application/pdf'
export const DOCX_TYPE = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'

/** One row of GET /api/meetings/{id}/documents. */
export interface MeetingDocument {
  id: string
  fileName: string
  /** Detected from the content at upload: `PDF_TYPE` or `DOCX_TYPE`. */
  contentType: string
  byteSize: number
  /** PDF only, counted by the uploader's browser; always null for DOCX. */
  pageCount: number | null
  /** Every comment and reply. */
  commentCount: number
  addedBy: { userId: string; fullName: string }
  addedAt: string
}

export type AgendaItemStatus = 'Pending' | 'InProgress' | 'Done'

export interface DocumentsAgendaItem {
  id: string
  position: number
  title: string
  status: AgendaItemStatus
  /** A document linked to several items appears under each (BR-277). */
  documents: MeetingDocument[]
}

/** GET /api/meetings/{id}/documents (UC-32a). Removed documents are never listed (BR-211). */
export interface MeetingDocumentList {
  meetingId: string
  /** The item in progress, or null; the call room puts it first (BR-316). */
  currentAgendaItemId: string | null
  /** Every agenda item by position, those without documents included. */
  agendaItems: DocumentsAgendaItem[]
  /** Documents linked to no item (BR-280). */
  general: MeetingDocument[]
}

/** 201 of POST /api/meetings/{id}/documents/upload: MSG161. */
export interface DocumentUploadResponse {
  code: string
  message: string
  meetingId: string
  addedCount: number
  documents: Pick<MeetingDocument, 'id' | 'fileName' | 'contentType' | 'byteSize' | 'pageCount'>[]
}

/** Hub `documentsChanged`: only says "re-read the list". */
export interface DocumentsChangedEvent {
  eventId: string
  meetingId: string
  occurredAt: string
}
