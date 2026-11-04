import { useCallback, useEffect, useState } from 'react'
import {
  createMinutes,
  editSummary,
  exportMinutes,
  getMinutes,
  getSummary,
  requestSummary,
  reviewSummary,
  saveMinutes,
} from '../../api/summary'
import { Badge } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { InfoTip } from '../../components/InfoTip'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatClock, formatDateTime } from '../../lib/format'
import type { Meeting } from '../../types/meeting'
import type { MinutesState, SummaryReadiness, SummaryState } from '../../types/summary'
import { saveBlob } from '../documents/documentFile'
import { MinutesPaper } from './MinutesPaper'

type Load<T> = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'ready'; data: T }

const SECTIONS = [
  { key: 'objectives', heading: 'I. Mục tiêu và phạm vi cuộc họp', rows: 4 },
  { key: 'discussion', heading: 'II. Nội dung thảo luận và đánh giá', rows: 14 },
  { key: 'conclusions', heading: 'III. Kết luận và phân công thực hiện', rows: 10 },
] as const

/**
 * The "Biên bản" tab (SCRUM-36, SCRUM-32), laid out as AI-eCabinet's MinutesPanel: the AI's Dự thảo first — asked
 * for, read, corrected and reviewed by the Chair or the Secretary — then the minutes built from the one fixed template
 * (Decree 30's Mẫu 1.4) with the reviewed Dự thảo in sections I–III. Writing a draft takes minutes on a laptop, so the
 * card polls while it waits.
 */
export function MinutesPanel({ meeting }: { meeting: Meeting }) {
  const [summary, setSummary] = useState<Load<SummaryState>>({ kind: 'loading' })
  const [minutes, setMinutes] = useState<Load<MinutesState>>({ kind: 'loading' })

  const loadSummary = useCallback(async () => {
    try {
      setSummary({ kind: 'ready', data: await getSummary(meeting.id) })
    } catch (error) {
      setSummary({ kind: 'error', message: errorMessage(error) })
    }
  }, [meeting.id])
  const loadMinutes = useCallback(async () => {
    try {
      setMinutes({ kind: 'ready', data: await getMinutes(meeting.id) })
    } catch (error) {
      setMinutes({ kind: 'error', message: errorMessage(error) })
    }
  }, [meeting.id])

  useEffect(() => {
    void loadSummary()
    void loadMinutes()
  }, [loadSummary, loadMinutes])

  // Poll while the AI writes, as eCab's pipeline chip does (every 5 s until the job is done).
  const waiting = summary.kind === 'ready' && ['Queued', 'Running'].includes(summary.data.summary?.status ?? '')
  useEffect(() => {
    if (!waiting) return
    const timer = window.setInterval(() => void loadSummary(), 5000)
    return () => window.clearInterval(timer)
  }, [waiting, loadSummary])

  return (
    <div className="minutes-tab">
      <SummaryCard meeting={meeting} state={summary} onChanged={setSummary} onRetry={() => void loadSummary()} />
      <MinutesCard
        meeting={meeting}
        state={minutes}
        onChanged={setMinutes}
        onRetry={() => void loadMinutes()}
      />
    </div>
  )
}

function AiLabel() {
  return (
    <span className="ai-label">
      <Icon name="sparkles" size={14} />
      Nội dung do AI tạo — cần rà soát
    </span>
  )
}

