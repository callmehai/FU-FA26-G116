import { useEffect, useState, useSyncExternalStore, type ReactNode } from 'react'
import { useAccessLost } from '../auth/useAccessLost'
import { UploadsContext } from './uploadsContext'
import { isPending, isUnsaved, UploadStore } from './uploadStore'
import { UploadTray } from './UploadTray'

/**
 * The signed-in person's upload queue, above every screen so a page change does not stop it, and its tray.
 * While a file is still on its way, or a recording failed and exists nowhere else, closing or reloading the tab
 * asks first: that would lose it.
 */
export function UploadsProvider({ children }: { children: ReactNode }) {
  const [store] = useState(() => new UploadStore())
  const handleAccessLost = useAccessLost()
  const holding = useSyncExternalStore(store.subscribe, () =>
    store.snapshot().some((j) => isPending(j) || isUnsaved(j)),
  )

  useEffect(() => {
    store.accessLost = handleAccessLost
  }, [store, handleAccessLost])

  useEffect(() => {
    window.addEventListener('online', store.resume)
    return () => window.removeEventListener('online', store.resume)
  }, [store])

  useEffect(() => () => store.dispose(), [store])

  useEffect(() => {
    if (!holding) return
    const hold = (event: BeforeUnloadEvent) => event.preventDefault()
    window.addEventListener('beforeunload', hold)
    return () => window.removeEventListener('beforeunload', hold)
  }, [holding])

  return (
    <UploadsContext value={store}>
      {children}
      <UploadTray />
    </UploadsContext>
  )
}
