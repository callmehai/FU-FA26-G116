/**
 * SCRUM-109 — sentences waiting to be sent, kept on this device until the server has them, as AI-eCabinet does: a
 * closed tab, a dropped network or a transcriber that is down loses nothing. The next visit to any meeting page sends
 * them. The audio is the speaker's own voice, so it goes as soon as the server confirms the sentence, and after a week
 * (when the server would refuse it anyway) it is dropped unsent.
 *
 * Any failure of IndexedDB itself (private browsing, storage full) is swallowed: the queue then lives in memory only,
 * as before.
 */

export interface StoredClip {
  id: string
  meetingId: string
  startedAt: string
  durationMs: number
  audio: Blob
}

const DB = 'trustcabinet-speech'
const STORE = 'clips'
/** SpeechClipRules.LateUpload on the server, plus a day for a meeting that ran late. */
export const KEEP_MS = 8 * 24 * 3600 * 1000

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB, 1)
    request.onupgradeneeded = () => request.result.createObjectStore(STORE, { keyPath: 'id' })
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error)
  })
}

function run<T>(mode: IDBTransactionMode, action: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  return open().then(
    (db) =>
      new Promise<T>((resolve, reject) => {
        const tx = db.transaction(STORE, mode)
        const request = action(tx.objectStore(STORE))
        tx.oncomplete = () => {
          db.close()
          resolve(request.result)
        }
        tx.onerror = tx.onabort = () => {
          db.close()
          reject(tx.error ?? request.error)
        }
      }),
  )
}

const quietly = <T>(work: () => Promise<T>, fallback: T) =>
  typeof indexedDB === 'undefined' ? Promise.resolve(fallback) : work().catch(() => fallback)

export const clipStore = {
  put: (clip: StoredClip) => quietly(() => run('readwrite', (s) => s.put(clip)).then(() => undefined), undefined),
  remove: (id: string) => quietly(() => run('readwrite', (s) => s.delete(id)).then(() => undefined), undefined),
  /** Every sentence still waiting, on any meeting; those older than a week are dropped here. */
  async all(now = Date.now()): Promise<StoredClip[]> {
    const clips = await quietly(() => run('readonly', (s) => s.getAll() as IDBRequest<StoredClip[]>), [])
    const stale = clips.filter((c) => now - Date.parse(c.startedAt) > KEEP_MS)
    await Promise.all(stale.map((c) => clipStore.remove(c.id)))
    return clips.filter((c) => !stale.includes(c))
  },
}
