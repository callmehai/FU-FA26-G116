import { useEffect, type MouseEvent } from 'react'

/**
 * The call room and the meeting page live in two tabs (PO, 2026-11-19): joining opens the room in a tab of its own, so
 * the meeting page stays where it was, and each can bring the other forward. Every tab is named after the page it
 * shows, so a second "Vào phòng họp" finds the room already open instead of joining a second time.
 *
 * A browser finds a tab by name only among the tabs that opened one another; a room reached some other way (an
 * emailed link) simply opens the meeting page in a new tab.
 */
export function tabName(path: string) {
  return `trustcabinet:${path}`
}

/** Names this tab after `path` while the page is shown, so the room and the meeting page can find it. */
export function useTabName(path: string) {
  useEffect(() => {
    const name = tabName(path)
    window.name = name
    return () => {
      if (window.name === name) window.name = ''
    }
  }, [path])
}

/** A plain left click, which the page handles; one with a modifier (new tab, new window) is the browser's. */
export function isPlainClick(event: MouseEvent) {
  return event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey
}

/**
 * Brings forward the tab named for `path`, loading `path` into it only when it shows something else — a live call is
 * never reloaded — or opening a new tab when there is none. False when the browser refused to open one.
 */
export function showInTab(path: string): boolean {
  const name = tabName(path)
  if (window.name === name) return true
  const tab = window.open('', name)
  if (!tab) return false
  const target = new URL(path, window.location.origin)
  let shown: string | null = null
  try {
    shown = tab.location.pathname
  } catch {
    // Another site's page: load ours over it.
  }
  if (shown !== target.pathname) tab.location.href = target.href
  tab.focus()
  return true
}

/**
 * Leaves the room's tab for the meeting page: brings that tab forward and closes this one, when this tab was opened by
 * the app and so may close itself. False when it may not; the caller then shows the meeting page here.
 */
export function returnToMeetingTab(path: string): boolean {
  if (!window.opener) return false
  if (!showInTab(path)) return false
  window.close()
  return window.closed
}
