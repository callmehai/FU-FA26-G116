import { useState } from 'react'
import { Link } from 'react-router-dom'
import { removeMeetingDocument, unlinkFromAgendaItem } from '../../api/documents'
import { Badge } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { SegmentedControl } from '../../components/SegmentedControl'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import { routes } from '../../lib/routes'
import type { DocumentsAgendaItem, MeetingDocument, MeetingDocumentList } from '../../types/document'
import type { Meeting } from '../../types/meeting'
import { AddDocumentsDialog } from './AddDocumentsDialog'
import { documentFacts, isPdf } from './documentFile'
import { FileTypeTag, OnlineOnly } from './DocumentTags'
import { useDocumentDownload } from './useDocumentDownload'
import { useMeetingDocuments } from './useMeetingDocuments'

type Group = 'agenda' | 'general'

const GROUPS = [
  { value: 'agenda', label: 'Theo nội dung họp', short: 'Nội dung họp' },
  // "Biểu quyết" (UC-32a AF-1) waits for the table linking documents to votes (UC-74).
  { value: 'general', label: 'Tài liệu chung' },
] satisfies { value: Group; label: string; short?: string }[]

/**
 * The meeting's documents (SCRUM-21, UC-32a), written once for the Tài liệu tab and the call room's
 * side panel (ADR-0011). It reads its own list and re-reads on the hub.
 *
 * - `page`: the AI-eCabinet layout — by agenda item (a document linked to several items under each,
 *   BR-277) or the general ones (BR-280). While the meeting has not ended or been cancelled, its Chair
 *   and Secretary add documents (SCRUM-107, SCRUM-58: from the computer or their library, to the general
 *   group or one item), detach one from an item and remove one from the meeting (BR-184, BR-279, BR-299);
 *   the server checks again.
 * - `room`: read only (BR-316). The item in progress comes first and follows the Chair; a document
 *   opens in a new tab so the call keeps running (BR-315).
 *
 * Status, sensitivity and the caller's role come from the meeting, which the list does not repeat.
 * `onMeetingStale` re-reads it when an answer says it changed (role lost, meeting ended, now Secret).
 */
