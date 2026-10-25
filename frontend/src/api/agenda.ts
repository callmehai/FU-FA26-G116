import type { AgendaEditorOptions, AgendaItemInput, AgendaMutationResponse, AgendaSnapshot } from '../types/agenda'
import { api } from './client'

/**
 * Meeting agenda (SCRUM-25, SCRUM-98, UC-77). Contract in docs/03-api/08-chuong-trinh-nghi-su.md.
 *
 * Every write answers only `{ code, message }`: re-read the snapshot afterwards. The snapshot's
 * `actions` and `permissions` say what the caller may do; the server re-checks on every write.
 */

const base = (meetingId: string) => `/api/meetings/${meetingId}/agenda`

export const getAgenda = (meetingId: string) => api.get<AgendaSnapshot>(base(meetingId))

/** Presenters and documents to pick from. The Secretary, while the agenda can still be planned. */
export const getAgendaEditorOptions = (meetingId: string) =>
  api.get<AgendaEditorOptions>(`${base(meetingId)}/editor-options`)

export const createAgendaItem = (meetingId: string, input: AgendaItemInput) =>
  api.post<AgendaMutationResponse>(base(meetingId), input)

export const updateAgendaItem = (meetingId: string, itemId: string, input: AgendaItemInput) =>
  api.put<AgendaMutationResponse>(`${base(meetingId)}/${itemId}`, input)

export const deleteAgendaItem = (meetingId: string, itemId: string) =>
  api.delete<AgendaMutationResponse>(`${base(meetingId)}/${itemId}`)

/** Every item's id in the new order, with the `orderVersion` of the snapshot it was decided on. */
export const reorderAgenda = (meetingId: string, orderVersion: string, orderedItemIds: string[]) =>
  api.put<AgendaMutationResponse>(`${base(meetingId)}/order`, { orderVersion, orderedItemIds })

/** Chair only, meeting in progress: makes a Pending item the current one. */
export const startAgendaItem = (meetingId: string, itemId: string) =>
  api.post<AgendaMutationResponse>(`${base(meetingId)}/${itemId}/current`)

/** Chair only: closes the current item. */
export const completeAgendaItem = (meetingId: string, itemId: string) =>
  api.post<AgendaMutationResponse>(`${base(meetingId)}/${itemId}/complete`)

/** UC-77, Chair or Secretary: 1–2,000 characters after trimming; locked once minutes exist. */
export const saveAgendaConclusion = (meetingId: string, itemId: string, conclusion: string) =>
  api.put<AgendaMutationResponse>(`${base(meetingId)}/${itemId}/conclusion`, { conclusion })
