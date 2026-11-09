import { lazy, Suspense, useCallback, useEffect, useRef, useState } from 'react'
import {
  approveMinutes,
  correctMinutes,
  createMinutes,
  editSummary,
  exportMinutes,
  getMinutes,
  getMinutesRevision,
  getSummary,
  requestSummary,
  reviewSummary,
  saveMinutes,
  submitMinutes,
} from '../../api/summary'
import { Badge, type BadgeTone } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { MenuButton } from '../../components/MenuButton'
import { Select } from '../../components/Select'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDateTime } from '../../lib/format'
import type { Meeting } from '../../types/meeting'
import type { MeetingMinutes, MinutesState, SummaryReadiness, SummaryState } from '../../types/summary'
import { FileTypeTag } from '../documents/DocumentTags'
import { saveBlob } from '../documents/documentFile'
import { MinutesPaper } from './MinutesPaper'
import { NameVoicesSheet } from './NameVoicesSheet'
import { OutlineSections, type OutlineValues } from './OutlineSections'
import { ReturnMinutesDialog } from './ReturnMinutesDialog'

// The editor brings TipTap with it: it loads when someone opens the minutes to edit, not with every meeting page.
const MinutesEditor = lazy(() => import('./MinutesEditor').then((m) => ({ default: m.MinutesEditor })))

type Load<T> = { kind: 'loading' } | { kind: 'error'; message: string } | { kind: 'ready'; data: T }

/**
 * The "Biên bản" tab (SCRUM-36, SCRUM-32), laid out as AI-eCabinet's MinutesPanel: the AI's Dự thảo first — asked
 * for, read, corrected and reviewed by the Chair or the Secretary — then the minutes built from the one fixed template
 * (Decree 30's Mẫu 1.4) with the Dự thảo as it stands in sections I–III, as eCab composes them. Writing a draft takes
 * minutes on a laptop, so the card polls while it waits.
 */
