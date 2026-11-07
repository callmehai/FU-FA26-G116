import type { MeetingNote, NoteSave, NotesState } from '../types/note'
import { api } from './client'

/** The Secretary's running notes (SCRUM-30). Contract in docs/03-api/19-ghi-chu-cuoc-hop.md. */

const notes = (meetingId: string) => `/api/meetings/${meetingId}/notes`

export const getNotes = (meetingId: string) => api.get<NotesState>(notes(meetingId))

export const saveNote = (meetingId: string, save: NoteSave) =>
  api.put<{ code: string; message: string; note: MeetingNote }>(notes(meetingId), save)
