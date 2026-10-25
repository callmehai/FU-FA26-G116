import { useState } from 'react'
import { saveAgendaConclusion } from '../../api/agenda'
import { Icon } from '../../components/Icon'
import { errorMessage } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import type { AgendaItem, AgendaMutationResponse } from '../../types/agenda'

const CONCLUSION_MAX = 2000

/**
 * UC-77: an item's conclusion, written in place by the Chair or a Secretary while the item is under
 * way or done and no minutes exist yet. It cannot be emptied once written, and the last save wins:
 * the server stamps who saved it and when.
 */
export function AgendaConclusion({
  meetingId,
  item,
  onSaved,
  onStale,
}: {
  meetingId: string
  item: AgendaItem
  onSaved: (response: AgendaMutationResponse) => void
  onStale: () => void
}) {
  const [draft, setDraft] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const canWrite = item.actions.canRecordConclusion
  const trimmed = draft?.trim() ?? ''

  async function save() {
    setPending(true)
    setError(null)
    try {
      onSaved(await saveAgendaConclusion(meetingId, item.id, trimmed))
      setDraft(null)
    } catch (err) {
      // MSG273 (minutes now exist, the meeting state moved on) or MSG174: keep the text, say why.
      setError(errorMessage(err))
      onStale()
    } finally {
      setPending(false)
    }
  }

  return (
    <div className="agenda-box">
      <div className="agenda-box-head">
        <h4>Kết luận</h4>
        {canWrite && draft === null && (
          <button type="button" className="btn btn-outline btn-sm" onClick={() => setDraft(item.conclusion ?? '')}>
            <Icon name="pencil" size={16} />
            {item.conclusion ? 'Sửa kết luận' : 'Ghi kết luận'}
          </button>
        )}
      </div>

      {draft !== null ? (
        <div className="agenda-conclusion-editor">
          <textarea
            rows={4}
            value={draft}
            onChange={(e) => setDraft(e.target.value)}
            maxLength={CONCLUSION_MAX}
            placeholder="Nhập kết luận của nội dung này…"
            aria-label={`Kết luận: ${item.title}`}
            // Opened on purpose by the button just above, so the caret goes straight into it.
            autoFocus
          />
          <span className="field-foot">
            {error ? <small className="field-error">{error}</small> : <span />}
            <small className="field-counter">
              {draft.length.toLocaleString('vi-VN')}/{CONCLUSION_MAX.toLocaleString('vi-VN')}
            </small>
          </span>
          <div className="agenda-conclusion-actions">
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => {
                setDraft(null)
                setError(null)
              }}
              disabled={pending}
            >
              Huỷ
            </button>
            <button
              type="button"
              className="btn btn-primary btn-sm"
              onClick={save}
              disabled={pending || !trimmed || trimmed === item.conclusion}
            >
              {pending ? 'Đang lưu…' : 'Lưu kết luận'}
            </button>
          </div>
        </div>
      ) : item.conclusion ? (
        <>
          <p className="agenda-text">{item.conclusion}</p>
          {item.conclusionUpdatedAt && (
            <p className="agenda-stamp">
              {item.conclusionUpdatedByName ?? 'Không rõ'} · {formatDateTime(item.conclusionUpdatedAt)}
            </p>
          )}
        </>
      ) : (
        <p className="value-empty">Chưa có kết luận.</p>
      )}
    </div>
  )
}
