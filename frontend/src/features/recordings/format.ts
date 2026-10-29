/** Lengths and sizes as the recordings panel shows them. */

export function formatDuration(ms: number) {
  const total = Math.round(ms / 1000)
  const h = Math.floor(total / 3600)
  const m = Math.floor((total % 3600) / 60)
  const s = total % 60
  const mm = String(m).padStart(2, '0')
  const ss = String(s).padStart(2, '0')
  return h > 0 ? `${h}:${mm}:${ss}` : `${mm}:${ss}`
}

export function formatSize(bytes: number) {
  return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`
}

/** The length of an audio file as the browser reads it, or undefined when it cannot tell within a few seconds. */
export function probeDuration(file: Blob): Promise<number | undefined> {
  return new Promise((resolve) => {
    const url = URL.createObjectURL(file)
    const audio = new Audio()
    const done = (ms?: number) => {
      window.clearTimeout(timer)
      URL.revokeObjectURL(url)
      resolve(ms)
    }
    const timer = window.setTimeout(() => done(), 4000)
    audio.preload = 'metadata'
    audio.onloadedmetadata = () => done(Number.isFinite(audio.duration) ? audio.duration * 1000 : undefined)
    audio.onerror = () => done()
    audio.src = url
  })
}

/** A place on a player, `m:ss`; minutes pass 59 (75:12) rather than turning into hours. */
export function formatClock(ms: number) {
  const total = Math.max(0, Math.floor(ms / 1000))
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`
}
