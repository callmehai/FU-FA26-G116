import { Icon } from '../../../components/Icon'
import { formatDateTime } from '../../../lib/format'
import type { MeetingHistoryItem } from '../../../types/meeting'
import { StatusBadge } from '../StatusBadge'
import { historyActionLabel } from './historyLabels'
import type { MeetingHistoryState } from './useMeetingHistory'

/**
 * The lifecycle timeline beside every tab of the detail page (SCRUM-19), oldest first like the
 * API. Each row says what happened, who did it and when, plus what that action carried: who a
 * submission went to, why a meeting was rejected. Participants read the same timeline with the
 * rejection reasons hidden by the server.
 */
export function MeetingHistory({
  history,
  onRetry,
  onLoadMore,
  nameOf,
}: {
  history: MeetingHistoryState
  onRetry: () => void
  onLoadMore: () => void
  /** A display name for an account id, when the page knows it. */
  nameOf: (id: string) => string | null
}) {
  return (
    <aside className="card history-card" aria-labelledby="meeting-history-title">
      <h2 className="card-title" id="meeting-history-title">
        Lịch sử trạng thái
      </h2>

      {!history.loaded ? (
        history.error ? (
          <div className="history-error" role="alert">
            <p>{history.error}</p>
            <button type="button" className="btn btn-outline btn-sm" onClick={onRetry}>
              <Icon name="refresh" size={16} />
              Thử lại
            </button>
          </div>
        ) : (
          <HistorySkeleton />
        )
      ) : (
        <>
          {history.items.length === 0 ? (
            // Meetings created before SCRUM-19 may have no rows; anything created since has at least one.
            <p className="card-pending">Chưa ghi nhận thay đổi trạng thái nào.</p>
          ) : (
            <ol className="timeline">
              {history.items.map((item) => (
                <HistoryRow key={item.id} item={item} nameOf={nameOf} />
              ))}
            </ol>
          )}
          {history.error && (
            <p className="history-inline-error" role="alert">
              {history.error}{' '}
              <button type="button" className="link-button" onClick={history.hasMore ? onLoadMore : onRetry}>
                Thử lại
              </button>
            </p>
          )}
          {history.hasMore && !history.error && (
            <button
              type="button"
              className="btn btn-ghost btn-sm btn-block history-more"
              onClick={onLoadMore}
              disabled={history.loadingMore}
            >
              {history.loadingMore ? 'Đang tải…' : 'Xem thêm'}
            </button>
          )}
        </>
      )}
    </aside>
  )
}

function HistoryRow({ item, nameOf }: { item: MeetingHistoryItem; nameOf: (id: string) => string | null }) {
  const approver =
    item.action === 'SubmitForApproval' && item.assignedApproverId ? nameOf(item.assignedApproverId) : null

  return (
    <li>
      <div className="timeline-head">
        <StatusBadge status={item.resultingStatus} label={historyActionLabel(item)} />
        <span className="timeline-actor">{item.actorName}</span>
      </div>
      {approver && <p className="timeline-detail">Người duyệt: {approver}</p>}
      {item.rejectionReason && <blockquote className="timeline-reason">{item.rejectionReason}</blockquote>}
      <time dateTime={item.occurredAt}>{formatDateTime(item.occurredAt)}</time>
    </li>
  )
}

/** Shaped like three real rows, so the card does not jump when the timeline arrives. */
function HistorySkeleton() {
  return (
    <ul className="timeline timeline-skeleton" aria-label="Đang tải lịch sử trạng thái">
      {[0, 1, 2].map((i) => (
        <li key={i}>
          <span className="skeleton" />
          <span className="skeleton" />
        </li>
      ))}
    </ul>
  )
}
