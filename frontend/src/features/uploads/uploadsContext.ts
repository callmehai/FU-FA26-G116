import { createContext, useContext, useEffect, useRef, useSyncExternalStore } from 'react'
import type { UploadJob, UploadStore } from './uploadStore'

export const UploadsContext = createContext<UploadStore | null>(null)

export function useUploads(): UploadStore {
  const store = useContext(UploadsContext)
  if (!store) throw new Error('useUploads needs an UploadsProvider')
  return store
}

export function useUploadJobs() {
  const store = useUploads()
  return useSyncExternalStore(store.subscribe, store.snapshot)
}

/**
 * Calls `onSettled` when an upload to `target` ends, done or not, so the screen shows its place as it is.
 * `'*'` hears every upload: the library, where a meeting's upload lands as well (BR-192).
 */
export function useUploadsSettled(target: string, onSettled: (job: UploadJob) => void) {
  const store = useUploads()
  const callback = useRef(onSettled)
  useEffect(() => {
    callback.current = onSettled
  })
  useEffect(
    () => store.onSettled((job) => (target === '*' || job.target === target) && callback.current(job)),
    [store, target],
  )
}
