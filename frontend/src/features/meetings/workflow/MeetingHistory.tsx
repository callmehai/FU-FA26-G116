import { Icon } from '../../../components/Icon'
import { formatDateTime } from '../../../lib/format'
import type { MeetingHistoryItem } from '../../../types/meeting'
import { StatusBadge } from '../StatusBadge'
import { STATUS_TONE } from '../statusTone'
import { historyActionLabel } from './historyLabels'
import type { MeetingHistoryState } from './useMeetingHistory'

/**
 * The lifecycle timeline beside every tab of the detail page (SCRUM-19), oldest first like the
 * API. As in AI-eCabinet, a row is what happened and when, on one line; who did it (and who a
 * submission went to) is in the time's tooltip. A rejection's reason stays in view: it is what the
 * reader came for. Participants read the same timeline with the reasons hidden by the server.
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
              <Icon name="chevronDown" size={16} />
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
  const who = [`Người thực hiện: ${item.actorName}`, approver && `Người duyệt: ${approver}`].filter(Boolean).join('\n')

  return (
    // The dot takes the colour of the state the action led to, as the status chips do.
    <li className={`timeline-${STATUS_TONE[item.resultingStatus]}`}>
      <div className="timeline-row">
        <StatusBadge status={item.resultingStatus} label={historyActionLabel(item)} plain />
        {/* Focusable, so the keyboard reaches who acted; screen readers hear it in the text. */}
        <time className="timeline-time" dateTime={item.occurredAt} tabIndex={0} data-tooltip={who}>
          {formatDateTime(item.occurredAt)}
          <span className="visually-hidden">, {who.replace('\n', ', ')}</span>
        </time>
      </div>
      {(item.rejectionReason ?? item.cancellationReason) && (
        <blockquote className="timeline-reason">{item.rejectionReason ?? item.cancellationReason}</blockquote>
      )}
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
