import type { MeetingStatus } from './meeting'

/** Mirrors the backend's AgendaContracts (SCRUM-25, SCRUM-98). Contract: docs/03-api/08-chuong-trinh-nghi-su.md. */

export type AgendaItemStatus = 'Pending' | 'InProgress' | 'Done'

export interface AgendaItemActions {
  canEdit: boolean
  /** Alias of `canEdit` since SCRUM-98: every field is editable in every item status. */
  canEditPlanningFields: boolean
  canDelete: boolean
  canSetCurrent: boolean
  canComplete: boolean
  canRecordConclusion: boolean
}

export interface AgendaItem {
  id: string
  position: number
  title: string
  description: string
  /** At most one of the two is set; both null when nobody presents or the assignment lapsed. */
  presenterId: string | null
  presenterGuestInvitationId: string | null
  presenterName: string | null
  /** A free-text name from before SCRUM-98, or a presenter who left the meeting: history only. */
  legacyPresenterName: string | null
  status: AgendaItemStatus
  actualStartedAt: string | null
  actualEndedAt: string | null
  conclusion: string | null
  conclusionUpdatedById: string | null
  conclusionUpdatedByName: string | null
  conclusionUpdatedAt: string | null
  canReadDocuments: boolean
  documents: { id: string; title: string }[]
  actions: AgendaItemActions
}

export interface AgendaSnapshot {
  meetingId: string
  meetingStatus: MeetingStatus
  plannedStartTime: string
  plannedEndTime: string
  serverTime: string
  /** Sent back with a reorder; a stale one answers 409 MSG143. */
  orderVersion: string
  permissions: { canCreate: boolean; canReorder: boolean }
  items: AgendaItem[]
}

export interface AgendaPresenterOption {
  /** A user id for `Internal`, a guest invitation id for `Guest`. */
  id: string
  kind: 'Internal' | 'Guest'
  fullName: string
}

export interface AgendaEditorOptions {
  presenters: AgendaPresenterOption[]
  /** The meeting's documents; one may already sit under other items, which does not stop linking it. */
  documents: { id: string; title: string; agendaItemIds: string[] }[]
}

export interface AgendaItemInput {
  title: string
  description: string
  presenterId: string | null
  presenterGuestInvitationId: string | null
  documentIds: string[]
}

export interface AgendaMutationResponse {
  code: string
  message: string
  meetingId: string
  agendaItemId?: string
}
