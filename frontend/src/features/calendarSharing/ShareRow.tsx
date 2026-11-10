import { Badge, type BadgeTone } from '../../components/Badge'
import { Icon } from '../../components/Icon'
import { InfoTip } from '../../components/InfoTip'
import { MenuButton, type MenuItem } from '../../components/MenuButton'
import type { Share, ShareAction } from '../../types/calendarSharing'
import { Person } from '../participants/Person'
import { LEVEL_HINT, LEVEL_LABEL, MSG439_STOPPED, STATUS_LABEL, dayText, isClosed, periodText } from './shareFormat'

/** What the page opens for a row: a form dialog or a confirmation. */
export type RowIntent =
  | { kind: 'accept'; share: Share }
  | { kind: 'decline'; share: Share }
  | { kind: 'change'; share: Share; as: 'viewer' | 'owner' }
  | { kind: 'withdraw'; share: Share }
  | { kind: 'end'; share: Share; as: 'viewer' | 'owner' }

const has = (share: Share, action: ShareAction) => share.actions.includes(action)

/** Who must act next on an open share, from the caller's side: that decides the chip's words and tone. */
function stateChip(share: Share, owed: boolean): { tone: BadgeTone; label: string } {
  if (share.status === 'Pending')
    return owed ? { tone: 'amber', label: 'Chờ bạn trả lời' } : { tone: 'slate', label: 'Chờ trả lời' }
  if (share.status === 'Effective') return { tone: 'emerald', label: STATUS_LABEL.Effective }
  if (share.status === 'Stopped') return { tone: 'red', label: STATUS_LABEL.Stopped }
  return { tone: 'slate', label: STATUS_LABEL[share.status] }
}

/**
 * One share in a list, seen from the caller's side (`as`): the other person, then one line of chips — state, level,
 * period — and, when something waits, what it is. Answering is in plain sight (Đồng ý / Từ chối); everything else sits
 * behind "…", so a row never carries four buttons as AI-eCabinet's does. The buttons follow `actions`, never the status.
 */
export function ShareRow({
  share,
  as,
  focused,
  onIntent,
}: {
  share: Share
  as: 'owner' | 'viewer'
  focused: boolean
  onIntent: (intent: RowIntent) => void
}) {
  const other = as === 'owner' ? share.viewer : share.owner
  const change = share.status === 'Effective' ? share.pendingChange : null
  // Accept means "answer something waiting for me": a Pending share asked of me, or (owner) the viewer's change.
  const owed = has(share, 'accept') || (share.status === 'Pending' && has(share, 'decline'))
  const chip = stateChip(share, owed)

  const more: MenuItem[] = []
  if (has(share, 'requestChange'))
    more.push({
      key: 'change',
      lead: <Icon name="pencil" size={16} />,
      label: 'Xin thay đổi',
      onSelect: () => onIntent({ kind: 'change', share, as: 'viewer' }),
    })
  if (has(share, 'update'))
    more.push({
      key: 'update',
      lead: <Icon name="pencil" size={16} />,
      label: 'Đổi chia sẻ',
      onSelect: () => onIntent({ kind: 'change', share, as: 'owner' }),
    })
  if (has(share, 'withdraw'))
    more.push({
      key: 'withdraw',
      lead: <Icon name="undo" size={16} />,
      label: change ? 'Rút yêu cầu thay đổi' : 'Rút yêu cầu',
      onSelect: () => onIntent({ kind: 'withdraw', share }),
    })
  if (has(share, 'end'))
    more.push({
      key: 'end',
      lead: <Icon name="calendarX" size={16} />,
      label: as === 'owner' ? 'Ngừng chia sẻ' : 'Thôi xem lịch',
      onSelect: () => onIntent({ kind: 'end', share, as }),
    })

  return (
    <li
      id={`share-${share.id}`}
      className={`share-row${isClosed(share) ? ' share-row-closed' : ''}${focused ? ' share-row-focus' : ''}`}
    >
      <div className="share-row-main">
        <Person name={other.fullName} email={other.email} />
        <div className="share-row-meta">
          <Badge tone={chip.tone}>{chip.label}</Badge>
          {share.status === 'Stopped' && <InfoTip text={MSG439_STOPPED} />}
          <span className="share-level" data-tooltip={LEVEL_HINT[share.level]}>
            {LEVEL_LABEL[share.level]}
          </span>
          <span className="share-period">{periodText(share.startDate, share.endDate)}</span>
        </div>
        {change && (
          <p className="share-row-note">
            <Icon name="clock" size={14} />
            <span>
              {as === 'owner' ? 'Xin đổi' : 'Bạn xin đổi'}:{' '}
              {[
                change.level !== share.level ? LEVEL_LABEL[change.level] : null,
                change.endDate !== share.endDate ? `đến ${dayText(change.endDate)}` : null,
              ]
                .filter(Boolean)
                .join(', ')}
            </span>
          </p>
        )}
        {share.senderDeactivated && share.status === 'Pending' && (
          <p className="share-row-note share-row-note-warn">
            <Icon name="alertCircle" size={14} />
            <span>Tài khoản người gửi đã bị vô hiệu hoá, chỉ từ chối được.</span>
          </p>
        )}
      </div>

      {(owed || more.length > 0) && (
        <div className="share-row-actions">
          {has(share, 'decline') && (
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => onIntent({ kind: 'decline', share })}
            >
              Từ chối
            </button>
          )}
          {has(share, 'accept') && (
            <button
              type="button"
              className="btn btn-primary btn-sm"
              onClick={() => onIntent({ kind: 'accept', share })}
            >
              <Icon name="check" size={16} />
              Đồng ý
            </button>
          )}
          {more.length > 0 && <MenuButton label="Thao tác khác" icon="moreVertical" items={more} iconOnly />}
        </div>
      )}
    </li>
  )
}
