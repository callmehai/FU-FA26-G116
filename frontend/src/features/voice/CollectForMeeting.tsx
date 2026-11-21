import { useEffect, useState } from 'react'
import { listMeetings } from '../../api/meetings'
import { getVoiceCollection } from '../../api/voice'
import { Select } from '../../components/Select'
import { formatDate } from '../../lib/format'
import type { MeetingListItem, MeetingStatus } from '../../types/meeting'
import { VoiceCollectionPanel } from './VoiceCollectionPanel'

/**
 * ADR-0060 — the Secretary's own entry to recording samples for others, without opening each meeting: pick one of the
 * in-person or hybrid meetings they are Secretary of (published or in progress), then record its people. A meeting is
 * offered only when the server answers its collection view, so the server alone decides who may record where.
 */
export function CollectForMeeting() {
  const [meetings, setMeetings] = useState<MeetingListItem[] | null>(null)
  const [picked, setPicked] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    const load = async () => {
      const statuses: MeetingStatus[] = ['InProgress', 'Published']
      const pages = await Promise.all(
        statuses.map((status) =>
          listMeetings({ status, page: 1, pageSize: 50, sortBy: 'plannedStartTime', sortDir: 'asc' }).catch(() => null),
        ),
      )
      const candidates = pages
        .flatMap((p) => p?.items ?? [])
        .filter((m) => m.mode !== 'Online' && m.sensitivity !== 'Secret')
      const checked = await Promise.all(
        candidates.map((m) =>
          getVoiceCollection(m.id).then(
            () => m,
            () => null,
          ),
        ),
      )
      if (!live) return
      const mine = checked.filter((m): m is MeetingListItem => m !== null)
      setMeetings(mine)
      setPicked((current) => current ?? mine[0]?.id ?? null)
    }
    void load()
    return () => {
      live = false
    }
  }, [])

  if (meetings === null || meetings.length === 0) return null

  return (
    <section className="voice-collect-for" aria-label="Thu hộ">
      <div className="voice-status">
        <h2 className="card-title">Thu hộ</h2>
        <Select
          value={picked}
          label="Cuộc họp"
          placeholder="Chọn cuộc họp"
          options={meetings.map((m) => ({ value: m.id, label: m.title, hint: formatDate(m.plannedStartTime) }))}
          onChange={setPicked}
        />
      </div>
      {picked && <VoiceCollectionPanel key={picked} meetingId={picked} />}
    </section>
  )
}
