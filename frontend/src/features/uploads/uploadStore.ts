import { ApiError } from '../../api/client'
import { errorMessage, isApiError } from '../../lib/errors'

export type UploadStatus = 'queued' | 'sending' | 'saving' | 'done' | 'failed' | 'canceled'

export type SendOptions = { onProgress: (loaded: number, total: number) => void; signal: AbortSignal }

export interface UploadRequest {
  name: string
  kind: 'pdf' | 'docx' | 'audio'
  /** Where the file goes, as the person reads it: a meeting's title, a library folder. */
  where: string
  href?: string
  /** Which screens show the result: they re-read when a job of theirs ends (`useUploadsSettled`). */
  target: string
  send: (options: SendOptions) => Promise<{ message: string }>
  /** Answers the screen handles itself (the meeting ended, the folder went away): never "access lost". */
  isExpected?: (err: unknown) => boolean
  /** Saves the bytes on this computer, for a file that exists nowhere else (a recording just made). */
  keep?: () => void
}

export interface UploadJob {
  readonly id: string
  readonly name: string
  readonly kind: UploadRequest['kind']
  readonly where: string
  readonly href?: string
  readonly target: string
  readonly status: UploadStatus
  /** Share of the bytes sent, 0..1. */
  readonly progress: number
  readonly error?: string
  /** "Thử lại" can help: the failure was the network or the server, not the file or the meeting. */
  readonly retryable?: boolean
  /** Waiting to try again by itself, at this time (ms). */
  readonly retryAt?: number
  readonly attempt: number
  readonly canKeep: boolean
}

export const isPending = (j: UploadJob) => j.status === 'queued' || j.status === 'sending' || j.status === 'saving'

/** Not saved, and its bytes exist nowhere else: closing the tray or the tab would lose them. */
export const isUnsaved = (j: UploadJob) => j.canKeep && j.status !== 'done'

/** Tries per file before it waits for the person: the first, then two more after these pauses. */
const RETRY_DELAYS_MS = [2000, 8000]

/** The body never fully left: nothing can have been stored, so sending again is safe. */
const UNSENT = 'unsent'
/** The server answered with an error after rolling back: nothing was stored either. */
const REFUSED_SAFE = 'refused'
/** The connection dropped after the whole body was out: the server may have stored it. */
const UNKNOWN = 'unknown'

function outcome(err: unknown, allSent: boolean) {
  if (err instanceof ApiError) {
    // A gateway gave up waiting; the app behind it may still have committed.
    if (err.status === 502 || err.status === 504) return allSent ? UNKNOWN : UNSENT
    if (err.status >= 500 || err.status === 408 || err.status === 429) return REFUSED_SAFE
    return null
  }
  if (err instanceof TypeError) return allSent ? UNKNOWN : UNSENT
  return null
}

/** MSG164 names the file's problem under `files[0]`: one file per request. */
function fileError(err: unknown) {
  if (isApiError(err, 400, 'MSG164')) return err.body?.errors?.['files[0]'] ?? errorMessage(err)
  return errorMessage(err)
}

/**
 * The session's uploads, one file per request and one request at a time, outside any dialog: closing the
 * dialog or changing page does not stop them. The server takes one upload per person at a time anyway
 * (its library lock), and one at a time gives each file the whole connection.
 *
 * A failure the network or the server caused is tried again by itself, twice, then waits for "Thử lại".
 * It is only resent by itself when nothing can have been stored; a connection lost after the last byte
 * may have been saved, so that one waits for the person, who can look first.
 */
export class UploadStore {
  private jobs: readonly UploadJob[] = []
  private readonly requests = new Map<string, UploadRequest>()
  private readonly listeners = new Set<() => void>()
  private readonly settledListeners = new Set<(job: UploadJob) => void>()
  private running: { id: string; abort: AbortController } | null = null
  private timer: number | null = null
  private seq = 0
  /** Set by the provider: a 403 that is not the screen's own answer means the session changed. */
  accessLost: (err: unknown) => boolean = () => false

  subscribe = (listener: () => void) => {
    this.listeners.add(listener)
    return () => void this.listeners.delete(listener)
  }

  snapshot = () => this.jobs

  onSettled(listener: (job: UploadJob) => void) {
    this.settledListeners.add(listener)
    return () => void this.settledListeners.delete(listener)
  }

  enqueue(requests: readonly UploadRequest[]) {
    const added = requests.map((r): UploadJob => {
      const id = `upload-${++this.seq}`
      this.requests.set(id, r)
      return {
        id,
        name: r.name,
        kind: r.kind,
        where: r.where,
        href: r.href,
        target: r.target,
        status: 'queued',
        progress: 0,
        attempt: 0,
        canKeep: !!r.keep,
      }
    })
    this.set([...this.jobs, ...added])
    this.pump()
  }

  cancel(id: string) {
    if (this.running?.id === id) this.running.abort.abort()
    else this.patch(id, { status: 'canceled', retryAt: undefined, retryable: true }, 'queued')
  }