function SummaryCard({
  meeting,
  state,
  onChanged,
  onRetry,
}: {
  meeting: Meeting
  state: Load<SummaryState>
  onChanged: (state: Load<SummaryState>) => void
  onRetry: () => void
}) {
  const toast = useToast()
  const [busy, setBusy] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [editing, setEditing] = useState<Record<(typeof SECTIONS)[number]['key'], string> | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})

  if (state.kind === 'loading') return <section className="card minutes-card" aria-busy="true"><div className="skeleton minutes-skeleton" /></section>
  if (state.kind === 'error')
    return (
      <section className="card minutes-card">
        <div className="alert alert-error" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={onRetry}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      </section>
    )

  const { summary, canManage, aiReady, readiness } = state.data
  const draft = summary?.draft ?? null
  const running = summary?.status === 'Queued' || summary?.status === 'Running'
  const canAsk = canManage && (meeting.status === 'InProgress' || meeting.status === 'Ended')

  const ask = async () => {
    setBusy(true)
    try {
      const result = await requestSummary(meeting.id)
      onChanged({ kind: 'ready', data: result })
      toast.show(result.message, 'success')
    } catch (error) {
      toast.show(errorMessage(error), 'error')
      if (isApiError(error, 409, 'MSG387')) onRetry()
      throw error
    } finally {
      setBusy(false)
    }
  }

  const save = async () => {
    if (!editing) return
    setBusy(true)
    setErrors({})
    try {
      const result = await editSummary(meeting.id, editing)
      onChanged({ kind: 'ready', data: result })
      setEditing(null)
      toast.show(result.message, 'success')
    } catch (error) {
      if (isApiError(error, 400, 'MSG389')) setErrors(error.body?.errors ?? {})
      toast.show(errorMessage(error), 'error')
    } finally {
      setBusy(false)
    }
  }

  const review = async () => {
    setBusy(true)
    try {
      const result = await reviewSummary(meeting.id)
      onChanged({ kind: 'ready', data: result })
      toast.show(result.message, 'success')
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card minutes-card" aria-label="Dự thảo nội dung">
      <header className="minutes-card-head">
        <h2>Dự thảo nội dung</h2>
        {summary && <SummaryStatusChip state={state.data} />}
        {canManage && !editing && (
          <div className="row-actions minutes-card-actions">
            {draft && !running && !summary?.reviewedAt && (
              <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => void review()}>
                <Icon name="checkCircle" size={16} />
                Duyệt
              </button>
            )}
            {draft && !running && (
              <button
                type="button"
                className="btn btn-outline btn-sm"
                disabled={busy}
                onClick={() => setEditing({ objectives: draft.objectives, discussion: draft.discussion, conclusions: draft.conclusions })}
              >
                <Icon name="pencil" size={16} />
                Sửa
              </button>
            )}
            {canAsk && !running && (
              <button
                type="button"
                className={draft ? 'btn btn-outline btn-sm' : 'btn btn-primary btn-sm'}
                disabled={busy || !aiReady}
                data-tooltip={aiReady ? undefined : 'Máy AI chưa sẵn sàng'}
                onClick={() => (draft ? setConfirming(true) : void ask().catch(() => undefined))}
              >
                <Icon name={draft ? 'refresh' : 'sparkles'} size={16} />
                {draft ? 'Tạo lại' : 'Tạo dự thảo'}
              </button>
            )}
          </div>
        )}
      </header>

      {canManage && readiness && !draft && !running && <ReadinessNote readiness={readiness} />}

      {summary?.status === 'Failed' && (
        <div className="alert alert-error" role="alert">
          <p>{summary.errorMessage || 'AI không viết được dự thảo.'}</p>
        </div>
      )}
      {running && (
        <p className="minutes-running" role="status">
          <span className="transcript-spinner" aria-hidden="true" />
          {summary?.status === 'Queued' ? 'Đã gửi yêu cầu. Dự thảo sẽ tự hiện ở đây khi AI viết xong.' : 'AI đang viết dự thảo, thường mất vài phút.'}
        </p>
      )}

      {!draft && !running && summary?.status !== 'Failed' && (
        <p className="field-hint minutes-empty">
          {canManage
            ? canAsk
              ? 'Chưa có dự thảo. Bấm “Tạo dự thảo” để AI tóm tắt chương trình, biểu quyết và lời thoại.'
              : 'Tạo dự thảo khi cuộc họp đã bắt đầu.'
            : 'Chưa có dự thảo đã duyệt.'}
        </p>
      )}

      {draft && !editing && (
        <>
          <AiLabel />
          <div className="minutes-outline">
            {SECTIONS.map((section) => (
              <section key={section.key}>
                <h3>{section.heading}</h3>
                <OutlineText text={draft[section.key]} />
              </section>
            ))}
          </div>
          {canManage && draft.sources.some((s) => s.from) && (
            <p className="minutes-sources">
              <span className="field-hint">Lấy từ bản ghi:</span>
              {draft.sources
                .filter((s) => s.from && s.to)
                .map((s) => (
                  <span key={`${s.agendaItemId ?? s.title}`} className="minutes-source" data-tooltip={`${s.lines} câu nói`}>
                    {s.title} · {formatClock(s.from!)}–{formatClock(s.to!)}
                  </span>
                ))}
            </p>
          )}
          <p className="field-hint minutes-meta">
            {summary?.generatedAt && `AI viết lúc ${formatDateTime(summary.generatedAt)}`}
            {summary?.model && ` · ${summary.model}`}
            {summary?.editedBy && summary.editedAt && ` · ${summary.editedBy.fullName} sửa lúc ${formatDateTime(summary.editedAt)}`}
          </p>
        </>
      )}

      {editing && (
        <form
          className="minutes-outline-form"
          onSubmit={(event) => {
            event.preventDefault()
            void save()
          }}
        >
          <AiLabel />
          {SECTIONS.map((section) => (
            <label key={section.key} className="field">
              <span className="field-label">{section.heading}</span>
              <textarea
                rows={section.rows}
                value={editing[section.key]}
                aria-invalid={errors[section.key] ? true : undefined}
                onChange={(event) => setEditing({ ...editing, [section.key]: event.target.value })}
              />
              {errors[section.key] && <span className="field-error">{errors[section.key]}</span>}
            </label>
          ))}
          <div className="row-actions minutes-form-actions">
            <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setEditing(null)}>
              Huỷ
            </button>
            <button type="submit" className="btn btn-primary btn-sm" disabled={busy}>
              {busy ? 'Đang lưu…' : 'Lưu'}
            </button>
          </div>
        </form>
      )}

      {confirming && (
        <ConfirmDialog
          title="Tạo lại dự thảo?"
          confirmLabel="Tạo lại"
          pendingLabel="Đang gửi…"
          onConfirm={ask}
          onClose={() => setConfirming(false)}
        >
          <p>AI viết lại từ đầu. Khi xong, dự thảo mới thay dự thảo hiện tại, kể cả phần đã sửa và đã duyệt.</p>
        </ConfirmDialog>
      )}
    </section>
  )
}