export function DocumentsPanel({
  meeting,
  variant,
  onMeetingStale,
}: {
  meeting: Meeting
  variant: 'page' | 'room'
  onMeetingStale?: () => void
}) {
  const { state, reload } = useMeetingDocuments(meeting.id)
  const data = state.kind === 'loaded' || state.kind === 'error' ? state.data : null
  const stale = () => {
    void reload()
    onMeetingStale?.()
  }
  const { download, busyId } = useDocumentDownload(meeting.id, stale)
  const canDownload = meeting.sensitivity !== 'Secret'
  const total = data && distinctCount([...data.agendaItems.flatMap((item) => item.documents), ...data.general])

  const rowProps = { meetingId: meeting.id, variant, canDownload, busyId, onDownload: download }
  const body = !data ? null : variant === 'room' ? (
    <RoomList data={data} row={rowProps} />
  ) : (
    <PageList data={data} meeting={meeting} row={rowProps} onUploaded={() => void reload()} onStale={stale} />
  )

  return (
    <div className={variant === 'room' ? 'room-panel-body documents-panel' : 'documents-panel'}>
      {variant === 'room' && (
        <>
          <h2 className="room-panel-title">Tài liệu{total !== null && ` (${total})`}</h2>
          {!canDownload && <OnlineOnly />}
        </>
      )}
      {state.kind === 'gone' && <p className="cell-sub">Bạn không còn xem được tài liệu của cuộc họp này.</p>}
      {state.kind === 'error' && (
        <div className="alert alert-error" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void reload()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}
      {state.kind === 'loading' && <ListSkeleton />}
      {body}
    </div>
  )
}

type RowProps = {
  meetingId: string
  variant: 'page' | 'room'
  canDownload: boolean
  busyId: string | null
  onDownload: (doc: MeetingDocument) => void
  /** The Chair's and Secretary's: detach from `item`, or remove from the meeting when `item` is absent. */
  onRemove?: (doc: MeetingDocument, item?: DocumentsAgendaItem) => void
}

/** What a remove answer means the page is out of date: role lost, meeting ended or cancelled, item or document gone. */
const staleRemove = (err: unknown) =>
  isApiError(err, 404, 'MSG174') ||
  isApiError(err, 404, 'MSG170') ||
  isApiError(err, 404, 'MSG242') ||
  isApiError(err, 404, 'MSG246') ||
  isApiError(err, 403, 'MSG169') ||
  isApiError(err, 403, 'MSG241') ||
  isApiError(err, 409, 'MSG248') ||
  isApiError(err, 409, 'MSG247') ||
  isApiError(err, 409, 'MSG349')

function PageList({
  data,
  meeting,
  row,
  onUploaded,
  onStale,
}: {
  data: MeetingDocumentList
  meeting: Meeting
  row: RowProps
  onUploaded: () => void
  onStale: () => void
}) {
  const toast = useToast()
  const [group, setGroup] = useState<Group>('agenda')
  /** The add dialog: for the general group (`item` absent) or one agenda item. */
  const [adding, setAdding] = useState<{ item?: DocumentsAgendaItem; number?: number } | null>(null)
  const [removing, setRemoving] = useState<{ doc: MeetingDocument; item?: DocumentsAgendaItem } | null>(null)
  const canManage =
    meeting.status !== 'Ended' &&
    meeting.status !== 'Cancelled' &&
    (meeting.myMeetingRole === 'Chair' || meeting.myMeetingRole === 'Secretary')
  // A document linked to several items counts once, as the room panel's total does.
  const all = distinctDocuments([...data.agendaItems.flatMap((item) => item.documents), ...data.general])
  const agendaCount = distinctCount(data.agendaItems.flatMap((item) => item.documents))
  const managed = canManage
    ? { ...row, onRemove: (doc: MeetingDocument, item?: DocumentsAgendaItem) => setRemoving({ doc, item }) }
    : row

  return (
    <>
      <div className="documents-toolbar">
        <SegmentedControl
          label="Nhóm tài liệu"
          value={group}
          options={GROUPS}
          counts={{ agenda: agendaCount, general: data.general.length }}
          onChange={setGroup}
        />
        {canManage && (
          <button type="button" className="btn btn-primary" onClick={() => setAdding({})}>
            <Icon name="plus" size={16} />
            Thêm tài liệu chung
          </button>
        )}
      </div>

      <div className="card documents-card">
        {group === 'agenda' ? (
          data.agendaItems.length === 0 ? (
            <p className="documents-empty">Cuộc họp chưa có mục nghị sự.</p>
          ) : (
            data.agendaItems.map((item, i) => (
              <AgendaGroup
                key={item.id}
                item={item}
                number={i + 1}
                row={managed}
                onAdd={canManage ? () => setAdding({ item, number: i + 1 }) : undefined}
              />
            ))
          )
        ) : data.general.length === 0 ? (
          <p className="documents-empty">Chưa có tài liệu chung.</p>
        ) : (
          <DocumentRows documents={data.general} row={managed} />
        )}
      </div>

      {adding && (
        <AddDocumentsDialog
          meeting={meeting}
          item={
            adding.item && {
              id: adding.item.id,
              number: adding.number!,
              title: adding.item.title,
              documentIds: adding.item.documents.map((d) => d.id),
            }
          }
          meetingDocuments={all}
          onClose={() => setAdding(null)}
          onAdded={(message) => {
            // What was added to the general group shows there; an item's documents stay under the item.
            if (!adding.item) setGroup('general')
            setAdding(null)
            toast.show(message)
            onUploaded()
          }}
          onStale={(message) => {
            setAdding(null)
            toast.show(message, 'error')
            onStale()
          }}
        />
      )}

      {removing && (
        <ConfirmDialog
          title={removing.item ? 'Gỡ tài liệu khỏi mục này?' : 'Gỡ tài liệu khỏi cuộc họp?'}
          confirmLabel={removing.item ? 'Gỡ khỏi mục' : 'Gỡ khỏi cuộc họp'}
          pendingLabel="Đang gỡ…"
          cancelLabel="Giữ tài liệu"
          tone={removing.item ? 'primary' : 'danger'}
          onConfirm={async () => {
            const { doc, item } = removing
            try {
              const done = item
                ? await unlinkFromAgendaItem(meeting.id, item.id, doc.id)
                : await removeMeetingDocument(meeting.id, doc.id)
              setRemoving(null)
              toast.show(done.message)
              onUploaded()
            } catch (err) {
              if (!staleRemove(err)) throw err
              setRemoving(null)
              toast.show(errorMessage(err), 'error')
              onStale()
            }
          }}
          onClose={() => setRemoving(null)}
        >
          {removing.item ? (
            <>
              <strong>{removing.doc.fileName}</strong> vẫn còn trong cuộc họp, chỉ không còn gắn với mục “
              {removing.item.title}”.
            </>
          ) : (
            <>
              <strong>{removing.doc.fileName}</strong> không còn trong cuộc họp này, góp ý trên tài liệu cũng không còn
              hiện. Bản trong Kho tài liệu của người đã tải lên vẫn giữ nguyên.
            </>
          )}
        </ConfirmDialog>
      )}
    </>
  )
}

/** The room's order: the item under discussion first and open, the others folded, then the general ones. */
function RoomList({ data, row }: { data: MeetingDocumentList; row: RowProps }) {
  // Numbered by order, as the agenda reads: positions may have gaps after a reorder.
  const numbered = data.agendaItems.map((item, i) => ({ item, number: i + 1 }))
  const current = numbered.find(({ item }) => item.id === data.currentAgendaItemId)
  const others = numbered.filter((entry) => entry !== current)

  return (
    <>
      {current && (
        <section className="documents-section" aria-label="Mục đang bàn">
          <h3 className="documents-section-title">
            Đang bàn: {current.number}. {current.item.title}
          </h3>
          {current.item.documents.length === 0 ? (
            <p className="documents-empty">Mục này chưa có tài liệu.</p>
          ) : (
            <DocumentRows documents={current.item.documents} row={row} />
          )}
        </section>
      )}
      {others.length > 0 && (
        <section className="documents-section">
          <h3 className="documents-section-title">{current ? 'Mục khác' : 'Nội dung họp'}</h3>
          {/* Keyed by the current item, so the folds reset when the Chair moves on. */}
          <div key={current?.item.id}>
            {others.map(({ item, number }) => (
              <AgendaGroup key={item.id} item={item} number={number} row={row} folded />
            ))}
          </div>
        </section>
      )}
      <section className="documents-section">
        <h3 className="documents-section-title">Tài liệu chung</h3>
        {data.general.length === 0 ? (
          <p className="documents-empty">Chưa có tài liệu chung.</p>
        ) : (
          <DocumentRows documents={data.general} row={row} />
        )}
      </section>
    </>
  )
}

/** One agenda item: a fold with its count. Items with documents start open on the page, folded in the room. */
function AgendaGroup({
  item,
  number,
  row,
  folded,
  onAdd,
}: {
  item: DocumentsAgendaItem
  number: number
  row: RowProps
  folded?: boolean
  /** The Chair's and Secretary's "add to this item" (UC-33b). */
  onAdd?: () => void
}) {
  const count = item.documents.length
  // Until the user folds or opens it, it follows its content: an item that gains a document opens.
  const [toggled, setToggled] = useState<boolean | null>(null)
  const open = toggled ?? (!folded && count > 0)

  return (
    <div className="documents-group">
      <div className="documents-group-bar">
        <button type="button" className="documents-group-head" aria-expanded={open} onClick={() => setToggled(!open)}>
          <Icon name={open ? 'chevronDown' : 'chevronRight'} size={16} />
          <span className="documents-group-title">
            {number}. {item.title}
          </span>
          <Badge tone="outline">{row.variant === 'room' ? count : `${count} tài liệu`}</Badge>
        </button>
        {onAdd && (
          <button
            type="button"
            className="icon-button icon-button-sm"
            aria-label={`Thêm tài liệu cho mục ${number}`}
            data-tooltip="Thêm tài liệu cho mục này"
            onClick={onAdd}
          >
            <Icon name="paperclip" size={16} />
          </button>
        )}
      </div>
      {open &&
        (count === 0 ? (
          <p className="documents-empty documents-empty-nested">Mục này chưa có tài liệu.</p>
        ) : (
          <DocumentRows documents={item.documents} row={row} item={item} />
        ))}
    </div>
  )
}

function DocumentRows({
  documents,
  row,
  item,
}: {
  documents: MeetingDocument[]
  row: RowProps
  /** The agenda item these rows sit under, if any. */
  item?: DocumentsAgendaItem
}) {
  return (
    <ul className="documents-rows">
      {documents.map((doc) => (
        <DocumentRow key={doc.id} doc={doc} row={row} item={item} />
      ))}
    </ul>
  )
}

/**
 * Name, type, page count and comments; who added it and when sit in the tooltip. In the room the
 * whole row is a link to a new tab; on the page it opens the viewer in place.
 */
function DocumentRow({ doc, row, item }: { doc: MeetingDocument; row: RowProps; item?: DocumentsAgendaItem }) {
  const facts = documentFacts(doc)
  const room = row.variant === 'room'
  const added = `${doc.addedBy.fullName} thêm lúc ${formatDateTime(doc.addedAt)}`

  return (
    <li className="documents-row">
      <Link
        className="documents-row-link"
        to={`${routes.meetingDocument(row.meetingId, doc.id)}${room ? '?from=room' : ''}`}
        data-tooltip={room ? 'Mở ở tab mới, bạn vẫn ở trong phòng' : added}
        {...(room && { target: '_blank', rel: 'noopener' })}
      >
        <FileTypeTag pdf={isPdf(doc)} />
        <span className="documents-row-name">
          <strong>{doc.fileName}</strong>
          {facts && <small>{facts}</small>}
        </span>
        {room && <Icon name="externalLink" size={16} />}
      </Link>
      {!room && row.canDownload && (
        <button
          type="button"
          className="icon-button icon-button-sm"
          aria-label={`Tải về ${doc.fileName}`}
          data-tooltip="Tải về"
          disabled={row.busyId === doc.id}
          onClick={() => row.onDownload(doc)}
        >
          <Icon name="download" size={16} />
        </button>
      )}
      {!room && row.onRemove && item && (
        <button
          type="button"
          className="icon-button icon-button-sm"
          aria-label={`Gỡ ${doc.fileName} khỏi mục này`}
          data-tooltip="Gỡ khỏi mục này"
          onClick={() => row.onRemove!(doc, item)}
        >
          <Icon name="unlink" size={16} />
        </button>
      )}
      {!room && row.onRemove && (
        <button
          type="button"
          className="icon-button icon-button-sm"
          aria-label={`Gỡ ${doc.fileName} khỏi cuộc họp`}
          data-tooltip="Gỡ khỏi cuộc họp"
          onClick={() => row.onRemove!(doc)}
        >
          <Icon name="trash" size={16} />
        </button>
      )}
    </li>
  )
}

/** A document linked to several items counts once. */
function distinctCount(documents: MeetingDocument[]) {
  return new Set(documents.map((d) => d.id)).size
}

function distinctDocuments(documents: MeetingDocument[]) {
  return [...new Map(documents.map((d) => [d.id, d])).values()]
}

function ListSkeleton() {
  return (
    <ul className="documents-rows" aria-busy="true" aria-label="Đang tải tài liệu">
      {Array.from({ length: 3 }, (_, i) => (
        <li key={i} className="documents-row">
          <span className="skeleton documents-skeleton" />
        </li>
      ))}
    </ul>
  )
}
