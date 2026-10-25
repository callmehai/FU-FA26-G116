import {
  closestCenter,
  DndContext,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type Announcements,
  type DragEndEvent,
} from '@dnd-kit/core'
import {
  arrayMove,
  SortableContext,
  sortableKeyboardCoordinates,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { useCallback, useState, type ReactNode } from 'react'
import { completeAgendaItem, deleteAgendaItem, startAgendaItem } from '../../api/agenda'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { avatarColors, initials } from '../../lib/avatar'
import { formatClock } from '../../lib/format'
import type { AgendaItem, AgendaMutationResponse } from '../../types/agenda'
import { AgendaConclusion } from './AgendaConclusion'
import { AgendaItemDialog } from './AgendaItemDialog'
import { useAgenda } from './useAgenda'

type Open = { kind: 'edit'; item: AgendaItem | null } | { kind: 'delete'; item: AgendaItem }

/**
 * The meeting's agenda (SCRUM-25, SCRUM-98, UC-77), written once for the detail tab and the call
 * room's side panel (ADR-0011). It reads its own data and what the caller may do comes from the
 * server's `actions`: the Secretary plans (add, edit, delete, drag to reorder), the Chair starts and
 * closes items while the meeting runs, either writes conclusions. Laid out as AI-eCabinet does: one
 * line per item, opened in place for its notes, documents and conclusion.
 */
export function AgendaPanel({ meetingId, variant }: { meetingId: string; variant: 'page' | 'room' }) {
  const toast = useToast()
  const { state, reload, run, reorder, reordering } = useAgenda(meetingId)
  const [open, setOpen] = useState<Open | null>(null)
  // The item under discussion starts open; the rest open on demand.
  const [expanded, setExpanded] = useState<ReadonlySet<string> | null>(null)

  const saved = useCallback(
    (response: AgendaMutationResponse) => {
      toast.show(response.message)
      void reload()
    },
    [toast, reload],
  )
  const stale = useCallback(() => void reload(), [reload])

  const sensors = useSensors(
    // A few pixels of travel before a drag starts, so a tap on the handle is not a drop.
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  const room = variant === 'room'
  const wrap = (body: ReactNode) =>
    room ? (
      <div className="room-panel-body agenda-room">
        <h2 className="room-panel-title">Nội dung họp</h2>
        {body}
      </div>
    ) : (
      body
    )

  if (state.kind === 'gone') {
    return wrap(
      <div className="card card-pending tab-pending">
        <p>Bạn không còn xem được nội dung của cuộc họp này.</p>
      </div>,
    )
  }
  const data = state.kind === 'loading' ? null : state.data
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
        <AgendaSkeleton />
      ),
    )
  }

  const items = data.items
  const current = items.find((i) => i.status === 'InProgress')
  const isOpen = (id: string) => (expanded ?? new Set(current ? [current.id] : [])).has(id)
  const toggle = (id: string) =>
    setExpanded((set) => {
      const next = new Set(set ?? (current ? [current.id] : []))
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  // Dragging needs room for the handle and a pointer; the room's narrow panel only shows the order.
  const sortable = data.permissions.canReorder && !room
  const titleOf = (id: string | number) => items.find((i) => i.id === id)?.title ?? ''
  const positionOf = (id: string | number) => items.findIndex((i) => i.id === id) + 1
  const announcements: Announcements = {
    onDragStart: ({ active }) => `Đã chọn "${titleOf(active.id)}" ở vị trí ${positionOf(active.id)}.`,
    onDragOver: ({ active, over }) =>
      over ? `"${titleOf(active.id)}" đang ở vị trí ${positionOf(over.id)} trên ${items.length}.` : undefined,
    onDragEnd: ({ active, over }) =>
      over ? `Đã chuyển "${titleOf(active.id)}" tới vị trí ${positionOf(over.id)} trên ${items.length}.` : undefined,
    onDragCancel: ({ active }) => `Đã huỷ, "${titleOf(active.id)}" giữ nguyên vị trí.`,
  }
  const onDragEnd = ({ active, over }: DragEndEvent) => {
    if (!over || active.id === over.id) return
    const ids = items.map((i) => i.id)
    void reorder(arrayMove(ids, ids.indexOf(String(active.id)), ids.indexOf(String(over.id))))
  }

  const list = (
    <ol className="agenda-list">
      {items.map((item, index) => (
        <AgendaRow
          key={item.id}
          item={item}
          number={index + 1}
          sortable={sortable}
          locked={reordering}
          expanded={isOpen(item.id)}
          onToggle={() => toggle(item.id)}
          onEdit={() => setOpen({ kind: 'edit', item })}
          onDelete={() => setOpen({ kind: 'delete', item })}
          onStart={() => void run(() => startAgendaItem(meetingId, item.id))}
          onComplete={() => void run(() => completeAgendaItem(meetingId, item.id))}
        >
          <AgendaConclusion meetingId={meetingId} item={item} onSaved={saved} onStale={stale} />
        </AgendaRow>
      ))}
    </ol>
  )

  return wrap(
    <section
      className={room ? 'agenda-panel agenda-panel-room' : 'list-panel agenda-panel'}
      aria-label="Nội dung cuộc họp"
    >
      {/* The tab already says what this is; the bar holds only the Secretary's add button. */}
      {data.permissions.canCreate && (
        <div className="toolbar agenda-toolbar">
          <button
            type="button"
            className="btn btn-outline btn-sm toolbar-end"
            onClick={() => setOpen({ kind: 'edit', item: null })}
          >
            <Icon name="plus" size={16} />
            Thêm nội dung
          </button>
        </div>
      )}

      {state.kind === 'error' && (
        <div className="alert alert-error agenda-alert" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void reload()}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      )}

      {items.length === 0 ? (
        <div className="agenda-empty">
          <Icon name="list" size={24} />
          <p>Chưa có nội dung nào</p>
          <small>
            {data.permissions.canCreate
              ? 'Thêm các nội dung cần bàn trong cuộc họp.'
              : 'Các nội dung cần bàn sẽ hiện ở đây khi được lập.'}
          </small>
        </div>
      ) : sortable ? (
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragEnd={onDragEnd}
          accessibility={{
            announcements,
            screenReaderInstructions: {
              draggable:
                'Nhấn phím cách để nhấc nội dung lên, dùng phím mũi tên để di chuyển, phím cách để thả, Esc để huỷ.',
            },
          }}
        >
          <SortableContext items={items.map((i) => i.id)} strategy={verticalListSortingStrategy}>
            {list}
          </SortableContext>
        </DndContext>
      ) : (
        list
      )}

      {open?.kind === 'edit' && (
        <AgendaItemDialog
          meetingId={meetingId}
          item={open.item}
          onSaved={saved}
          onStale={stale}
          onClose={() => setOpen(null)}
        />
      )}
      {open?.kind === 'delete' && (
        <ConfirmDialog
          title="Xoá nội dung"
          tone="danger"
          confirmLabel="Xoá"
          pendingLabel="Đang xoá…"
          onConfirm={async () => {
            // A failure stays in the dialog (it would cover a toast); the list re-reads behind it.
            try {
              saved(await deleteAgendaItem(meetingId, open.item.id))
            } catch (err) {
              stale()
              throw err
            }
          }}
          onClose={() => setOpen(null)}
        >
          Xoá nội dung “{open.item.title}”? Tài liệu đã gắn vẫn còn trong tab Tài liệu.
        </ConfirmDialog>
      )}
    </section>,
  )
}

