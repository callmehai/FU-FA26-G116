import { useCallback, useEffect, useState } from 'react'

/** Document Picture-in-Picture (Chrome 116+), not yet in TypeScript's DOM types. */
interface DocumentPictureInPicture {
  requestWindow(options?: { width?: number; height?: number }): Promise<Window>
}

function pipApi(): DocumentPictureInPicture | undefined {
  return (window as Window & { documentPictureInPicture?: DocumentPictureInPicture }).documentPictureInPicture
}

/** Copies the page's stylesheets into the floating window, which starts as a blank document. */
function copyStyles(target: Document) {
  for (const sheet of [...document.styleSheets]) {
    try {
      const style = target.createElement('style')
      style.textContent = [...sheet.cssRules].map((rule) => rule.cssText).join('\n')
      target.head.appendChild(style)
    } catch {
      // A cross-origin sheet (the web font) cannot be read; link it instead.
      if (!sheet.href) continue
      const link = target.createElement('link')
      link.rel = 'stylesheet'
      link.href = sheet.href
      target.head.appendChild(link)
    }
  }
}

/**
 * A floating, always-on-top window for the call, like Google Meet's: it keeps the presenter's
 * screen, a camera and the main controls in view while the user works in another tab or app.
 * Chrome opens it on its own when the user switches tab during the call ("enterpictureinpicture",
 * Chrome 134+); the room also has a button for it.
 */
export function usePipWindow() {
  const supported = pipApi() !== undefined
  const [pip, setPip] = useState<Window | null>(null)

  const open = useCallback(async () => {
    const api = pipApi()
    if (!api || pip) return
    try {
      const win = await api.requestWindow({ width: 375, height: 375 })
      copyStyles(win.document)
      win.document.title = 'Phòng họp — TrustCabinet'
      win.document.body.classList.add('room-pip-body')
      win.addEventListener('pagehide', () => setPip(null), { once: true })
      setPip(win)
    } catch {
      // Refused (no user gesture, or a window is already open): the room carries on as it is.
    }
  }, [pip])

  const close = useCallback(() => pip?.close(), [pip])

  // Automatic PiP on tab switch, where the browser supports the action.
  useEffect(() => {
    if (!supported || !('mediaSession' in navigator)) return
    const action = 'enterpictureinpicture' as MediaSessionAction
    try {
      navigator.mediaSession.setActionHandler(action, () => void open())
    } catch {
      return
    }
    return () => {
      try {
        navigator.mediaSession.setActionHandler(action, null)
      } catch {
        // Nothing to undo.
      }
    }
  }, [supported, open])

  // Leaving the room closes the floating window with it.
  useEffect(() => () => pip?.close(), [pip])

  return { supported, pip, open, close }
}
