import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { avatarColors, initials } from '../../lib/avatar'
import type { AuditActor, AuditLogItem } from '../../types/auditLog'
import { objectTypeLabel } from './objectTypes'

/** BR-441: an account, a Guest by the invitation's name, or the system. */
export function ActorCell({ actor }: { actor: AuditActor }) {
  if (actor.kind === 'system') {
    return (
      <span className="person">
        <span className="avatar audit-system" aria-hidden="true">
          <Icon name="settings" size={16} />
        </span>
        <span className="person-name">Hệ thống</span>
      </span>
    )
  }
  const name = actor.name ?? 'Không rõ'
  const { bg, fg } = avatarColors(name)
  return (
    <span className="person">
      <span className="avatar" style={{ backgroundColor: bg, color: fg }} aria-hidden="true">
        {initials(name)}
      </span>
      <span>
        <span className="person-name">
          {name}
          {actor.kind === 'guest' && <span className="person-self">Khách</span>}
        </span>
        {actor.email && <span className="cell-sub">{actor.email}</span>}
      </span>
    </span>
  )
}

/** Evidence: entries of one minute are told apart by the second. */
const withSeconds = new Intl.DateTimeFormat('vi-VN', {
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

/**
 * UC-13 AF-2: one entry, read-only, beside the list. `details` is shown key by key exactly as recorded —
 * its field names differ between event types, so none is singled out.
 */
export function AuditEntrySheet({ item, onClose }: { item: AuditLogItem; onClose: () => void }) {
  const details =
    item.details && typeof item.details === 'object' && !Array.isArray(item.details)
      ? Object.entries(item.details as Record<string, unknown>)
      : item.details == null
        ? []
        : [['', item.details] as [string, unknown]]

  return (
    <Dialog
      title={item.actionLabel}
      description={withSeconds.format(new Date(item.occurredAt)).replace(', ', ' ')}
      onClose={onClose}
      size="sheet"
    >
      <dl className="audit-facts">
        <div>
          <dt>Người thực hiện</dt>
          <dd>
            <ActorCell actor={item.actor} />
          </dd>
        </div>
        <div>
          <dt>Mã sự kiện</dt>
          <dd>
            <code className="audit-code">{item.action}</code>
          </dd>
        </div>
        <div>
          <dt>Đối tượng</dt>
          <dd>
            {objectTypeLabel(item.objectType)}
            {item.objectName && ` · ${item.objectName}`}
            {item.objectId && <code className="audit-code">{item.objectId}</code>}
          </dd>
        </div>
        <div>
          <dt>Địa chỉ IP</dt>
          <dd>{item.ipAddress || <span className="audit-none">Không có</span>}</dd>
        </div>
      </dl>

      <h3 className="audit-details-title">Chi tiết</h3>
      {details.length === 0 ? (
        <p className="audit-none">Không có chi tiết.</p>
      ) : (
        <dl className="audit-facts audit-details">
          {details.map(([key, value]) => (
            <div key={key}>
              <dt>{key}</dt>
              <dd>
                <DetailValue value={value} />
              </dd>
            </div>
          ))}
        </dl>
      )}
    </Dialog>
  )
}

function DetailValue({ value }: { value: unknown }) {
  if (value === null || value === undefined || value === '') return <span className="audit-none">Chưa có</span>
  if (typeof value === 'object') return <pre className="audit-json">{JSON.stringify(value, null, 2)}</pre>
  return <span className="audit-value">{String(value)}</span>
}
