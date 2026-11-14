import type { MeetingMutationResponse } from '../types/meeting'
import type { SlotDraftRequest, SlotSearchRequest, SlotSearchResult } from '../types/meetingSlots'
import { api } from './client'

/** SCRUM-137 — docs/03-api/24-tim-gio-hop.md. Both need `MeetingCreator` (403 MSG448). */
const BASE = '/api/calendar/slots'

/** UC-85a — 400 MSG444 with `errors` by field, 500 MSG453. 200 with `code = MSG445` when nothing fits everyone. */
export const findSlots = (body: SlotSearchRequest) => api.post<SlotSearchResult>(BASE, body)

/**
 * UC-85b — 201 MSG29 (same body as POST /api/meetings); 409 MSG446 when the slot is no longer free; 400 MSG31 / MSG277
 * with `errors`; 500 MSG453 (recheck failed) or MSG35 (save failed).
 */
export const createDraftFromSlot = (body: SlotDraftRequest) => api.post<MeetingMutationResponse>(`${BASE}/draft`, body)