/** A section's text; its numbered sub-headings ("1. <nội dung>", "1. Kết luận của chủ trì") stand out in bold. */
function OutlineText({ text }: { text: string }) {
  return (
    <div className="minutes-outline-text">
      {text.split('\n').map((line, i) =>
        /^\d{1,2}\.\s+\S.{0,150}$/.test(line.trim()) ? (
          <p key={i} className="minutes-outline-heading">{line}</p>
        ) : (
          <p key={i}>{line || '\u00a0'}</p>
        ),
      )}
    </div>
  )
}

function SummaryStatusChip({ state }: { state: SummaryState }) {
  const summary = state.summary!
  if (summary.status === 'Queued') return <Badge tone="slate">Đang chờ</Badge>
  if (summary.status === 'Running') return <Badge tone="blue">AI đang viết</Badge>
  if (summary.status === 'Failed') return <Badge tone="red">Lỗi</Badge>
  if (summary.reviewedAt)
    return (
      <span data-tooltip={summary.reviewedBy ? `${summary.reviewedBy.fullName} duyệt lúc ${formatDateTime(summary.reviewedAt)}` : undefined}>
        <Badge tone="emerald">Đã duyệt</Badge>
      </span>
    )
  return <Badge tone="amber">Chưa duyệt</Badge>
}

/** AI-eCabinet's ReadinessNote: what the draft would miss if asked for now. */
function ReadinessNote({ readiness }: { readiness: SummaryReadiness }) {
  const gaps: string[] = []
  if (readiness.sentences === 0) gaps.push('Chưa có lời thoại nào: dự thảo chỉ dựa vào chương trình và biểu quyết.')
  if (readiness.unnamedVoices > 0) gaps.push(`${readiness.unnamedVoices} giọng chưa gán tên sẽ hiện là “Người nói N”.`)
  if (readiness.transcribing > 0) gaps.push(`${readiness.transcribing} bản ghi đang chuyển chữ, chưa được tính.`)
  if (readiness.failedTranscripts > 0) gaps.push(`${readiness.failedTranscripts} bản ghi chuyển chữ lỗi, không được tính.`)
  if (readiness.agendaItems > 0 && readiness.agendaItemsTimed < readiness.agendaItems)
    gaps.push('Có nội dung chưa được bắt đầu: lời thoại không chia được theo từng nội dung.')
  if (gaps.length === 0) return null
  return (
    <div className="alert alert-warning minutes-readiness">
      {gaps.map((gap) => (
        <p key={gap}>{gap}</p>
      ))}
    </div>
  )
}

