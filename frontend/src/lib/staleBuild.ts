/**
 * Reload once when a screen's code is gone because the server was deployed under an open tab.
 *
 * Every screen but the first is a lazy chunk named by its hash (`MeetingRoomPage-BVDdUZv2.js`). A
 * deploy replaces those files, so a tab opened before it still asks for the old names, gets a 404
 * and draws nothing: the meeting room stayed white until the user pressed F5. Vite reports the
 * failed import as `vite:preloadError`; a reload fetches the new index.html (served `no-cache`)
 * and with it the new chunk names.
 *
 * A chunk missing for another reason would fail again after the reload, so a second failure within
 * RETRY_WINDOW_MS is left to surface instead of reloading forever.
 */
const RELOADED_AT_KEY = 'tc:stale-build-reload-at'
const RETRY_WINDOW_MS = 30_000

function lastReload(): number {
  try {
    return Number(sessionStorage.getItem(RELOADED_AT_KEY)) || 0
  } catch {
    return 0
  }
}

export function installStaleBuildReload() {
  window.addEventListener('vite:preloadError', (event) => {
    const now = Date.now()
    if (now - lastReload() < RETRY_WINDOW_MS) return
    try {
      sessionStorage.setItem(RELOADED_AT_KEY, String(now))
    } catch {
      // Without storage the guard is gone; reload anyway, as the page is blank otherwise.
    }
    event.preventDefault()
    window.location.reload()
  })
}
