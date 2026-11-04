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

export const exportMinutes = (minutesId: string) => api.blob(`/api/minutes/${minutesId}/export`)
