import { useState, type ReactNode } from 'react'
import {
  declineSpeakingRequest,
  finishSpeaking,
  inviteToSpeak,
  lowerHand,
  raiseHand,
  startSpeaking,
} from '../../api/speaking'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { avatarColors, initials } from '../../lib/avatar'
import { formatClock } from '../../lib/format'
import { MEETING_ROLE_LABEL } from '../../types/call'
import type { SpeakingRequest } from '../../types/speaking'
import type { Speaking } from './useSpeaking'

type Confirm = { kind: 'decline' | 'finish'; request: SpeakingRequest }

/**
 * Register to Speak (SCRUM-27, UC-79): the call room's side panel (ADR-0011). It shows only in the room — hands are
 * raised there, and the meeting page carries no queue. Whether the caller may run the turns comes from the server's
 * `canManage`. One line per raised hand: the person, then where they stand; the time and the agenda item wait behind
 * the line's marker. The caller raises and lowers their own hand, and starts and finishes their own turn.
 */
export function SpeakingPanel({
  meetingId,
  speaking,
}: {
  meetingId: string
  /** The room's own queue, already read for its "Giơ tay" button. */
  speaking: Speaking
}) {
  const { state, data, pending, reload, run } = speaking
  const [confirm, setConfirm] = useState<Confirm | null>(null)
  const wrap = (body: ReactNode) => (
    <div className="room-panel-body speaking-panel">
      <h2 className="room-panel-title">Phát biểu</h2>
      {body}
    </div>
  )

  if (state.kind === 'gone') {
    return wrap(<p className="speaking-note">Bạn không còn xem được lượt phát biểu của cuộc họp này.</p>)
  }
  if (!data) {
    return wrap(
      state.kind === 'error' ? (
        <div className="alert alert-error" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void reload()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      ) : (
        <SpeakingSkeleton />
      ),
    )
  }

  const live = data.meetingStatus === 'InProgress'
  const queued = data.requests.filter((r) => r.status === 'Queued')
  // BR-338: one person holds the floor; the Chair invites the next only once that turn is closed.
  const floorTaken = data.requests.some((r) => r.status !== 'Queued')

  return wrap(
    <>
      {state.kind === 'error' && (
        <div className="alert alert-error" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void reload()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}

      {/* EX-1: the queue exists only while the meeting runs; ending it closes every request. */}
      {!live ? (
        <p className="speaking-note">Chỉ đăng ký phát biểu khi cuộc họp đang diễn ra.</p>
      ) : (
        <MyTurn meetingId={meetingId} speaking={speaking} />
      )}

      {live &&
        (data.requests.length === 0 ? (
          <div className="speaking-empty">
            <Icon name="hand" size={22} />
            <p>Chưa có ai giơ tay.</p>
          </div>
        ) : (
          <ol className="speaking-list" aria-label="Hàng đợi phát biểu" aria-busy={pending}>
            {data.requests.map((request) => {
              const at = queued.indexOf(request)
              return (
                <SpeakingRow
                  key={request.id}
                  request={request}
                  place={at + 1}
                  manage={
                    data.canManage
                      ? {
                          pending,
                          canInvite: !floorTaken,
                          onInvite: () => void run(() => inviteToSpeak(meetingId, request.id)),
                          onDecline: () => setConfirm({ kind: 'decline', request }),
                          onFinish: () => setConfirm({ kind: 'finish', request }),
                        }
                      : null
                  }
                />
              )
            })}
          </ol>
        ))}

      {confirm && (
        <ConfirmDialog
          title={confirm.kind === 'decline' ? 'Từ chối lượt phát biểu' : 'Kết thúc lượt phát biểu'}
          tone={confirm.kind === 'decline' ? 'danger' : 'primary'}
          confirmLabel={confirm.kind === 'decline' ? 'Từ chối' : 'Kết thúc lượt'}
          pendingLabel={confirm.kind === 'decline' ? 'Đang từ chối…' : 'Đang kết thúc…'}
          cancelLabel="Để sau"
          onConfirm={async () => {
            const { request } = confirm
            await run(() =>
              confirm.kind === 'decline'
                ? declineSpeakingRequest(meetingId, request.id)
                : finishSpeaking(meetingId, request.id),
            )
          }}
          onClose={() => setConfirm(null)}
        >
          {confirm.kind === 'decline'
            ? `Hạ tay của ${confirm.request.displayName}? Người này có thể giơ tay lại.`
            : `Kết thúc lượt của ${confirm.request.displayName}? Thời gian nói được giữ cho biên bản.`}
        </ConfirmDialog>
      )}
    </>,
  )
}

/**
 * The caller's own hand in the room: raise it, see where it stands, lower it, and — once invited — start and finish
 * the turn. The invitation never turns the microphone on (BR-338): the person does that themself (UC-39a).
 */
function MyTurn({ meetingId, speaking }: { meetingId: string; speaking: Speaking }) {
  const { data, mine, pending, run } = speaking
  if (!data) return null

  if (!mine) {
    return (
      <button
        type="button"
        className="btn btn-outline btn-sm speaking-raise"
        disabled={pending}
        onClick={() => void run(() => raiseHand(meetingId))}
      >
        <Icon name="hand" size={16} />
        {pending ? 'Đang giơ tay…' : 'Giơ tay'}
      </button>
    )
  }

  const lower = () => void run(() => lowerHand(meetingId, mine.id))
  if (mine.status === 'Speaking') {
    return (
      <div className="speaking-mine">
        <span className="status status-live">Bạn đang nói</span>
        <button
          type="button"
          className="btn btn-primary btn-sm"
          disabled={pending}
          onClick={() => void run(() => finishSpeaking(meetingId, mine.id))}
        >
          Kết thúc phát biểu
        </button>
      </div>
    )
  }
  if (mine.status === 'Invited') {
    return (
      <div className="speaking-mine">
        <strong>Đến lượt bạn</strong>
        <button type="button" className="btn btn-outline btn-sm" disabled={pending} onClick={lower}>
          Hạ tay
        </button>
        <button
          type="button"
          className="btn btn-primary btn-sm"
          disabled={pending}
          onClick={() => void run(() => startSpeaking(meetingId, mine.id))}
        >
          Bắt đầu phát biểu
        </button>
      </div>
    )
  }
  const place = data.requests.filter((r) => r.status === 'Queued').indexOf(mine) + 1
  return (
    <div className="speaking-mine">
      <span>
        <Icon name="hand" size={16} />
        Bạn đang giơ tay · thứ {place}
      </span>
      <button type="button" className="btn btn-outline btn-sm" disabled={pending} onClick={lower}>
        Hạ tay
      </button>
    </div>
  )
}

interface Manage {
  pending: boolean
  /** Nobody is invited or speaking (BR-338). */
  canInvite: boolean
  onInvite: () => void
  onDecline: () => void
  onFinish: () => void
}

function SpeakingRow({ request, place, manage }: { request: SpeakingRequest; place: number; manage: Manage | null }) {
  const colors = avatarColors(request.displayName)
  const role = request.role ? MEETING_ROLE_LABEL[request.role] : null
  // Who, when and about what: one hover on the marker, not a second line.
  const facts = [
    role,
    `Giơ tay lúc ${formatClock(request.requestedAt)}`,
    request.startedAt ? `Bắt đầu nói lúc ${formatClock(request.startedAt)}` : null,
    request.agendaItemTitle ? `Mục: ${request.agendaItemTitle}` : null,
  ]
    .filter(Boolean)
    .join('\n')

  return (
    <li className={`speaking-row${request.isMine ? ' is-mine' : ''}`}>
      <span
        className="avatar avatar-sm"
        style={{ backgroundColor: colors.bg, color: colors.fg }}
        role="img"
        tabIndex={0}
        data-tooltip={facts}
        aria-label={facts.replaceAll('\n', '. ')}
      >
        {initials(request.displayName)}
      </span>
      <span className="speaking-name">
        {request.displayName}
        {request.isMine && ' (Bạn)'}
      </span>
      {request.status === 'Speaking' ? (
        <span className="status status-live">Đang nói</span>
      ) : request.status === 'Invited' ? (
        <span className="badge badge-blue">Đã mời</span>
      ) : (
        <span className="speaking-place" aria-label={`Thứ ${place}`}>
          {place}
        </span>
      )}
      {manage && (
        <span className="speaking-actions">
          {request.status === 'Speaking' ? (
            <button
              type="button"
              className="btn btn-outline btn-sm"
              disabled={manage.pending}
              onClick={manage.onFinish}
            >
              Kết thúc
            </button>
          ) : (
            <button
              type="button"
              className="icon-button icon-button-sm"
              aria-label={request.status === 'Invited' ? 'Huỷ lời mời' : 'Từ chối'}
              data-tooltip={request.status === 'Invited' ? 'Huỷ lời mời' : 'Từ chối'}
              disabled={manage.pending}
              onClick={manage.onDecline}
            >
              <Icon name="x" size={16} />
            </button>
          )}
          {/* Hidden, not disabled, while someone holds the floor: that person's row at the top says why. */}
          {request.status === 'Queued' && manage.canInvite && (
            <button
              type="button"
              className="btn btn-primary btn-sm"
              disabled={manage.pending}
              onClick={manage.onInvite}
            >
              Mời
            </button>
          )}
        </span>
      )}
    </li>
  )
}

/** Shaped like a short queue, so nothing jumps when it arrives. */
function SpeakingSkeleton() {
  return (
    <ol className="speaking-list" aria-busy="true" aria-label="Đang tải hàng đợi phát biểu">
      {Array.from({ length: 2 }, (_, i) => (
        <li key={i} className="speaking-row">
          <span className="skeleton speaking-skeleton" />
        </li>
      ))}
    </ol>
  )
}
