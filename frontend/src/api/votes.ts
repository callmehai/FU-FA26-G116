import type {
  VoteClosedResponse,
  VoteConclusionValue,
  VoteDetail,
  VoteDraftInput,
  VoteListSnapshot,
  VoteMutationResponse,
} from '../types/vote'
import { api } from './client'

/**
 * Votes during a meeting (SCRUM-28, UC-52a–UC-57). Contract in docs/03-api/15-bieu-quyet.md.
 *
 * Writes answer `{ code, message, meetingId, voteId }`: re-read afterwards, or wait for `voteChanged`. The
 * snapshot's `permissions` and each vote's `actions` say what the caller may do; the server re-checks every write.
 */

const base = (meetingId: string) => `/api/meetings/${meetingId}/votes`

export const getVotes = (meetingId: string) => api.get<VoteListSnapshot>(base(meetingId))

export const getVote = (meetingId: string, voteId: string) =>
  api.get<VoteDetail>(`${base(meetingId)}/${voteId}`)

/**
 * A draft (201 MSG210), or with `openImmediately` an Open vote at once (200 MSG217) — the Chair, while the meeting
 * is InProgress. If opening fails nothing is saved, so keep the form.
 */
export const createVote = (meetingId: string, input: VoteDraftInput, openImmediately = false) =>
  api.post<VoteMutationResponse>(base(meetingId), { ...input, openImmediately })

/** Replaces the whole configuration of a draft. */
export const updateVote = (meetingId: string, voteId: string, input: VoteDraftInput) =>
  api.put<VoteMutationResponse>(`${base(meetingId)}/${voteId}`, input)

export const deleteVote = (meetingId: string, voteId: string) =>
  api.delete<VoteMutationResponse>(`${base(meetingId)}/${voteId}`)

export const openVote = (meetingId: string, voteId: string) =>
  api.post<VoteMutationResponse>(`${base(meetingId)}/${voteId}/open`)

/** Final: 409 MSG226 on a second try. */
export const castBallot = (meetingId: string, voteId: string, optionId: string) =>
  api.post<VoteMutationResponse>(`${base(meetingId)}/${voteId}/ballots`, { optionId })

export const closeVote = (meetingId: string, voteId: string) =>
  api.post<VoteClosedResponse>(`${base(meetingId)}/${voteId}/close`)

/** Recorded once (409 MSG233 afterwards). `note` is at most 2000 characters. */
export const recordVoteConclusion = (meetingId: string, voteId: string, conclusion: VoteConclusionValue, note = '') =>
  api.post<VoteMutationResponse>(`${base(meetingId)}/${voteId}/conclusion`, { conclusion, note })
