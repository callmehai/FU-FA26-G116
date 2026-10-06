import { useEffect, useState } from 'react'
import { listMeetings } from '../../api/meetings'
import { Icon, type IconName } from '../../components/Icon'
import type { MeetingStatus } from '../../types/meeting'
import { useAccessLost } from '../auth/useAccessLost'

interface Card {
  status: MeetingStatus
  label: string
  hint: string
  icon: IconName
  tone: string
}

const CARDS: Card[] = [
  { status: 'InProgress', label: 'Đang diễn ra', hint: 'Đang họp', icon: 'broadcast', tone: 'live' },
  { status: 'Published', label: 'Đã công bố', hint: 'Đã gửi giấy mời', icon: 'send', tone: 'blue' },
  { status: 'WaitingApproval', label: 'Chờ duyệt', hint: 'Đã gửi người duyệt', icon: 'clock', tone: 'amber' },
  { status: 'Draft', label: 'Bản nháp', hint: 'Chưa gửi duyệt', icon: 'fileText', tone: 'slate' },
]

type Counts = Partial<Record<MeetingStatus, number>> | 'error' | null

/**
 * The four states people look for first, as counts that double as a status filter (ADR-0012).
 *
 * Each count is GET /api/meetings with that status and one row per page — its `totalCount` comes
 * after the server's visibility rule, so a meeting the caller may not see is never counted. The
 * counts ignore the other filters on purpose: they say how many there are, not how many match.
 * "Bản nháp" is only for those who can create meetings; nobody else ever has a draft.
 */
export function StatusSummary({
  active,
  showDrafts,
  reloadKey,
  onSelect,
}: {
  active: MeetingStatus | ''
  showDrafts: boolean
  /** Changes when the page's "Thử lại" is pressed, so the counts are read again with the list. */
  reloadKey: number
  onSelect: (status: MeetingStatus | '') => void
}) {
  const handleAccessLost = useAccessLost()
  const [counts, setCounts] = useState<Counts>(null)
  const cards = CARDS.filter((c) => showDrafts || c.status !== 'Draft')

  useEffect(() => {
    let cancelled = false
    const statuses = CARDS.map((c) => c.status)
    void Promise.all(
      statuses.map((status) =>
        listMeetings({ status, page: 1, pageSize: 1, sortBy: 'createdAt', sortDir: 'desc' }).then((r) => r.totalCount),
      ),
    ).then(
      (totals) => {
        if (!cancelled) setCounts(Object.fromEntries(statuses.map((s, i) => [s, totals[i]])))
      },
      (err) => {
        if (cancelled || handleAccessLost(err)) return
        setCounts('error')
      },
    )
    return () => {
      cancelled = true
    }
  }, [handleAccessLost, reloadKey])

  return (
    <div className="summary-cards" role="group" aria-label="Lọc nhanh theo trạng thái">
      {cards.map((card) => {
        const selected = active === card.status
        const count = counts && counts !== 'error' ? counts[card.status] : undefined
        return (
          <button
            key={card.status}
            type="button"
            className={`summary-card summary-${card.tone}${selected ? ' summary-card-selected' : ''}`}
            aria-pressed={selected}
            onClick={() => onSelect(selected ? '' : card.status)}
            title={selected ? 'Bỏ lọc theo trạng thái này' : `Chỉ xem cuộc họp ${card.label.toLowerCase()}`}
          >
            <span className="summary-icon">
              <Icon name={card.icon} size={22} />
            </span>
            <span className="summary-body">
              <span className="summary-label">{card.label}</span>
              {count !== undefined ? (
                <span className="summary-count">{count.toLocaleString('vi-VN')}</span>
              ) : counts === 'error' ? (
                <span className="summary-count summary-count-missing">–</span>
              ) : (
                <span className="skeleton summary-count-skeleton" aria-hidden="true" />
              )}
              {counts === 'error' ? (
                <span className="summary-hint summary-hint-missing">Không tải được</span>
              ) : (
                <span className="summary-hint">{card.hint}</span>
              )}
            </span>
          </button>
        )
      })}
    </div>
  )
}
