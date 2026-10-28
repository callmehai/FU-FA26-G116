import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react'
import type { Participant } from 'livekit-client'
import { listParticipants } from '../../../api/participants'
import type { MeetingRole } from '../../../types/call'
import { useMeetingEvents } from '../../meetings/useMeetingEvents'
import { meetingRoleOf } from './roomSupport'

/** A meeting has at most 100 participants, so one page holds the whole roster. */
const ROSTER_PAGE = 100

/**
 * Who holds which meeting role now, by LiveKit identity (the user id). The role in a participant's
 * metadata is fixed when they join, so a Chair handed over during the call (UC-73) kept the tag and
 * the mute button until a reload (BR-328). The roster is read again on every `participantsChanged`;
 * until it arrives, or if it cannot be read, the metadata stands in.
 */
export function useRoomRoles(meetingId: string) {
  const [roles, setRoles] = useState<ReadonlyMap<string, MeetingRole> | null>(null)
  const generation = useRef(0)

  const load = useCallback(async () => {
    const current = ++generation.current
    try {
      const page = await listParticipants(meetingId, '', 1, ROSTER_PAGE)
      if (current !== generation.current) return
      const next = new Map<string, MeetingRole>()
      for (const row of page.items) if (row.userId && row.role) next.set(row.userId.toLowerCase(), row.role)
      setRoles(next)
    } catch {
      // The call goes on; tags keep what the token said. The next change reads again.
    }
  }, [meetingId])

  useEffect(() => {
    void Promise.resolve().then(load)
    const loads = generation
    return () => {
      loads.current++
    }
  }, [load])

  useMeetingEvents(meetingId, () => void load(), ['participantsChanged'])

  return useCallback(
    (participant: Participant) => roles?.get(participant.identity.toLowerCase()) ?? meetingRoleOf(participant),
    [roles],
  )
}

export const RoomRolesContext = createContext<(participant: Participant) => MeetingRole | undefined>(meetingRoleOf)

/** The current meeting role of anyone in the room. */
export const useRoleOf = () => useContext(RoomRolesContext)