  retry(id: string) {
    const job = this.jobs.find((j) => j.id === id)
    if (job?.status !== 'failed' && job?.status !== 'canceled') return
    this.patch(id, { status: 'queued', progress: 0, error: undefined, retryable: undefined, attempt: 0 })
    this.pump()
  }

  keep(id: string) {
    this.requests.get(id)?.keep?.()
  }

  remove(id: string) {
    if (this.running?.id === id) this.running.abort.abort()
    this.requests.delete(id)
    this.set(this.jobs.filter((j) => j.id !== id))
  }

  /** Drops every job that is over; what is still on its way, or exists nowhere else, stays. */
  clearFinished() {
    const stays = (j: UploadJob) => isPending(j) || isUnsaved(j)
    for (const j of this.jobs) if (!stays(j)) this.requests.delete(j.id)
    this.set(this.jobs.filter(stays))
  }

  /** Back online: what was waiting out a network error goes now. */
  resume = () => {
    this.set(this.jobs.map((j) => (j.retryAt ? { ...j, retryAt: undefined } : j)))
    this.pump()
  }

  /** The person signed out: what is on its way stops. Listeners stay, as StrictMode remounts the provider. */
  dispose() {
    this.running?.abort.abort()
    if (this.timer !== null) window.clearTimeout(this.timer)
    this.timer = null
  }

  private pump() {
    if (this.running) return
    const now = Date.now()
    const job = this.jobs.find((j) => j.status === 'queued' && (!j.retryAt || j.retryAt <= now))
    if (!job) return this.wake()
    void this.run(job)
  }

  /** Sleeps until the next job waiting to try again is due. */
  private wake() {
    if (this.timer !== null) window.clearTimeout(this.timer)
    this.timer = null
    const due = this.jobs.filter((j) => j.status === 'queued' && j.retryAt).map((j) => j.retryAt!)
    if (due.length === 0) return
    this.timer = window.setTimeout(
      () => {
        this.timer = null
        this.pump()
      },
      Math.max(0, Math.min(...due) - Date.now()),
    )
  }

  private async run(job: UploadJob) {
    const request = this.requests.get(job.id)
    if (!request) return
    const abort = new AbortController()
    this.running = { id: job.id, abort }
    let allSent = false
    this.patch(job.id, { status: 'sending', progress: 0, retryAt: undefined, attempt: job.attempt + 1 })
    try {
      await request.send({
        signal: abort.signal,
        onProgress: (loaded, total) => {
          allSent = loaded >= total
          this.patch(job.id, allSent ? { status: 'saving', progress: 1 } : { progress: loaded / total })
        },
      })
      this.settle(job.id, { status: 'done', progress: 1 })
    } catch (err) {
      this.fail(job.id, request, err, allSent)
    } finally {
      this.running = null
    }
    this.pump()
  }

  private fail(id: string, request: UploadRequest, err: unknown, allSent: boolean) {
    if (err instanceof DOMException && err.name === 'AbortError') {
      return this.patch(id, { status: 'canceled', retryable: true })
    }
    if (!request.isExpected?.(err) && this.accessLost(err)) {
      // Every next file would be refused the same way.
      const error = errorMessage(err)
      for (const j of this.jobs)
        if (j.id === id || j.status === 'queued') this.settle(j.id, { status: 'failed', error, retryable: false })
      return
    }
    const kind = outcome(err, allSent)
    const attempt = this.jobs.find((j) => j.id === id)?.attempt ?? 1
    if ((kind === UNSENT || kind === REFUSED_SAFE) && attempt <= RETRY_DELAYS_MS.length) {
      return this.patch(id, {
        status: 'queued',
        progress: 0,
        error: errorMessage(err),
        // Offline: wait for the browser to say the network is back (`resume`), not for the clock.
        retryAt: Date.now() + (navigator.onLine ? RETRY_DELAYS_MS[attempt - 1] : 24 * 60 * 60 * 1000),
      })
    }
    this.settle(id, {
      status: 'failed',
      error:
        kind === UNKNOWN
          ? 'Mất kết nối lúc máy chủ đang lưu, có thể tệp đã được lưu. Xem lại danh sách trước khi thử lại.'
          : fileError(err),
      retryable: kind !== null,
    })
  }

  /** The job is over: the screens that show its place re-read it. */
  private settle(id: string, change: Partial<UploadJob>) {
    this.patch(id, { ...change, retryAt: undefined })
    const job = this.jobs.find((j) => j.id === id)
    if (job) for (const listener of this.settledListeners) listener(job)
  }

  private patch(id: string, change: Partial<UploadJob>, onlyIf?: UploadStatus) {
    this.set(this.jobs.map((j) => (j.id === id && (!onlyIf || j.status === onlyIf) ? { ...j, ...change } : j)))
  }

  private set(jobs: readonly UploadJob[]) {
    this.jobs = jobs
    for (const listener of this.listeners) listener()
  }
}