export function MinutesPanel({ meeting }: { meeting: Meeting }) {
  const [summary, setSummary] = useState<Load<SummaryState>>({ kind: 'loading' })
  const [minutes, setMinutes] = useState<Load<MinutesState>>({ kind: 'loading' })
  const [barHost, setBarHost] = useState<HTMLDivElement | null>(null)
  // A revision opened from the history (UC-58); null is the one the server picks: the latest for the Chair and the
  // Secretary, the latest issued for everyone else.
  const [selected, setSelected] = useState<string | null>(null)

  const loadSummary = useCallback(async () => {
    try {
      setSummary({ kind: 'ready', data: await getSummary(meeting.id) })
    } catch (error) {
      setSummary({ kind: 'error', message: errorMessage(error) })
    }
  }, [meeting.id])
  const loadMinutes = useCallback(async () => {
    try {
      setMinutes({ kind: 'ready', data: selected ? await getMinutesRevision(selected) : await getMinutes(meeting.id) })
    } catch (error) {
      // The revision opened is no longer one this reader may see: go back to the one the server picks.
      if (selected && isApiError(error, 404)) return setSelected(null)
      setMinutes({ kind: 'error', message: errorMessage(error) })
    }
  }, [meeting.id, selected])

  useEffect(() => {
    void loadSummary()
  }, [loadSummary])
  useEffect(() => {
    void loadMinutes()
  }, [loadMinutes])

  // The minutes card says when the draft is newer than the minutes: ask again whenever the draft is written or saved.
  const draftStamp =
    summary.kind === 'ready' ? `${summary.data.summary?.generatedAt ?? ''}|${summary.data.summary?.editedAt ?? ''}` : null
  const seenStamp = useRef<string | null>(null)
  useEffect(() => {
    if (draftStamp === null) return
    if (seenStamp.current !== null && seenStamp.current !== draftStamp) void loadMinutes()
    seenStamp.current = draftStamp
  }, [draftStamp, loadMinutes])

  // Poll while the AI writes, as eCab's pipeline chip does (every 5 s until the job is done).
  const waiting = summary.kind === 'ready' && ['Queued', 'Running'].includes(summary.data.summary?.status ?? '')
  useEffect(() => {
    if (!waiting) return
    const timer = window.setInterval(() => void loadSummary(), 5000)
    return () => window.clearInterval(timer)
  }, [waiting, loadSummary])

  return (
    <div className="minutes-tab">
      <SummaryCard
        meeting={meeting}
        state={summary}
        barHost={barHost}
        onChanged={setSummary}
        onRetry={() => void loadSummary()}
      />
      <MinutesCard
        meeting={meeting}
        state={minutes}
        source={draftSource(summary)}
        onChanged={(next) => {
          // Every change answers with the state the server picks, so the card leaves an opened revision with it.
          setSelected(null)
          setMinutes(next)
        }}
        onSelect={setSelected}
        onRetry={() => void loadMinutes()}
      />
      <div ref={setBarHost} className="outline-savebar-host" />
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
  barHost,
  onChanged,
  onRetry,
}: {
  meeting: Meeting
  state: Load<SummaryState>
  barHost: HTMLElement | null
  onChanged: (state: Load<SummaryState>) => void
  onRetry: () => void
}) {
  const toast = useToast()
  const [busy, setBusy] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [dirty, setDirty] = useState(false)
  const [naming, setNaming] = useState(false)

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
  // The Dự thảo is a snapshot of the transcript: a line corrected, left out or named since leaves it behind (eCab).
  const changedAt = readiness?.transcriptChangedAt
  const stale =
    !!draft && !running && !!changedAt && !!summary?.generatedAt && Date.parse(changedAt) > Date.parse(summary.generatedAt)
  const askBlocked = !aiReady ? 'Máy AI chưa sẵn sàng' : dirty ? 'Lưu hoặc hoàn tác thay đổi trước' : undefined

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

  const save = async (values: OutlineValues) => {
    try {
      const result = await editSummary(meeting.id, values)
      onChanged({ kind: 'ready', data: result })
      toast.show(result.message, 'success')
      const stored = result.summary?.draft
      return { saved: true, errors: {}, values: stored ? { objectives: stored.objectives, discussion: stored.discussion, conclusions: stored.conclusions } : undefined }
    } catch (error) {
      toast.show(errorMessage(error), 'error')
      // The AI started writing it again meanwhile: read the state, which closes the editor and shows it writing.
      if (isApiError(error, 409, 'MSG387')) onRetry()
      return { saved: false, errors: isApiError(error, 400, 'MSG389') ? (error.body?.errors ?? {}) : {} }
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
      if (isApiError(error, 409, 'MSG387')) onRetry()
    } finally {
      setBusy(false)
    }
  }

  return (
    <section id="summary-card" className="card minutes-card" aria-label="Dự thảo nội dung">
      <header className="minutes-card-head">
        <h2>Dự thảo nội dung</h2>
        {summary && <SummaryStatusChip state={state.data} />}
        {canManage && (
          <div className="row-actions minutes-card-actions">
            {draft && !running && !summary?.reviewedAt && (
              <button
                id="summary-review"
                type="button"
                className="btn btn-primary btn-sm"
                disabled={busy || dirty}
                data-tooltip={dirty ? 'Lưu thay đổi trước khi duyệt' : undefined}
                onClick={() => void review()}
              >
                <Icon name="checkCircle" size={16} />
                Duyệt
              </button>
            )}
            {canAsk && !running && (
              <button
                id="summary-ask"
                type="button"
                className={draft ? 'btn btn-outline btn-sm' : 'btn btn-primary btn-sm'}
                disabled={busy || !!askBlocked}
                data-tooltip={askBlocked}
                onClick={() => (draft ? setConfirming(true) : void ask().catch(() => undefined))}
              >
                <Icon name={draft ? 'refresh' : 'sparkles'} size={16} />
                {draft ? 'Tạo lại' : 'Tạo dự thảo'}
              </button>
            )}
          </div>
        )}
      </header>

      {canAsk && stale && (
        <div className="alert minutes-stale" role="status">
          <p>
            <Icon name="history" size={16} />
            Bản ghi đã được sửa sau khi tạo dự thảo: dự thảo chưa có các chỗ sửa đó.
          </p>
          <button
            type="button"
            className="btn btn-sm"
            disabled={busy || !!askBlocked}
            data-tooltip={askBlocked}
            onClick={() => setConfirming(true)}
          >
            Tạo lại
          </button>
        </div>
      )}

      {canManage && readiness && !running && (
        <ReadinessNote readiness={readiness} drafted={!!draft} onNameVoices={() => setNaming(true)} />
      )}

      {summary?.status === 'Failed' && (
        <div className="alert minutes-failed" role="alert">
          <Icon name="alertCircle" size={16} />
          <p>
            {summary.errorMessage || 'AI không viết được dự thảo.'}
            {draft && <span className="minutes-failed-kept"> Đang giữ bản trước.</span>}
          </p>
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

      {draft && (
        <>
          <AiLabel />
          <OutlineSections
            // A new draft is a different text: nobody keeps editing one that is gone.
            key={summary?.generatedAt ?? ''}
            draft={draft}
            editable={canManage && !running}
            reviewed={!!summary?.reviewedAt}
            showSources={canManage}
            barHost={barHost}
            onSave={save}
            onDirtyChange={setDirty}
          />
          <p className="field-hint minutes-meta">
            {summary?.generatedAt && `AI viết lúc ${formatDateTime(summary.generatedAt)}`}
            {summary?.model && ` (${summary.model})`}
            {summary?.editedBy && summary.editedAt && `, ${summary.editedBy.fullName} sửa lúc ${formatDateTime(summary.editedAt)}`}
          </p>
        </>
      )}

      {confirming && (
        <ConfirmDialog
          title="Tạo lại dự thảo?"
          confirmLabel="Tạo lại"
          pendingLabel="Đang gửi…"
          onConfirm={ask}
          onClose={() => setConfirming(false)}
        >
          AI viết lại từ đầu. Khi xong, dự thảo mới thay dự thảo hiện tại, kể cả phần đã sửa và đã duyệt.
        </ConfirmDialog>
      )}

      {naming && (
        <NameVoicesSheet
          meetingId={meeting.id}
          drafted={!!draft}
          canGenerate={!canAsk ? 'Tạo dự thảo khi cuộc họp đã bắt đầu.' : running ? 'AI đang viết dự thảo' : askBlocked}
          onClose={() => {
            setNaming(false)
            // Names given here change the readiness and may leave the draft behind the transcript.
            onRetry()
          }}
          onGenerate={() => {
            setNaming(false)
            // A first draft: the POST answers with the whole state, so no refresh races it and overwrites Queued. Only
            // when it fails is the state read again, for the names just given.
            if (!draft) return void ask().catch(() => onRetry())
            onRetry()
            setConfirming(true)
          }}
        />
      )}
    </section>
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
/**
 * What a draft asked for now would miss. Before the first draft every gap is listed; once there is one, only what the
 * transcript can still change (voices to name, transcripts pending or failed) — the rest is already in the text.
 */
function ReadinessNote({
  readiness,
  drafted,
  onNameVoices,
}: {
  readiness: SummaryReadiness
  drafted: boolean
  onNameVoices?: () => void
}) {
  const gaps: string[] = []
  if (!drafted && readiness.sentences === 0)
    gaps.push('Chưa có lời thoại nào: dự thảo chỉ dựa vào chương trình và biểu quyết.')
  if (readiness.transcribing > 0) gaps.push(`${readiness.transcribing} bản ghi đang chuyển chữ, chưa được tính.`)
  if (readiness.failedTranscripts > 0) gaps.push(`${readiness.failedTranscripts} bản ghi chuyển chữ lỗi, không được tính.`)
  if (!drafted && readiness.agendaItems > 0 && readiness.agendaItemsTimed < readiness.agendaItems)
    gaps.push('Có nội dung chưa được bắt đầu: lời thoại không chia được theo từng nội dung.')
  const unnamed = readiness.unnamedVoices > 0
  if (gaps.length === 0 && !unnamed) return null
  return (
    <div className="minutes-readiness" role="status">
      {unnamed && (
        <p className="minutes-readiness-line">
          <Icon name="warning" size={16} />
          <span>{readiness.unnamedVoices} giọng chưa gán tên sẽ hiện là “Người nói N” trong dự thảo.</span>
          {onNameVoices && (
            <button type="button" className="btn btn-outline btn-sm" onClick={onNameVoices}>
              Gán tên người nói
            </button>
          )}
        </p>
      )}
      {gaps.map((gap) => (
        <p key={gap} className="minutes-readiness-line">
          <Icon name="warning" size={16} />
          <span>{gap}</span>
        </p>
      ))}
    </div>
  )
}

/** Where sections I–III of the minutes come from: the Dự thảo card above, as it stands. Null while it loads. */
type DraftSource = 'none' | 'running' | 'unreviewed' | 'reviewed' | null

function draftSource(summary: Load<SummaryState>): DraftSource {
  if (summary.kind !== 'ready') return null
  const s = summary.data.summary
  if (s?.status === 'Queued' || s?.status === 'Running') return 'running'
  if (!s?.draft) return 'none'
  return s.reviewedAt ? 'reviewed' : 'unreviewed'
}

/**
 * Takes the reader to a button of the Dự thảo card, pinging it a few seconds as the page's calls to act do: the draft
 * is read there before it is reviewed, so the minutes card does not review it blind.
 */
function goTo(id: string): string | null {
  const target = document.getElementById(id)
  if (!(target instanceof HTMLButtonElement) || target.disabled) {
    // Nothing to press there now (the AI is not ready, the draft is being edited): show the card and say why.
    document.getElementById('summary-card')?.scrollIntoView({ block: 'start', behavior: 'smooth' })
    return target?.dataset.tooltip ?? 'Xem thẻ Dự thảo nội dung ở trên.'
  }
  target.scrollIntoView({ block: 'center', behavior: 'smooth' })
  target.focus({ preventScroll: true })
  target.classList.add('btn-ping')
  window.setTimeout(() => target.classList.remove('btn-ping'), 4000)
  return null
}

/** How each state of the minutes reads on the card (SCRUM-33). */
const MINUTES_STATUS: Record<MeetingMinutes['status'], { label: string; tone: BadgeTone }> = {
  Draft: { label: 'Bản nháp', tone: 'slate' },
  WaitingApproval: { label: 'Chờ duyệt', tone: 'violet' },
  Rejected: { label: 'Bị trả lại', tone: 'amber' },
  Issued: { label: 'Đã ban hành', tone: 'emerald' },
  Superseded: { label: 'Đã thay thế', tone: 'slate' },
}

/** The first minutes are the original; each correction after them is numbered from 1. */
const revisionName = (revisionNumber: number) => (revisionNumber === 1 ? 'Bản gốc' : `Đính chính ${revisionNumber - 1}`)

/** Who moved the revision to the state it is in, and when; the save time while it is still being written. */
function minutesMeta(m: MeetingMinutes): string {
  if ((m.status === 'Issued' || m.status === 'Superseded') && m.issuedAt)
    return `${m.approvedBy ? `${m.approvedBy.fullName} duyệt, ban hành` : 'Ban hành'} lúc ${formatDateTime(m.issuedAt)}`
  if (m.status === 'WaitingApproval' && m.submittedAt)
    return `${m.submittedBy ? `${m.submittedBy.fullName} gửi duyệt` : 'Gửi duyệt'} lúc ${formatDateTime(m.submittedAt)}`
  return `Lưu lần cuối ${formatDateTime(m.updatedAt)}`
}

type MinutesDialog = 'rebuild' | 'submit' | 'approve' | 'return' | 'correct'

/**
 * The minutes (SCRUM-32) and their approval (SCRUM-33): the Secretary sends the draft to the Chair once the meeting has
 * ended, the Chair issues it or returns it with a reason, and issued text is corrected only by a new revision. Which of
 * the two officers the caller is comes from the server (`canSubmit`, `canApprove`); the states decide the rest.
 */
function MinutesCard({
  meeting,
  state,
  source,
  onChanged,
  onSelect,
  onRetry,
}: {
  meeting: Meeting
  state: Load<MinutesState>
  source: DraftSource
  onChanged: (state: Load<MinutesState>) => void
  onSelect: (minutesId: string | null) => void
  onRetry: () => void
}) {
  const toast = useToast()
  const [busy, setBusy] = useState(false)
  const [dialog, setDialog] = useState<MinutesDialog | null>(null)
  // The text being edited and the version it was opened on. The card reloads while it is open (the AI draft finishing,
  // the draft saved), so the save must send the version the editing started from, or it would quietly go over a save
  // made in between. null base: the save after a conflict, meant to go over the newer one.
  const [draft, setDraft] = useState<{ text: string; base: string | null; hasText: boolean } | null>(null)

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

  const { minutes, canManage, draftChangedAt, history, canSubmit, canApprove } = state.data
  // The revision on screen is the newest the caller may see; an older one opened from the history is only read.
  const latest = !minutes || history.length === 0 || history[0].id === minutes.id
  const meetingOpen = meeting.status === 'InProgress' || meeting.status === 'Ended'
  // A draft, or one the Chair returned, is written again (BR-407); a correction is too, but never rebuilt (BR-413).
  const editable = !!minutes && latest && canManage && meetingOpen && (minutes.status === 'Draft' || minutes.status === 'Rejected')
  const canBuild = canManage && meetingOpen && (!minutes || (editable && !minutes.supersedesId))
  const waiting = !!minutes && latest && minutes.status === 'WaitingApproval'
  const issued = !!minutes && latest && minutes.status === 'Issued'
  // Issued text, and what it replaced, downloads for every reader; a draft only for the two officers (BR-412).
  const canDownload = !!minutes && meeting.sensitivity !== 'Secret'
  // The draft has moved on since the minutes were built (or they were built before there was one).
  const stale =
    !!minutes && !!draftChangedAt && (!minutes.builtFromDraftAt || Date.parse(draftChangedAt) !== Date.parse(minutes.builtFromDraftAt))

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
      const result = await saveMinutes(minutes.id, draft.text, draft.base ?? minutes.updatedAt)
      onChanged({ kind: 'ready', data: result })
      setDraft(null)
      toast.show(result.message, 'success')
    } catch (error) {
      toast.show(errorMessage(error), 'error')
      // Someone saved in between: keep the text on screen, reload the card so the next save goes over the newer one.
      if (isApiError(error, 409, 'MSG401')) {
        setDraft((d) => d && { ...d, base: null })
        onRetry()
      }
    } finally {
      setBusy(false)
    }
  }

  /**
   * A step of the approval. Someone acting on the minutes meanwhile (409), or the minutes gone from the caller (404),
   * closes the dialog with the reason and shows the minutes as they are now; any other failure stays in the dialog.
   */
  const act = async (call: () => Promise<MinutesState & { message: string }>) => {
    try {
      const result = await call()
      onChanged({ kind: 'ready', data: result })
      setDraft(null)
      toast.show(result.message, 'success')
    } catch (error) {
      if (!isApiError(error, 409) && !isApiError(error, 404)) throw error
      toast.show(errorMessage(error), 'error')
      onRetry()
    }
  }

  const notify = (reason: string | null) => {
    if (reason) toast.show(reason, 'info')
  }

  const download = async (format: 'docx' | 'pdf') => {
    if (!minutes) return
    setBusy(true)
    try {
      saveBlob(await exportMinutes(minutes.id, format), `Biên bản - ${meeting.title}.${format}`)
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    } finally {
      setBusy(false)
    }
  }

  const status = minutes ? MINUTES_STATUS[minutes.status] : null

  return (
    <section className="card minutes-card" aria-label="Biên bản">
      <header className="minutes-card-head">
        <h2>Biên bản</h2>
        {status && <Badge tone={status.tone}>{status.label}</Badge>}
        {draft === null && (
          <div className="row-actions minutes-card-actions">
            {minutes && history.length > 1 && (
              <Select
                size="sm"
                label="Phiên bản biên bản"
                value={minutes.id}
                options={history.map((r) => ({ value: r.id, label: revisionName(r.revisionNumber), hint: MINUTES_STATUS[r.status].label }))}
                onChange={(id) => onSelect(id === history[0].id ? null : id)}
              />
            )}
            {canDownload && (
              <MenuButton
                label="Tải xuống"
                icon="download"
                disabled={busy}
                items={[
                  { key: 'docx', lead: <FileTypeTag pdf={false} size={22} />, label: 'Word', onSelect: () => void download('docx') },
                  { key: 'pdf', lead: <FileTypeTag pdf size={22} />, label: 'PDF', onSelect: () => void download('pdf') },
                ]}
              />
            )}
            {minutes && canBuild && (
              <button type="button" className="btn btn-outline btn-sm" disabled={busy} onClick={() => setDialog('rebuild')}>
                <Icon name="refresh" size={16} />
                Tạo lại
              </button>
            )}
            {minutes && editable && (
              <button
                type="button"
                className={canSubmit ? 'btn btn-outline btn-sm' : 'btn btn-primary btn-sm'}
                disabled={busy}
                onClick={() => setDraft({ text: minutes.content, base: minutes.updatedAt, hasText: true })}
              >
                <Icon name="pencil" size={16} />
                Sửa
              </button>
            )}
            {editable && canSubmit && (
              <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => setDialog('submit')}>
                <Icon name="send" size={16} />
                Gửi duyệt
              </button>
            )}
            {waiting && canApprove && (
              <button type="button" className="btn btn-outline btn-sm" disabled={busy} onClick={() => setDialog('return')}>
                <Icon name="reply" size={16} />
                Trả lại
              </button>
            )}
            {waiting && canApprove && (
              <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={() => setDialog('approve')}>
                <Icon name="checkCircle" size={16} />
                Duyệt và ban hành
              </button>
            )}
            {issued && canManage && (
              <button type="button" className="btn btn-outline btn-sm" disabled={busy} onClick={() => setDialog('correct')}>
                <Icon name="pencil" size={16} />
                Tạo bản đính chính
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
            ? 'Bản nháp điền sẵn thông tin cuộc họp và dự thảo vào mẫu biên bản.'
            : canManage
              ? 'Chưa có biên bản.'
              : 'Chưa có biên bản ban hành.'}
        </p>
      )}

      {minutes && !latest && (
        <div className="alert alert-info minutes-notice" role="status">
          <span>
            Đang xem {revisionName(minutes.revisionNumber).toLowerCase()}, {MINUTES_STATUS[minutes.status].label.toLowerCase()}.
          </span>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => onSelect(null)}>
            Xem bản mới nhất
          </button>
        </div>
      )}

      {minutes && latest && canManage && minutes.status === 'Rejected' && minutes.rejectionReason && (
        <div className="alert alert-warning minutes-notice minutes-returned" role="status">
          <p>
            <strong>{minutes.returnedBy ? `${minutes.returnedBy.fullName} trả lại` : 'Chủ toạ trả lại'}:</strong>{' '}
            {minutes.rejectionReason}
          </p>
        </div>
      )}

      {canBuild && draft === null && source === 'none' && (
        <div className="alert alert-info minutes-notice" role="status">
          <span>Chưa có dự thảo AI nên mục I–III để trống.</span>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => notify(goTo('summary-ask'))}>
            <Icon name="sparkles" size={16} />
            Tạo dự thảo
          </button>
        </div>
      )}
      {canBuild && draft === null && source === 'running' && (
        <div className="alert alert-info minutes-notice" role="status">
          <span>{minutes ? 'AI đang viết dự thảo. Xong thì bấm Tạo lại để điền mục I–III.' : 'AI đang viết dự thảo. Đợi xong rồi tạo biên bản để có mục I–III.'}</span>
        </div>
      )}
      {canBuild && draft === null && (source === 'unreviewed' || (source === 'reviewed' && stale)) && (
        <div className={source === 'unreviewed' ? 'alert alert-warning minutes-notice' : 'alert alert-info minutes-notice'} role="status">
          <span>
            {!minutes
              ? 'Dự thảo AI chưa duyệt: biên bản sẽ dùng nội dung chưa rà soát.'
              : !stale
                ? 'Biên bản đang dùng dự thảo AI chưa duyệt.'
                : source === 'unreviewed'
                  ? 'Biên bản chưa có dự thảo mới nhất, dự thảo cũng chưa duyệt.'
                  : 'Biên bản chưa có dự thảo mới nhất.'}
          </span>
          <span className="row-actions">
            {source === 'unreviewed' && (
              <button type="button" className="btn btn-outline btn-sm" onClick={() => notify(goTo('summary-review'))}>
                <Icon name="checkCircle" size={16} />
                Duyệt dự thảo
              </button>
            )}
            {minutes && stale && (
              <button type="button" className="btn btn-outline btn-sm" disabled={busy} onClick={() => setDialog('rebuild')}>
                <Icon name="refresh" size={16} />
                Tạo lại
              </button>
            )}
          </span>
        </div>
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
          <Suspense fallback={<div className="skeleton minutes-skeleton" />}>
            <MinutesEditor
              content={draft.text}
              onChange={(text, hasText) => setDraft((d) => d && { ...d, text, hasText })}
              actions={
                <>
                  <button type="button" className="btn btn-ghost btn-sm" disabled={busy} onClick={() => setDraft(null)}>
                    Huỷ
                  </button>
                  <button type="submit" className="btn btn-primary btn-sm" disabled={busy || !draft.hasText}>
                    {busy ? 'Đang lưu…' : 'Lưu'}
                  </button>
                </>
              }
            />
          </Suspense>
        </form>
      )}

      {minutes && <p className="field-hint minutes-meta">{minutesMeta(minutes)}</p>}

      {dialog === 'rebuild' && (
        <ConfirmDialog
          title="Tạo lại bản nháp biên bản?"
          confirmLabel="Tạo lại"
          pendingLabel="Đang tạo…"
          tone="danger"
          onConfirm={() => build(true)}
          onClose={() => setDialog(null)}
        >
          Bản nháp mới điền lại từ mẫu và dự thảo hiện tại. Mọi chỗ đã sửa trong bản nháp biên bản sẽ mất.
        </ConfirmDialog>
      )}

      {dialog === 'submit' && minutes && (
        <ConfirmDialog
          title="Gửi biên bản cho Chủ toạ duyệt?"
          confirmLabel="Gửi duyệt"
          pendingLabel="Đang gửi…"
          blocker={meeting.status !== 'Ended' ? 'Chỉ gửi duyệt biên bản sau khi cuộc họp kết thúc.' : undefined}
          onConfirm={() => act(() => submitMinutes(minutes.id, minutes.updatedAt))}
          onClose={() => setDialog(null)}
        >
          {/* UC-61 AF-1: the screen warns about the blanks the template left, the server does not block them. */}
          {minutes.content.includes('……') && <strong>Biên bản còn chỗ “……” chưa điền. </strong>}
          Sau khi gửi, biên bản khoá lại cho tới khi Chủ toạ duyệt hoặc trả lại.
        </ConfirmDialog>
      )}

      {dialog === 'approve' && minutes && (
        <ConfirmDialog
          title="Duyệt và ban hành biên bản?"
          confirmLabel="Duyệt và ban hành"
          pendingLabel="Đang ban hành…"
          onConfirm={() => act(() => approveMinutes(minutes.id, minutes.updatedAt))}
          onClose={() => setDialog(null)}
        >
          Biên bản được khoá và gửi tới mọi người dự họp. Muốn sửa sau đó phải tạo bản đính chính.
        </ConfirmDialog>
      )}

      {dialog === 'return' && minutes && (
        <ReturnMinutesDialog
          minutesId={minutes.id}
          updatedAt={minutes.updatedAt}
          onDone={(result) => {
            setDialog(null)
            onChanged({ kind: 'ready', data: result })
            toast.show(result.message, 'success')
          }}
          onConflict={(message) => {
            setDialog(null)
            toast.show(message, 'error')
            onRetry()
          }}
          onClose={() => setDialog(null)}
        />
      )}

      {dialog === 'correct' && minutes && (
        <ConfirmDialog
          title="Tạo bản đính chính?"
          confirmLabel="Tạo bản đính chính"
          pendingLabel="Đang tạo…"
          onConfirm={() => act(() => correctMinutes(minutes.id))}
          onClose={() => setDialog(null)}
        >
          Bản mới chép nội dung đang ban hành để sửa, rồi gửi duyệt như bản đầu. Mọi người vẫn đọc bản đang ban hành cho tới
          khi bản đính chính được duyệt.
        </ConfirmDialog>
      )}
    </section>
  )
}
