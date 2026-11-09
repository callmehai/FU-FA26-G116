import type { MinutesState, SummaryEdit, SummaryState } from '../types/summary'
import { api } from './client'

/**
 * The AI draft (SCRUM-36) and the draft minutes (SCRUM-32). Contract in docs/03-api/17-du-thao-bien-ban.md.
 * Asking for a draft only queues it (202, MSG384): poll `getSummary` until its status is Ready or Failed.
 */

const summary = (meetingId: string) => `/api/meetings/${meetingId}/summary`

export const getSummary = (meetingId: string) => api.get<SummaryState>(summary(meetingId))

export const requestSummary = (meetingId: string) => api.post<SummaryState & { code: string; message: string }>(summary(meetingId))

export const editSummary = (meetingId: string, edit: SummaryEdit) =>
  api.put<SummaryState & { code: string; message: string }>(summary(meetingId), edit)

export const reviewSummary = (meetingId: string) =>
  api.post<SummaryState & { code: string; message: string }>(`${summary(meetingId)}/review`)

export const getMinutes = (meetingId: string) => api.get<MinutesState>(`/api/meetings/${meetingId}/minutes`)

/** `rebuild` replaces an existing draft, edits and all; the page asks first. */
export const createMinutes = (meetingId: string, rebuild: boolean) =>
  api.post<MinutesState & { code: string; message: string }>(`/api/meetings/${meetingId}/minutes`, { rebuild })

export const saveMinutes = (minutesId: string, content: string, updatedAt: string) =>
  api.put<MinutesState & { code: string; message: string }>(`/api/minutes/${minutesId}`, { content, updatedAt })

/** One revision with the same state around it, to open a superseded one from the history (UC-58). */
export const getMinutesRevision = (minutesId: string) => api.get<MinutesState>(`/api/minutes/${minutesId}`)

type MinutesResult = MinutesState & { code: string; message: string }

/** SCRUM-33 — UC-61: the Secretary sends a draft or a returned revision to the Chair. */
export const submitMinutes = (minutesId: string, updatedAt: string) =>
  api.post<MinutesResult>(`/api/minutes/${minutesId}/submit`, { updatedAt })

/** UC-64: the Chair approves, which issues the minutes at once. */
export const approveMinutes = (minutesId: string, updatedAt: string) =>
  api.post<MinutesResult>(`/api/minutes/${minutesId}/approve`, { updatedAt })

/** UC-65: the Chair sends them back to the Secretary with a reason (at most 2,000 characters). */
export const returnMinutes = (minutesId: string, reason: string, updatedAt: string) =>
  api.post<MinutesResult>(`/api/minutes/${minutesId}/reject`, { reason, updatedAt })

/** UC-83: a new draft revision that starts from the issued text. */
export const correctMinutes = (minutesId: string) => api.post<MinutesResult>(`/api/minutes/${minutesId}/corrections`)

/** The minutes as a Word or a PDF file, made on the server from the same text. */
export const exportMinutes = (minutesId: string, format: 'docx' | 'pdf') =>
  api.blob(`/api/minutes/${minutesId}/export?format=${format}`)