function AgendaRow({
  item,
  number,
  sortable,
  locked,
  expanded,
  onToggle,
  onEdit,
  onDelete,
  onStart,
  onComplete,
  children,
}: {
  item: AgendaItem
  number: number
  sortable: boolean
  /** A drop is being saved: the handle stays, the next drag waits for the new order. */
  locked: boolean
  expanded: boolean
  onToggle: () => void
  onEdit: () => void
  onDelete: () => void
  onStart: () => void
  onComplete: () => void
  children: ReactNode
}) {
  const { attributes, listeners, setNodeRef, setActivatorNodeRef, transform, transition, isDragging } = useSortable({
    id: item.id,
    disabled: !sortable || locked,
  })
  const { actions } = item
  const bodyId = `agenda-body-${item.id}`
  const classes = ['agenda-item']
  if (item.status === 'InProgress') classes.push('agenda-item-current')
  if (expanded) classes.push('agenda-item-open')
  if (isDragging) classes.push('agenda-item-dragging')

  return (
    <li
      ref={setNodeRef}
      className={classes.join(' ')}
      style={{ transform: CSS.Translate.toString(transform), transition }}
    >
      <div className="agenda-head">
        {sortable && (
          <button
            type="button"
            ref={setActivatorNodeRef}
            className="agenda-grip"
            aria-label={`Kéo để đổi vị trí: ${item.title}`}
            data-tooltip="Kéo để đổi vị trí"
            {...attributes}
            {...listeners}
          >
            <Icon name="grip" size={16} />
          </button>
        )}
        <span className="agenda-number" aria-hidden="true">
          {number}
        </span>
        <button
          type="button"
          className="agenda-title"
          aria-expanded={expanded}
          aria-controls={bodyId}
          onClick={onToggle}
        >
          <Icon name={expanded ? 'chevronDown' : 'chevronRight'} size={16} />
          <span>{item.title}</span>
        </button>

        {(actions.canSetCurrent || actions.canComplete || actions.canEdit || actions.canDelete) && (
          <div className="row-actions agenda-actions">
            {actions.canSetCurrent && (
              <button type="button" className="btn btn-outline btn-sm" onClick={onStart}>
                <Icon name="play" size={16} />
                Bắt đầu
              </button>
            )}
            {actions.canComplete && (
              <button type="button" className="btn btn-live btn-sm" onClick={onComplete}>
                <Icon name="check" size={16} />
                Xong
              </button>
            )}
            {actions.canEdit && (
              <button
                type="button"
                className="icon-button icon-button-sm"
                onClick={onEdit}
                aria-label={`Sửa: ${item.title}`}
                data-tooltip="Sửa"
              >
                <Icon name="pencil" size={16} />
              </button>
            )}
            {/* A started item cannot be deleted: keep its slot, so every row's pencil lines up. */}
            {actions.canEdit && !actions.canDelete && <span className="agenda-action-slot" aria-hidden="true" />}
            {actions.canDelete && (
              <button
                type="button"
                className="icon-button icon-button-sm icon-button-danger"
                onClick={onDelete}
                aria-label={`Xoá: ${item.title}`}
                data-tooltip="Xoá"
              >
                <Icon name="trash" size={16} />
              </button>
            )}
          </div>
        )}
        {/* The facts go on a second line under the title, unless the panel is wide enough for one. */}
        <span className="agenda-break" aria-hidden="true" />
        <div className="agenda-meta">
          <Presenter item={item} />
          <StatusChip item={item} />
          {item.conclusion && (
            <span className="status status-violet agenda-concluded" data-tooltip="Đã có kết luận">
              <Icon name="clipboardCheck" size={14} />
              <span className="agenda-concluded-text">Đã kết luận</span>
            </span>
          )}
          <span
            className={item.documents.length ? 'agenda-docs' : 'agenda-docs agenda-docs-none'}
            data-tooltip={item.documents.length ? item.documents.map((d) => d.title).join('\n') : undefined}
          >
            <Icon name="paperclip" size={14} />
            {item.documents.length}
          </span>
        </div>
      </div>

      {expanded && (
        <div className="agenda-body" id={bodyId}>
          {item.description && <p className="agenda-text agenda-note">{item.description}</p>}
          <div className="agenda-boxes">
            <div className="agenda-box">
              <div className="agenda-box-head">
                <h4>Tài liệu</h4>
              </div>
              {item.documents.length ? (
                <ul className="agenda-doc-list">
                  {item.documents.map((doc) => (
                    <li key={doc.id}>
                      <Icon name="fileText" size={16} />
                      <span>{doc.title}</span>
                    </li>
                  ))}
                </ul>
              ) : (
                <p className="value-empty">Chưa có tài liệu.</p>
              )}
            </div>
            {children}
          </div>
        </div>
      )}
    </li>
  )
}

