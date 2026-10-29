import type { SavedTape, TapeStore } from './roomTape'

/**
 * ADR-0041 — the room mic's take, what the server does not have yet, kept on this device: one per meeting. A closed
 * tab, a crash or a dropped network loses nothing; the next visit to the meeting carries on or saves it. It goes as
 * soon as the server has the last part. Any failure of IndexedDB itself (private browsing, storage full) is
 * swallowed: the take then lives in memory only.
 */

const DB = 'trustcabinet-room-tape'
const STORE = 'tapes'

function open(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB, 1)
    request.onupgradeneeded = () => request.result.createObjectStore(STORE, { keyPath: 'meetingId' })
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

export const tapeStore: TapeStore = {
  load: (meetingId) =>
    quietly(
      () => run('readonly', (s) => s.get(meetingId) as IDBRequest<SavedTape | undefined>).then((t) => t ?? null),
      null,
    ),
  save: (tape) => quietly(() => run('readwrite', (s) => s.put(tape)).then(() => undefined), undefined),
  clear: (meetingId) => quietly(() => run('readwrite', (s) => s.delete(meetingId)).then(() => undefined), undefined),
}
