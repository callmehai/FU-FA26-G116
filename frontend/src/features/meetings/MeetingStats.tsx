import { useEffect, useState, type ReactNode } from 'react'
import { listMeetings } from '../../api/meetings'
import { Icon, type IconName } from '../../components/Icon'
import { useAccessLost } from '../auth/useAccessLost'
import type { MeetingListQuery } from '../../types/meeting'
import { STAT_CARDS, statQuery, type StatCard } from './listParams'

// Each card's accent colours its icon and number (global.css: .stat-card-<card>).
const LOOKS: Record<StatCard, { label: string; icon: IconName }> = {
  today: { label: 'Họp hôm nay', icon: 'calendarCheck' },
  published: { label: 'Đã công bố', icon: 'megaphone' },
  todo: { label: 'Cần xử lý', icon: 'clipboardCheck' },
  draft: { label: 'Bản nháp', icon: 'pencil' },
}

/** What each card counts, plus today's meetings under way that "Họp hôm nay" mentions under its number. */
type Counted = StatCard | 'live'
type Count = number | 'error' | undefined

/**
 * The four counts above the meeting list, as on AI-eCabinet: Họp hôm nay, Đã công bố, Cần xử lý and
 * Bản nháp. Each is the list's own `totalCount` under the card's criteria (a one-row page), so the
 * server's visibility rule decides what is counted and a click shows exactly the rows counted. They
 * ignore the search and the filters on purpose: they say how many there are, not how many match.
 * "Bản nháp" is only for those who can create meetings; nobody else ever has a draft.
 */
export function MeetingStats({
  active,
  showDrafts,
  canApprove,
  reloadKey,
  onSelect,
}: {
  active: StatCard | null
  /** May create meetings: has drafts, and publishes their own approved meetings. */
  showDrafts: boolean
  /** May approve meetings assigned to them. */
  canApprove: boolean
  /** Changes when the page's "Thử lại" is pressed, so the counts are read again with the list. */
  reloadKey: number
  /** A card, or null to leave the one that is on. */
  onSelect: (card: StatCard | null) => void
}) {
  const handleAccessLost = useAccessLost()
  const [counts, setCounts] = useState<Partial<Record<Counted, Count>>>({})
  const cards = STAT_CARDS.filter((card) => showDrafts || card !== 'draft')

  useEffect(() => {
    let cancelled = false
    const now = new Date()
    const counted: [Counted, MeetingListQuery][] = [
      ...STAT_CARDS.filter((card) => showDrafts || card !== 'draft').map((card): [Counted, MeetingListQuery] => [
        card,
        statQuery(card, now),
      ]),
      // Today's, as the card's own number: a meeting left running on another day is not "họp hôm nay".
      ['live', statQuery({ status: 'InProgress', when: 'today', scope: '' }, now)],
    ]
    for (const [key, query] of counted) {
      void listMeetings(query).then(
        (page) => {
          if (!cancelled) setCounts((c) => ({ ...c, [key]: page.totalCount }))
        },
        (err) => {
          if (cancelled || handleAccessLost(err)) return
          setCounts((c) => ({ ...c, [key]: 'error' }))
        },
      )
    }
    return () => {
      cancelled = true
    }
  }, [handleAccessLost, reloadKey, showDrafts])

  const live = counts.live
  const todaySub: ReactNode =
    typeof live === 'number' && live > 0 ? (
      <span className="stat-live">{live.toLocaleString('vi-VN')} đang diễn ra</span>
    ) : (
      'Chưa có cuộc họp đang diễn ra'
    )
  const subs: Record<StatCard, ReactNode> = {
    today: todaySub,
    published: 'Đã gửi giấy mời',
    // What waits on this user: approving needs MeetingApprover, publishing one's own meeting MeetingCreator.
    todo:
      canApprove && !showDrafts
        ? 'Chờ bạn duyệt'
        : showDrafts && !canApprove
          ? 'Chờ bạn công bố'
          : 'Chờ bạn duyệt, công bố',
    draft: 'Chưa gửi duyệt',
  }
  // A card that could not count shows no number. When none could, one line says so for the row instead of the
  // same sentence on every card.
  const allFailed = cards.every((card) => counts[card] === 'error')

  return (
    <section
      className={cards.length === 3 ? 'stat-cards stat-cards-3' : 'stat-cards'}
      aria-label="Thống kê cuộc họp"
      aria-busy={cards.some((card) => counts[card] === undefined) || live === undefined}
    >
      <div className="stat-grid">
        {cards.map((card) => {
          const look = LOOKS[card]
          const pressed = active === card
          // "Họp hôm nay" waits for its sub line too, so the card does not change once shown.
          const count = card === 'today' && live === undefined && counts.today !== 'error' ? undefined : counts[card]
          return (
            <button
              key={card}
              type="button"
              className={`stat-card${pressed ? ' stat-card-pressed' : ''}`}
              aria-pressed={pressed}
              onClick={() => onSelect(pressed ? null : card)}
            >
              <span className="stat-label">
                <Icon name={look.icon} size={16} />
                {look.label}
              </span>
              {count === undefined ? (
                <>
                  <span className="stat-value-slot" aria-hidden="true">
                    <span className="skeleton stat-value-skeleton" />
                  </span>
                  <span className="stat-sub-slot" aria-hidden="true">
                    <span className="skeleton stat-sub-skeleton" />
                  </span>
                </>
              ) : count === 'error' ? (
                !allFailed && (
                  <>
                    <span className="stat-value-slot" aria-hidden="true" />
                    <span className="stat-sub">Không tải được số lượng</span>
                  </>
                )
              ) : (
                <>
                  <span className="stat-value">{count.toLocaleString('vi-VN')}</span>
                  <span className="stat-sub">{subs[card]}</span>
                </>
              )}
            </button>
          )
        })}
      </div>
      {allFailed && <p className="stat-error">Không tải được số lượng cuộc họp.</p>}
    </section>
  )
}
