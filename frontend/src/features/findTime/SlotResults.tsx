import { Badge, type BadgeTone } from '../../components/Badge'
import { Icon } from '../../components/Icon'
import type { Slot, SlotSearchResult } from '../../types/meetingSlots'
import { clockOf, dayLabel, slotKey } from './slotRules'

const STATE: Record<'free' | 'busy' | 'unknown', { label: string; tone: BadgeTone }> = {
  free: { label: 'Rảnh', tone: 'emerald' },
  busy: { label: 'Bận', tone: 'red' },
  unknown: { label: 'Chưa biết', tone: 'slate' },
}

/**
 * UC-85a step 4 / AF-1 — the slots by day as start-time chips, AI-eCabinet's layout. Choosing a chip opens, under its day,
 * who among the people counted is free, busy or unknown, and the one "Tạo nháp" (UC-85b). Nothing about a meeting is
 * ever shown (BR-427); "Chưa biết" is never free (BR-415).
 */
export function SlotResults({
  result,
  range,
  selected,
  onSelect,
  onDraft,
}: {
  result: SlotSearchResult
  /** The searched dates, to tell "the shares do not reach these days" from "everyone is busy". */
  range: { from: string; to: string }
  selected: string | null
  onSelect: (key: string | null) => void
  onDraft: (slot: Slot, day: string) => void
}) {
  const counted = result.people.filter((p) => p.status !== 'NotShared')
  const notShared = result.people.filter((p) => p.status === 'NotShared')
  const total = result.days.reduce((n, d) => n + d.slots.length, 0)
  const mostFree = result.code === 'MSG445'
  const notSharedText =
    notShared.length > 0
      ? `Chưa tính lịch của ${notShared.map((p) => p.fullName).join(', ')} vì chưa được chia sẻ. Các khung dưới đây có thể trùng lịch của họ.`
      : null

  if (total === 0) {
    const shared = result.people.filter((p) => p.status === 'Shared')
    const reaches = shared.some((p) => p.startDate! <= range.to && p.endDate! >= range.from)
    return (
      <div className="card find-empty" role="status">
        <Icon name="calendarSearch" size={24} />
        {counted.length === 0 ? (
          <>
            <p className="empty-title">Không có lịch nào để tính</p>
            <p className="find-empty-text">
              {notShared.map((p) => p.fullName).join(', ')} chưa chia sẻ lịch làm việc với bạn. Yêu cầu chia sẻ lịch, hoặc
              tick “Tính cả lịch của tôi” rồi tìm lại.
            </p>
          </>
        ) : shared.length > 0 && !reaches && !result.people.some((p) => p.status === 'Own') ? (
          <>
            <p className="empty-title">Lịch được chia sẻ không phủ khoảng ngày này</p>
            <p className="find-empty-text">Chọn khoảng ngày nằm trong thời gian chia sẻ, hoặc xin gia hạn chia sẻ.</p>
          </>
        ) : (
          <>
            <p className="empty-title">Không còn khung nào trong khoảng đã chọn</p>
            <p className="find-empty-text">Thử khoảng ngày khác, thời lượng ngắn hơn hoặc cách cuộc họp khác ít hơn.</p>
          </>
        )}
      </div>
    )
  }

  return (
    <div className="find-results">
      {(mostFree || notSharedText) && (
        <p className="alert alert-warning" role="status">
          {[mostFree ? result.message : null, notSharedText].filter(Boolean).join(' ')}
        </p>
      )}
      <p className="find-count">
        {total} khung giờ {mostFree ? 'nhiều người rảnh nhất' : 'phù hợp'}
        {result.truncated && <span className="find-count-note"> · chỉ hiện {total} khung đầu, thu hẹp khoảng ngày để xem thêm</span>}
      </p>
      {result.days.map((day) => {
        const open = day.slots.find((s) => slotKey(s) === selected)
        return (
          <section key={day.date} className="card find-day" aria-label={dayLabel(day.date)}>
            <h3 className="find-day-title">{dayLabel(day.date)}</h3>
            <div className="find-chips" role="group" aria-label={`Giờ bắt đầu ${dayLabel(day.date)}`}>
              {day.slots.map((slot) => (
                <button
                  key={slotKey(slot)}
                  type="button"
                  className="chip-toggle find-chip"
                  aria-pressed={slotKey(slot) === selected}
                  onClick={() => onSelect(slotKey(slot) === selected ? null : slotKey(slot))}
                >
                  {clockOf(slot.start)}
                  {mostFree && (
                    <span className="find-chip-count">
                      {slot.freeCount}/{slot.countedCount}
                    </span>
                  )}
                </button>
              ))}
            </div>
            {open && (
              <div className="find-detail">
                <div className="find-detail-head">
                  <b>
                    {dayLabel(day.date)}, {clockOf(open.start)} – {clockOf(open.end)}
                  </b>
                  <button type="button" className="btn btn-primary" onClick={() => onDraft(open, day.date)}>
                    <Icon name="plus" size={16} />
                    Tạo nháp
                  </button>
                </div>
                <ul className="find-detail-people" aria-label="Ai rảnh ở khung này">
                  {counted.map((person) => {
                    const state = open.busyUserIds.includes(person.id)
                      ? STATE.busy
                      : open.unknownUserIds.includes(person.id)
                        ? STATE.unknown
                        : STATE.free
                    return (
                      <li key={person.id}>
                        <span>{person.status === 'Own' ? 'Tôi' : person.fullName}</span>
                        <Badge tone={state.tone}>{state.label}</Badge>
                      </li>
                    )
                  })}
                </ul>
              </div>
            )}
          </section>
        )
      })}
    </div>
  )
}