/** The presenter, or why there is none; the name of a former presenter is history, shown muted. */
function Presenter({ item }: { item: AgendaItem }) {
  if (item.presenterName) {
    const colors = avatarColors(item.presenterName)
    const guest = item.presenterGuestInvitationId !== null
    return (
      <span
        className="agenda-presenter"
        data-tooltip={`Người trình bày: ${item.presenterName}${guest ? ' (khách)' : ''}`}
      >
        <span className="avatar avatar-xs" style={{ backgroundColor: colors.bg, color: colors.fg }} aria-hidden="true">
          {initials(item.presenterName)}
        </span>
        <span className="agenda-presenter-name">{item.presenterName}</span>
      </span>
    )
  }
  return (
    <span
      className="agenda-presenter agenda-presenter-none"
      data-tooltip={
        item.legacyPresenterName
          ? `Trước đây: ${item.legacyPresenterName}. Chưa có người trình bày.`
          : 'Chưa có người trình bày'
      }
    >
      <span className="avatar avatar-xs avatar-empty" aria-hidden="true" />
      <span className="agenda-presenter-name">{item.legacyPresenterName ?? 'Chưa phân công'}</span>
    </span>
  )
}

/** Under way or done, as icon and text (ADR-0028), real times on hover; a pending item shows nothing. */
function StatusChip({ item }: { item: AgendaItem }) {
  if (item.status === 'InProgress') {
    return (
      <span
        className="status status-live agenda-status"
        data-tooltip={item.actualStartedAt ? `Bắt đầu ${formatClock(item.actualStartedAt)}` : undefined}
      >
        Đang bàn
      </span>
    )
  }
  if (item.status === 'Done') {
    const times =
      item.actualStartedAt && item.actualEndedAt
        ? `${formatClock(item.actualStartedAt)} – ${formatClock(item.actualEndedAt)}`
        : undefined
    return (
      <span className="status status-ended agenda-status" data-tooltip={times}>
        <Icon name="checkCheck" size={14} />
        Đã xong
      </span>
    )
  }
  return null
}

/** Shaped like the loaded list, so nothing jumps when it arrives. */
function AgendaSkeleton() {
  return (
    <ol className="agenda-list" aria-busy="true" aria-label="Đang tải nội dung cuộc họp">
      {Array.from({ length: 3 }, (_, i) => (
        <li key={i} className="agenda-item">
          <div className="agenda-head">
            <span className="skeleton agenda-skeleton" />
          </div>
        </li>
      ))}
    </ol>
  )
}
