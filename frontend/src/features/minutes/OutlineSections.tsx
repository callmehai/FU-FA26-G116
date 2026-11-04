import { type ReactNode, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { formatClock } from '../../lib/format'
import type { SummaryDraft, SummarySource } from '../../types/summary'

export type OutlineKey = 'objectives' | 'discussion' | 'conclusions'
export type OutlineValues = Record<OutlineKey, string>

/** The limits of SummaryLimits in SummaryContracts.cs; the server checks them again (MSG389). */
const SECTIONS: { key: OutlineKey; heading: string; limit: number }[] = [
  { key: 'objectives', heading: 'I. Mục tiêu và phạm vi cuộc họp', limit: 2000 },
  { key: 'discussion', heading: 'II. Nội dung thảo luận và đánh giá', limit: 12000 },
  { key: 'conclusions', heading: 'III. Kết luận và phân công thực hiện', limit: 8000 },
]

/**
 * The Dự thảo as AI-eCabinet's OutlineSections shows it: the three sections of the minutes, each folded to its heading
 * and first line so the whole tab reads as one screen (eCab BR 19/10). A section opens in place; the Chair or the
 * Secretary corrects it right there, and a bar pinned to the bottom of the screen saves or undoes the changes, since a
 * Save button under three long sections is not found.
 */
export function OutlineSections({
  draft,
  editable,
  reviewed,
  showSources,
  barHost,
  onSave,
  onDirtyChange,
}: {
  draft: SummaryDraft
  editable: boolean
  reviewed: boolean
  showSources: boolean
  /** Where the save bar goes: the end of the tab's column, so it sticks to the bottom of the screen anywhere in the tab. */
  barHost: HTMLElement | null
  /** Saves; `errors` are the server's per-section messages (MSG389). A failure is already shown as a toast. */
  onSave: (values: OutlineValues) => Promise<{ saved: boolean; errors: Record<string, string> }>
  onDirtyChange: (dirty: boolean) => void
}) {
  const initial: OutlineValues = {
    objectives: draft.objectives,
    discussion: draft.discussion,
    conclusions: draft.conclusions,
  }
  const [saved, setSaved] = useState(initial)
  const [values, setValues] = useState(initial)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const [asking, setAsking] = useState<'save' | 'undo' | null>(null)
  const dirty = SECTIONS.some((s) => values[s.key] !== saved[s.key])
  const over = SECTIONS.filter((s) => values[s.key].length > s.limit)

  const change = (key: OutlineKey, text: string) => {
    const next = { ...values, [key]: text }
    setValues(next)
    onDirtyChange(SECTIONS.some((s) => next[s.key] !== saved[s.key]))
  }

  const [saving, setSaving] = useState(false)
  const save = async () => {
    setSaving(true)
    const result = await onSave(values)
    setSaving(false)
    setErrors(result.errors)
    if (!result.saved) return
    setSaved(values)
    onDirtyChange(false)
  }

  return (
    <div className="outline-sections">
      {SECTIONS.map((section) => (
        <OutlineSection
          key={section.key}
          id={`outline-${section.key}`}
          heading={section.heading}
          value={values[section.key]}
          limit={section.limit}
          editable={editable}
          error={errors[section.key]}
          onChange={(text) => change(section.key, text)}
          footer={
            section.key === 'discussion' && showSources && draft.sources.some((s) => s.from && s.to) ? (
              <Sources sources={draft.sources} />
            ) : null
          }
        />
      ))}

      {editable &&
        barHost &&
        createPortal(
          <div
            className={dirty ? 'outline-savebar is-open' : 'outline-savebar'}
            aria-hidden={!dirty || undefined}
            inert={!dirty}
          >
            <div className="outline-savebar-inner">
              <span role="status" className={over.length ? 'outline-savebar-status is-over' : 'outline-savebar-status'}>
                {over.length
                  ? `Mục ${over.map((s) => s.heading.split('.')[0]).join(', ')} quá dài`
                  : 'Dự thảo có thay đổi chưa lưu'}
              </span>
              <button
                type="button"
                className="btn btn-outline btn-sm"
                disabled={saving}
                onClick={() => setAsking('undo')}
              >
                <Icon name="undo" size={16} />
                Hoàn tác
              </button>
              <button
                type="button"
                className="btn btn-primary btn-sm"
                disabled={over.length > 0 || saving}
                onClick={() => (reviewed ? setAsking('save') : void save())}
              >
                {saving ? 'Đang lưu…' : 'Lưu'}
              </button>
            </div>
          </div>,
          barHost,
        )}

      {asking === 'undo' && (
        <ConfirmDialog
          title="Bỏ các thay đổi?"
          confirmLabel="Bỏ thay đổi"
          pendingLabel="Đang bỏ…"
          tone="danger"
          onConfirm={async () => {
            setValues(saved)
            setErrors({})
            onDirtyChange(false)
          }}
          onClose={() => setAsking(null)}
        >
          Dự thảo trở về bản đã lưu lần cuối.
        </ConfirmDialog>
      )}
      {asking === 'save' && (
        <ConfirmDialog
          title="Lưu thay đổi?"
          confirmLabel="Lưu"
          pendingLabel="Đang lưu…"
          onConfirm={save}
          onClose={() => setAsking(null)}
        >
          Dự thảo đã duyệt sẽ phải duyệt lại sau khi sửa.
        </ConfirmDialog>
      )}
    </div>
  )
}

function OutlineSection({
  id,
  heading,
  value,
  limit,
  editable,
  error,
  onChange,
  footer,
}: {
  id: string
  heading: string
  value: string
  limit: number
  editable: boolean
  error?: string
  onChange: (text: string) => void
  footer: ReactNode
}) {
  // Folded at first: three long sections read as one screen of headings (eCab BR 19/10).
  const [open, setOpen] = useState(false)
  const box = useRef<HTMLTextAreaElement>(null)
  // The box grows with its text, so a section reads whole and never in a scrolled slot.
  useLayoutEffect(() => {
    const el = box.current
    if (!el) return
    el.style.height = 'auto'
    el.style.height = `${el.scrollHeight + 2}px`
  }, [value, open])
  const tooLong = value.length > limit
  const showCount = editable && open && value.length >= limit * 0.8
  const first =
    value
      .trim()
      .split('\n')
      .find((line) => line.trim()) ?? ''

  return (
    <div className={open ? 'outline-section is-open' : 'outline-section'}>
      <button
        type="button"
        className="outline-toggle"
        aria-expanded={open}
        aria-controls={`${id}-body`}
        onClick={() => setOpen((o) => !o)}
      >
        <Icon name="chevronDown" size={16} />
        <span id={`${id}-heading`} className="outline-heading">
          {heading}
        </span>
        {!open && (
          <span className="outline-preview" aria-hidden="true">
            {first || 'Chưa có nội dung'}
          </span>
        )}
        {!open && (tooLong || error) && <span className="outline-flag">{error ? 'Cần sửa' : 'Quá dài'}</span>}
      </button>
      {open && (
        <div id={`${id}-body`} className="outline-body">
          {editable ? (
            <textarea
              id={id}
              ref={box}
              rows={3}
              value={value}
              maxLength={Math.max(limit, value.length)}
              spellCheck={false}
              aria-labelledby={`${id}-heading`}
              aria-invalid={tooLong || error ? true : undefined}
              aria-describedby={error ? `${id}-error` : showCount ? `${id}-count` : undefined}
              onChange={(event) => onChange(event.target.value)}
            />
          ) : (
            <OutlineText text={value} />
          )}
          {error && (
            <span id={`${id}-error`} className="field-error">
              {error}
            </span>
          )}
          {showCount && (
            <span id={`${id}-count`} className={tooLong ? 'outline-count is-over' : 'outline-count'}>
              {value.length.toLocaleString('vi-VN')}/{limit.toLocaleString('vi-VN')} ký tự
            </span>
          )}
          {footer}
        </div>
      )}
    </div>
  )
}

/** Where each part of section II came from: the transcript between these times, for the Secretary to check. */
function Sources({ sources }: { sources: SummarySource[] }) {
  return (
    <p className="minutes-sources">
      <span className="field-hint">Lấy từ bản ghi:</span>
      {sources
        .filter((s) => s.from && s.to)
        .map((s) => (
          <span key={`${s.agendaItemId ?? s.title}`} className="minutes-source" data-tooltip={`${s.lines} câu nói`}>
            {s.title}: {formatClock(s.from!)}–{formatClock(s.to!)}
          </span>
        ))}
    </p>
  )
}

/** A section's text; its numbered sub-headings ("1. <nội dung>", "1. Kết luận của chủ trì") stand out in bold. */
function OutlineText({ text }: { text: string }) {
  return (
    <div className="minutes-outline-text">
      {text.split('\n').map((line, i) =>
        /^\d{1,2}\.\s+\S.{0,150}$/.test(line.trim()) ? (
          <p key={i} className="minutes-outline-heading">
            {line}
          </p>
        ) : (
          <p key={i}>{line || ' '}</p>
        ),
      )}
    </div>
  )
}
