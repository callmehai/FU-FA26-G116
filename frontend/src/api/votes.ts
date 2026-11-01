import type {
  DocumentReadConfirmed,
  VoteClosedResponse,
  VoteConclusionValue,
  VoteDetail,
  VoteInput,
  VoteList,
  VoteDocumentsAttached,
  VoteMutationResponse,
} from '../types/vote'
import { api } from './client'
import { filesForm, type Upload, type UploadOptions } from './documents'

/**
 * Votes during a meeting (SCRUM-28, UC-52a–UC-57). Contract in docs/03-api/15-bieu-quyet.md.
 *
 * Every write answers only a code and a message: re-read the list (or wait for `voteChanged`) to see
 * the new state. A vote the caller may not see answers 404 MSG205, the same as one that does not exist.
 */

const base = (meetingId: string) => `/api/meetings/${meetingId}/votes`

export const getVotes = (meetingId: string) => api.get<VoteList>(base(meetingId))

export const getVote = (meetingId: string, voteId: string) => api.get<VoteDetail>(`${base(meetingId)}/${voteId}`)

/** 201 MSG210 as a draft; 200 MSG217 with `openImmediately`, and nothing is saved when opening fails. */
export const createVote = (meetingId: string, input: VoteInput) =>
  api.post<VoteMutationResponse>(base(meetingId), input)

/** 200 MSG211; with `openImmediately` 200 MSG217, and a failed open keeps the draft as it was. */
export const updateVote = (meetingId: string, voteId: string, input: VoteInput) =>
  api.put<VoteMutationResponse>(`${base(meetingId)}/${voteId}`, input)

export const deleteVote = (meetingId: string, voteId: string) =>
  api.delete<VoteMutationResponse>(`${base(meetingId)}/${voteId}`)

export const openVote = (meetingId: string, voteId: string) =>
  api.post<VoteMutationResponse>(`${base(meetingId)}/${voteId}/open`)

/** One ballot, never changed or withdrawn (BR-265). */
export const castBallot = (meetingId: string, voteId: string, optionId: string) =>
  api.post<VoteMutationResponse>(`${base(meetingId)}/${voteId}/ballots`, { optionId })

export const closeVote = (meetingId: string, voteId: string) =>
  api.post<VoteClosedResponse>(`${base(meetingId)}/${voteId}/close`)

/** Recorded once, never edited (BR-262); the system never derives it from the tally (BR-263). */
export const recordVoteConclusion = (
  meetingId: string,
  voteId: string,
  conclusion: VoteConclusionValue,
  note: string,
) => api.post<VoteMutationResponse>(`${base(meetingId)}/${voteId}/conclusion`, { conclusion, note })

/**
 * UC-76a (SCRUM-96): documents of a Draft vote, Chair or Secretary only (409 MSG213 otherwise), all or none
 * (BR-304). From the meeting's documents: 404 MSG170, 409 MSG266 already attached, 503 MSG267.
 */
export const attachVoteDocuments = (meetingId: string, voteId: string, meetingDocumentIds: string[]) =>
  api.post<VoteDocumentsAttached>(`${base(meetingId)}/${voteId}/documents`, { meetingDocumentIds })

/** UC-76a AF-2: library documents, added to the meeting and attached in one save. 400 MSG249, 409 MSG165, 503 MSG166. */
export const attachVoteFromLibrary = (meetingId: string, voteId: string, documentIds: string[]) =>
  api.post<VoteDocumentsAttached>(`${base(meetingId)}/${voteId}/documents/from-library`, { documentIds })

/** UC-76a AF-1: files from the computer, as the meeting upload (MSG164, MSG288, MSG346, MSG166). */
export const uploadVoteDocuments = (meetingId: string, voteId: string, files: Upload, options: UploadOptions = {}) =>
  api.upload<VoteDocumentsAttached>(`${base(meetingId)}/${voteId}/documents/upload`, filesForm(files), options)

/** UC-76b: only the link goes; the document stays in the meeting (BR-305). 404 MSG269, 503 MSG270. */
export const detachVoteDocument = (meetingId: string, voteId: string, meetingDocumentId: string) =>
  api.delete<VoteMutationResponse>(`${base(meetingId)}/${voteId}/documents/${meetingDocumentId}`)

/**
 * UC-74: the caller read a vote document to its last page — sent only once the viewer has been
 * scrolled there (BR-290). One confirmation counts for every vote that links the document (BR-291);
 * a second one answers 200 with `alreadyConfirmed`.
 */
export const confirmDocumentRead = (meetingId: string, meetingDocumentId: string) =>
  api.post<DocumentReadConfirmed>(`/api/meetings/${meetingId}/documents/${meetingDocumentId}/read-confirmation`, {
    lastPageReached: true,
  })