function MinutesCard({
  meeting,
  state,
  onChanged,
  onRetry,
}: {
  meeting: Meeting
  state: Load<MinutesState>
  onChanged: (state: Load<MinutesState>) => void
  onRetry: () => void
}) {
  const toast = useToast()
  const [busy, setBusy] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [draft, setDraft] = useState<string | null>(null)

  if (state.kind === 'loading') return <section className="card minutes-card" aria-busy="true"><div className="skeleton minutes-skeleton" /></section>
  if (state.kind === 'error')
    return (
      <section className="card minutes-card">
        <div className="alert alert-error" role="alert">
          <p>{state.message}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={onRetry}>
            <Icon name="refresh" size={16} />
            Thử lại
          </button>
        </div>
      </section>
    )

  const { minutes, canManage, summaryReviewed } = state.data
  const canBuild = canManage && (meeting.status === 'InProgress' || meeting.status === 'Ended') && (!minutes || minutes.status === 'Draft')

  const build = async (rebuild: boolean) => {
    setBusy(true)
    try {
      const result = await createMinutes(meeting.id, rebuild)
      onChanged({ kind: 'ready', data: result })
      setDraft(null)
      toast.show(result.message, 'success')
    } catch (error) {
      toast.show(errorMessage(error), 'error')
      if (isApiError(error, 409, 'MSG399')) onRetry()
      throw error
    } finally {
      setBusy(false)
    }
  }

  const save = async () => {
    if (!minutes || draft === null) return
    setBusy(true)
    try {
      const result = await saveMinutes(minutes.id, draft, minutes.updatedAt)
      onChanged({ kind: 'ready', data: result })
      setDraft(null)
      toast.show(result.message, 'success')
    } catch (error) {
      toast.show(errorMessage(error), 'error')
      // Someone saved in between: keep the text on screen, reload the card so the next save goes over the newer one.
      if (isApiError(error, 409, 'MSG401')) onRetry()
    } finally {
      setBusy(false)
    }
  }

  const download = async () => {
    if (!minutes) return
    setBusy(true)
    try {
      saveBlob(await exportMinutes(minutes.id), `Biên bản - ${meeting.title}.docx`)
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <section className="card minutes-card" aria-label="Biên bản">
      <header className="minutes-card-head">
        <h2>Biên bản</h2>
        {minutes && <Badge tone={minutes.status === 'Issued' ? 'emerald' : 'slate'}>{minutes.status === 'Issued' ? 'Đã ban hành' : 'Bản nháp'}</Badge>}
        {canManage && draft === null && (
          <div className="row-actions minutes-card-actions">
            {minutes && meeting.sensitivity !== 'Secret' && (
              <button type="button" className="btn btn-outline btn-sm" disabled={busy} onClick={() => void download()}>
                <Icon name="download" size={16} />
                Tải Word
              </button>
            )}
            {minutes && canBuild && (
              <button type="button" className="btn btn-outline btn-sm" disabled={busy} onClick={() => setConfirming(true)}>
                <Icon name="refresh" size={16} />
                Tạo lại
              </button>
            )}
            {minutes && canBuild && (
              <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => setDraft(minutes.content)}>
                <Icon name="pencil" size={16} />
                Sửa
              </button>
            )}
            {!minutes && canBuild && (
              <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => void build(false).catch(() => undefined)}>
                <Icon name="fileText" size={16} />
                Tạo bản nháp
              </button>
            )}
          </div>
        )}
      </header>

      {!minutes && (
        <p className="field-hint minutes-empty">
          {canBuild
            ? summaryReviewed
              ? 'Bản nháp điền sẵn thông tin cuộc họp và dự thảo đã duyệt vào mẫu biên bản.'
              : 'Dự thảo chưa được duyệt: bản nháp chỉ điền thông tin cuộc họp, mục I–III để trống (……) cho thư ký viết.'
            : 'Chưa có biên bản.'}
        </p>
      )}

      {minutes && draft === null && <MinutesPaper content={minutes.content} />}

      {minutes && draft !== null && (
        <form
          className="minutes-editor"
          onSubmit={(event) => {
            event.preventDefault()
            void save()
          }}
        >
          <label className="field">
            <span className="field-label">
              Nội dung biên bản
              <InfoTip text="Dấu đầu dòng: ## tiêu đề mục, ### tiêu đề nhỏ, - gạch đầu dòng, **chữ đậm**, __chữ nghiêng__, @@ trái | phải là hai cột (phần đầu và chữ ký). “……” là chỗ cần điền." />
            </span>
            <textarea rows={28} value={draft} onChange={(event) => setDraft(event.target.value)} />
          </label>
          <div className="minutes-editor-preview" aria-label="Xem trước">
            <MinutesPaper content={draft} />
          </div>
          <div className="row-actions minutes-form-actions">
            <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setDraft(null)}>
              Huỷ
            </button>
            <button type="submit" className="btn btn-primary btn-sm" disabled={busy || !draft.trim()}>
              {busy ? 'Đang lưu…' : 'Lưu'}
            </button>
          </div>
        </form>
      )}

      {minutes && (
        <p className="field-hint minutes-meta">Lưu lần cuối {formatDateTime(minutes.updatedAt)}</p>
      )}

      {confirming && (
        <ConfirmDialog
          title="Tạo lại bản nháp biên bản?"
          confirmLabel="Tạo lại"
          pendingLabel="Đang tạo…"
          tone="danger"
          onConfirm={() => build(true)}
          onClose={() => setConfirming(false)}
        >
          <p>Bản nháp mới điền lại từ mẫu và dự thảo đã duyệt. Mọi chỗ đã sửa trong bản nháp hiện tại sẽ mất.</p>
        </ConfirmDialog>
      )}
    </section>
  )
}
