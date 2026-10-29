import type { AgendaItemStatus } from './agenda'

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

/**
 * The personal library (SCRUM-110, SCRUM-111). Contract in docs/03-api/14-kho-tai-lieu-ca-nhan.md.
 * Library IDs are **Document IDs** — not the MeetingDocument IDs above.
 */

export interface LibraryFolder {
  id: string
  name: string
  /** null at the library root. */
  parentId: string | null
  /** Set on the folder an upload into a meeting created (BR-192). */
  meetingId: string | null
  createdAt: string
}

export interface LibraryDocument {
  id: string
  fileName: string
  contentType: string
  byteSize: number
  pageCount: number | null
  folderId: string | null
  createdAt: string
  /** Meetings the reader can see that still use this file (BR-251). */
  meetingCount: number
}

/** GET /api/documents: every folder (for the trail and the move dialog), the documents at one place. */
export interface LibraryView {
  folderId: string | null
  folders: LibraryFolder[]
  documents: LibraryDocument[]
  /** The whole system's document storage, not this person's (ADR-0034): every upload must fit it (MSG346). */
  storageUsedBytes: number
  storageLimitBytes: number
}

export interface LibraryUploadResponse {
  code: string
  message: string
  addedCount: number
  documents: LibraryDocument[]
}

/** 201 of POST …/documents (from the library) and 200 of the agenda attach endpoints. */
export interface MeetingDocumentsAdded {
  code: string
  message: string
  meetingId: string
  agendaItemId?: string
  addedCount?: number
  documents: Pick<MeetingDocument, 'id' | 'fileName' | 'contentType' | 'byteSize' | 'pageCount'>[]
}

/** SCRUM-23: comments on one document of one meeting. */
export interface DocumentCommentAuthor {
  userId: string | null
  guestInvitationId: string | null
  name: string
  isGuest: boolean
}

export interface DocumentComment {
  id: string
  body: string
  /** PDF top-level comments only (BR-248). */
  pageNumber: number | null
  author: DocumentCommentAuthor
  createdAt: string
  /** One level (BR-317); always empty on a reply. */
  replies: DocumentComment[]
}

/** GET …/documents/{id}/comments. `canComment` is false once the meeting has ended or was cancelled. */
export interface DocumentCommentList {
  meetingDocumentId: string
  canComment: boolean
  comments: DocumentComment[]
}
