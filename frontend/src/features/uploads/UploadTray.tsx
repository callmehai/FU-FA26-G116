import { useEffect, useRef, useState } from 'react'
import { Link, useMatch } from 'react-router-dom'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { FileTypeTag } from '../documents/DocumentTags'
import { useUploadJobs, useUploads } from './uploadsContext'
import { isPending, isUnsaved, type UploadJob } from './uploadStore'

/**
 * The corner box that follows the session's uploads from any page: each file, where it goes, how far it
 * is and what can still be done with it. It never blocks the screen; toasts sit above it.
 */
export function UploadTray() {
  const store = useUploads()
  const jobs = useUploadJobs()
  const [collapsed, setCollapsed] = useState(false)
  const box = useRef<HTMLElement>(null)

  // Toasts stack above the tray: they read its height from here.
  useEffect(() => {
    const el = box.current
    if (!el) return
    const root = document.documentElement
    const observer = new ResizeObserver(() => root.style.setProperty('--upload-tray-h', `${el.offsetHeight + 8}px`))
    observer.observe(el)
    return () => {
      observer.disconnect()
      root.style.removeProperty('--upload-tray-h')
    }
  }, [jobs.length > 0])

  if (jobs.length === 0) return null

  const pending = jobs.filter(isPending).length
  const done = jobs.filter((j) => j.status === 'done').length
  const failed = jobs.filter((j) => j.status === 'failed').length
  const total = jobs.filter((j) => j.status !== 'canceled').length
  const title = pending
    ? `Đang tải lên ${done}/${total} tệp`
    : failed
      ? `${failed} tệp chưa tải lên được`
      : done
        ? `Đã tải lên ${done} tệp`
        : `Đã huỷ ${jobs.length} tệp`

  return (
    <section ref={box} className="upload-tray" aria-label="Tải lên">
      <header className="upload-tray-head">
        {pending ? (
          <span className="upload-spinner" aria-hidden="true" />
        ) : (
          <span className={failed ? 'tray-bad' : done ? 'tray-ok' : 'tray-off'}>
            <Icon name={failed ? 'alertCircle' : done ? 'checkCircle' : 'xCircle'} size={18} />
          </span>
        )}
        <h2 aria-live="polite">{title}</h2>
        <button
          type="button"
          className="icon-button icon-button-sm"
          aria-label={collapsed ? 'Mở danh sách tải lên' : 'Thu gọn danh sách tải lên'}
          aria-expanded={!collapsed}
          data-tooltip={collapsed ? 'Mở rộng' : 'Thu gọn'}
          onClick={() => setCollapsed((v) => !v)}
        >
          <Icon name={collapsed ? 'chevronUp' : 'chevronDown'} size={18} />
        </button>
        {pending === 0 && (
          <button
            type="button"
            className="icon-button icon-button-sm"
            aria-label="Đóng danh sách tải lên"
            data-tooltip="Đóng"
            onClick={() => {
              setCollapsed(false)
              store.clearFinished()
            }}
          >
            <Icon name="x" size={18} />
          </button>
        )}
      </header>
      {!collapsed && (
        <ul className="upload-tray-list" aria-label="Tệp đang tải lên">
          {jobs.map((job) => (
            <TrayRow key={job.id} job={job} />
          ))}
        </ul>
      )}
    </section>
  )
}

function stateText(job: UploadJob) {
  switch (job.status) {
    case 'queued':
      return job.retryAt ? 'Chưa tải được, sẽ tự thử lại…' : 'Đang chờ'
    case 'sending':
      return `${Math.round(job.progress * 100)}%`
    case 'saving':
      return 'Đang lưu…'
    case 'done':
      return 'Đã tải lên'
    case 'canceled':
      return 'Đã huỷ'
    case 'failed':
      return job.error ?? 'Không tải lên được.'
  }
}

function TrayRow({ job }: { job: UploadJob }) {
  const store = useUploads()
  const [dropping, setDropping] = useState(false)
  // In the call room a link opens a new tab: leaving the page would drop the call.
  const inRoom = useMatch('/mgmt/meetings/:id/room') !== null
  const moving = job.status === 'sending' || job.status === 'saving'
  const over = job.status === 'failed' || job.status === 'canceled'

  return (
    <li className={`upload-tray-row is-${job.status}`}>
      {job.kind === 'audio' ? (
        <span className="upload-tray-audio">
          <Icon name="mic" size={18} />
        </span>
      ) : (
        <FileTypeTag pdf={job.kind === 'pdf'} size={28} />
      )}
      <span className="upload-tray-main">
        <strong data-tooltip={job.name}>{job.name}</strong>
        <small>
          {job.href ? (
            <Link to={job.href} {...(inRoom && { target: '_blank', rel: 'noopener' })}>
              {job.where}
            </Link>
          ) : (
            <span>{job.where}</span>
          )}
          {job.status !== 'failed' && (
            <span className="upload-tray-state" data-tooltip={job.retryAt ? job.error : undefined}>
              {stateText(job)}
            </span>
          )}
        </small>
        {/* A reason is a sentence: it gets its own line rather than the end of a short one. */}
        {job.status === 'failed' && <span className="upload-tray-error">{stateText(job)}</span>}
        {moving && (
          <span
            className={`upload-progress${job.status === 'saving' ? ' is-saving' : ''}`}
            role="progressbar"
            aria-label={`Tiến độ tải ${job.name}`}
            aria-valuemin={0}
            aria-valuemax={100}
            aria-valuenow={Math.round(job.progress * 100)}
          >
            <i style={{ width: `${Math.max(job.progress * 100, 2)}%` }} />
          </span>
        )}
      </span>
      {job.status === 'done' && (
        <span className="tray-ok upload-tray-done">
          <Icon name="checkCircle" size={18} />
        </span>
      )}
      {over && (job.retryable || job.status === 'canceled') && (
        <button
          type="button"
          className="icon-button icon-button-sm"
          aria-label={`Thử lại ${job.name}`}
          data-tooltip="Thử lại"
          onClick={() => store.retry(job.id)}
        >
          <Icon name="refresh" size={18} />
        </button>
      )}
      {over && job.canKeep && (
        <button
          type="button"
          className="icon-button icon-button-sm"
          aria-label={`Lưu ${job.name} về máy`}
          data-tooltip="Lưu về máy"
          onClick={() => store.keep(job.id)}
        >
          <Icon name="download" size={18} />
        </button>
      )}
      {job.status !== 'done' && (
        <button
          type="button"
          className="icon-button icon-button-sm"
          aria-label={isPending(job) ? `Huỷ tải ${job.name}` : `Bỏ ${job.name} khỏi danh sách`}
          data-tooltip={isPending(job) ? 'Huỷ tải' : 'Bỏ khỏi danh sách'}
          onClick={() =>
            isPending(job) ? store.cancel(job.id) : isUnsaved(job) ? setDropping(true) : store.remove(job.id)
          }
        >
          <Icon name="x" size={18} />
        </button>
      )}
      {dropping && (
        <ConfirmDialog
          title="Bỏ bản ghi chưa lưu?"
          confirmLabel="Bỏ bản ghi"
          pendingLabel="Đang bỏ…"
          cancelLabel="Giữ lại"
          tone="danger"
          onConfirm={async () => store.remove(job.id)}
          onClose={() => setDropping(false)}
        >
          “{job.name}” chỉ còn trên trình duyệt này. Bỏ là mất hẳn, trừ khi bạn đã lưu về máy.
        </ConfirmDialog>
      )}
    </li>
  )
}
