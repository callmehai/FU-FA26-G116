/** SCRUM-30 — the Secretary's running notes. Contract: docs/03-api/19-ghi-chu-cuoc-hop.md. */

export type NoteAuthor = { id: string; fullName: string }

export type MeetingNote = {
  /** null: the note on the meeting as a whole. */
  agendaItemId: string | null
  body: string
  updatedAt: string
  author: NoteAuthor
}

export type NotesState = {
  /** The current Secretary's notes: the meeting-wide note first, then in agenda order. */
  notes: MeetingNote[]
  /** The item the Chair has in progress; new text goes there unless the Secretary picks another. */
  currentAgendaItemId: string | null
  /** The caller is the current Secretary and the meeting still takes notes. The Chair reads with false. */
  canWrite: boolean
  /** A Secret meeting: unsent text is kept only until the page is closed, never in localStorage (BR-403). */
  secret: boolean
}

export type NoteSave = {
  agendaItemId: string | null
  body: string
  /** The `updatedAt` last read or saved; null for a note never saved. A mismatch is 409 MSG418. */
  updatedAt: string | null
}

/** `notesChanged` on /hubs/meetings: refetch; it carries no text. */
export type NotesChange = { eventId: string; meetingId: string; agendaItemId: string | null; updatedAt: string }

export const noteMaxLength = 20_000
