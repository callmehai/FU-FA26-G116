import { useCallback, useEffect, useState } from 'react'
import { deviceOptions } from '../calls/room/roomSupport'

/**
 * Which microphone records, chosen wherever something is recorded outside the call room: a voice sample (one's own
 * or someone else's) and the meeting page's room mic. A sample only matches the meeting's voices when both went
 * through the same mic (Hải 25/11/2026: samples from a laptop's own mic, the meeting from a USB room mic, and three
 * voices out of four went unnamed), so the choice is remembered in this browser and shared with the call room.
 */

/** The call room's per-browser preferences; only the mic is read and written here, the rest is left as it is. */
const PREFS_KEY = 'trustcabinet.call.prefs'

function readSaved(): { id: string; label: string } {
  try {
    const saved = JSON.parse(localStorage.getItem(PREFS_KEY) ?? '{}') as { micId?: string; micLabel?: string }
    return { id: saved.micId ?? '', label: saved.micLabel ?? '' }
  } catch {
    return { id: '', label: '' }
  }
}

function writeSaved(id: string, label: string) {
  try {
    const saved = JSON.parse(localStorage.getItem(PREFS_KEY) ?? '{}') as Record<string, unknown>
    localStorage.setItem(PREFS_KEY, JSON.stringify({ ...saved, micId: id, micLabel: label }))
  } catch {
    // Private mode or blocked storage: the choice lasts while the screen is open.
  }
}

/**
 * The microphones and the one chosen. Names come only once the site may use the mic, so the list is read again when
 * `refresh` changes (after a recording starts) and whenever a device is plugged in or out. A saved choice whose id
 * changed is found again by its name; one that is gone falls back to the system default ("").
 */
export function useMicChoice(refresh?: unknown) {
  const [devices, setDevices] = useState<MediaDeviceInfo[]>([])
  const [saved, setSaved] = useState(readSaved)

  const reload = useCallback(() => {
    void navigator.mediaDevices
      ?.enumerateDevices()
      .then((all) => setDevices(all.filter((d) => d.kind === 'audioinput')), () => undefined)
  }, [])
  useEffect(() => {
    const media = navigator.mediaDevices
    if (!media?.enumerateDevices) return
    reload()
    media.addEventListener('devicechange', reload)
    return () => media.removeEventListener('devicechange', reload)
  }, [refresh, reload])

  // Without permission the browser lists one nameless mic: nothing to choose between yet.
  const options = devices.some((d) => d.label) ? deviceOptions(devices, 'Micro') : []
  const known = devices.filter((d) => d.deviceId)
  const deviceId =
    !saved.id || known.length === 0 || known.some((d) => d.deviceId === saved.id)
      ? saved.id
      : (known.find((d) => saved.label !== '' && d.label === saved.label)?.deviceId ?? '')

  const pick = useCallback(
    (id: string) => {
      const label = devices.find((d) => d.deviceId === id)?.label ?? ''
      writeSaved(id, label)
      setSaved({ id, label })
    },
    [devices],
  )

  // "default" is Chrome's alias for whatever the system uses; recording with no id does the same.
  const chosen = options.find((o) => o.id === deviceId) ?? options.find((o) => o.isDefault) ?? options[0]
  return {
    /** For getUserMedia: undefined records with the system's mic. */
    deviceId: deviceId && deviceId !== 'default' ? deviceId : undefined,
    options,
    chosenId: chosen?.id ?? '',
    pick,
  }
}

export type MicChoice = ReturnType<typeof useMicChoice>

/**
 * Opens the microphone `deviceId` names, or the system's when there is none. Chrome treats an `ideal` device as a
 * hint it may drop for the other constraints and hands back the default mic, so the choice is asked for `exact`; a mic
 * unplugged since it was chosen falls back to the default rather than failing.
 */
export async function openChosenMic(audio: MediaTrackConstraints, deviceId?: string): Promise<MediaStream> {
  if (!deviceId) return navigator.mediaDevices.getUserMedia({ audio })
  try {
    return await navigator.mediaDevices.getUserMedia({ audio: { ...audio, deviceId: { exact: deviceId } } })
  } catch (error) {
    if (!(error instanceof Error) || !['OverconstrainedError', 'NotFoundError'].includes(error.name)) throw error
    return navigator.mediaDevices.getUserMedia({ audio })
  }
}
