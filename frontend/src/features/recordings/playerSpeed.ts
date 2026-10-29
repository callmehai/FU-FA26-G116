/** The listening speed, kept per device and shared by every player (AI-eCabinet's `timeline.speed`). */

export const SPEEDS = [1, 1.25, 1.5, 2, 3]
const SPEED_KEY = 'trustcabinet.player.speed'

export function storedSpeed() {
  try {
    const value = Number(localStorage.getItem(SPEED_KEY))
    return SPEEDS.includes(value) ? value : 1
  } catch {
    return 1
  }
}

export function rememberSpeed(speed: number) {
  try {
    localStorage.setItem(SPEED_KEY, String(speed))
  } catch {
    // A private window: the speed lasts for this page only.
  }
}
