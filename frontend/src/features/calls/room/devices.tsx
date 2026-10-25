import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react'
import { useMediaDeviceSelect } from '@livekit/components-react'
import { createAudioAnalyser, type LocalAudioTrack, TrackEvent } from 'livekit-client'
import { Icon, type IconName } from '../../../components/Icon'
import { useToast } from '../../../components/toastContext'
import { errorMessage } from '../../../lib/errors'
import { type DeviceOption, deviceErrorMessage, deviceOptions, noDeviceReason, shortDeviceName } from './roomSupport'

/**
 * Three bars that follow your own voice while the microphone is on, as Meet's do. Read from the
 * local track, so they move at once instead of waiting for the server's speaker updates.
 */
export function MicLevel({ track }: { track: LocalAudioTrack }) {
  const ref = useRef<HTMLSpanElement>(null)
  // A device switch restarts the track on a new capture; the analyser has to follow it.
  const [capture, setCapture] = useState(0)

  useEffect(() => {
    const restarted = () => setCapture((n) => n + 1)
    track.on(TrackEvent.Restarted, restarted)
    return () => {
      track.off(TrackEvent.Restarted, restarted)
    }
  }, [track])

  useEffect(() => {
    const bars = ref.current
    if (!bars || !track.mediaStreamTrack) return
    // The library's default range (-100 to -80 dB) saturates on a quiet room's hiss; -80 to -20 dB
    // leaves a quiet room near 0.03 and puts speech well above it.
    const { calculateVolume, cleanup } = createAudioAnalyser(track, {
      smoothingTimeConstant: 0.5,
      minDecibels: -80,
      maxDecibels: -20,
    })
    let frame = 0
    let last = 0
    const tick = (now: number) => {
      if (now - last >= 60) {
        last = now
        bars.style.setProperty('--level', Math.min(1, Math.max(0, (calculateVolume() - 0.04) / 0.2)).toFixed(2))
      }
      frame = requestAnimationFrame(tick)
    }
    frame = requestAnimationFrame(tick)
    return () => {
      cancelAnimationFrame(frame)
      void cleanup()
    }
  }, [track, capture])

  return (
    <span className="room-level" ref={ref} aria-hidden="true">
      <span />
      <span />
      <span />
    </span>
  )
}

/**
 * A device picker as Meet draws one: a pill with the device's short name that opens a menu of every
 * device, the chosen one ticked. `activeId` may be "" or "default" for "whatever the system uses".
 */
export function DevicePicker({
  title,
  icon,
  options,
  activeId,
  emptyReason,
  onPick,
}: {
  title: string
  icon: IconName
  options: DeviceOption[]
  activeId: string
  emptyReason: string
  onPick: (id: string) => void
}) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  const triggerRef = useRef<HTMLButtonElement>(null)
  const menuId = useId()
  // The list arrives a moment after the picker appears; only call it empty once it has had time to.
  const [settled, setSettled] = useState(false)
  useEffect(() => {
    const timer = window.setTimeout(() => setSettled(true), 1000)
    return () => window.clearTimeout(timer)
  }, [])
  const active =
    options.find((o) => o.id === activeId) ??
    (activeId === '' || activeId === 'default' ? options.find((o) => o.isDefault) : undefined) ??
    options[0]

  useEffect(() => {
    if (!open) return
    ref.current?.querySelector<HTMLElement>('[role="menuitemradio"][aria-checked="true"]')?.focus()
    const onPointer = (event: PointerEvent) => {
      if (!ref.current?.contains(event.target as Node)) setOpen(false)
    }
    const onKey = (event: globalThis.KeyboardEvent) => {
      if (event.key !== 'Escape') return
      setOpen(false)
      triggerRef.current?.focus()
    }
    document.addEventListener('pointerdown', onPointer)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('pointerdown', onPointer)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  function pick(id: string) {
    setOpen(false)
    triggerRef.current?.focus()
    onPick(id)
  }

  function onMenuKey(event: KeyboardEvent<HTMLDivElement>) {
    // Tab leaves the menu for the next control, so the menu goes with it.
    if (event.key === 'Tab') {
      setOpen(false)
      return
    }
    const items = [...event.currentTarget.querySelectorAll<HTMLElement>('[role="menuitemradio"]')]
    const at = items.indexOf(document.activeElement as HTMLElement)
    const next =
      event.key === 'ArrowDown'
        ? (at + 1) % items.length
        : event.key === 'ArrowUp'
          ? (at + items.length - 1) % items.length
          : event.key === 'Home'
            ? 0
            : event.key === 'End'
              ? items.length - 1
              : -1
    if (next < 0) return
    event.preventDefault()
    items[next]?.focus()
  }

  if (options.length === 0) {
    const reason = settled ? emptyReason : title
    return (
      <div className="room-select">
        <button type="button" className="room-select-trigger" disabled aria-label={`${title}: ${reason}`}>
          <Icon name={icon} size={18} />
          <span className="room-select-value">{reason}</span>
        </button>
      </div>
    )
  }

  return (
    <div className="room-select" ref={ref}>
      <button
        ref={triggerRef}
        type="button"
        className="room-select-trigger"
        aria-label={`${title}: ${active?.label ?? ''}`}
        title={active?.label}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={open ? menuId : undefined}
        onClick={() => setOpen((v) => !v)}
      >
        <Icon name={icon} size={18} />
        <span className="room-select-value">{active && shortDeviceName(active.label)}</span>
        <span className="room-select-caret">
          <Icon name="chevronDown" size={16} />
        </span>
      </button>
      {open && (
        <div className="room-select-menu" role="menu" id={menuId} aria-label={title} onKeyDown={onMenuKey}>
          {options.map((option) => {
            const checked = option.id === active?.id
            return (
              <button
                key={option.id}
                type="button"
                role="menuitemradio"
                aria-checked={checked}
                tabIndex={checked ? 0 : -1}
                className={`room-select-item${checked ? ' is-active' : ''}`}
                onClick={() => pick(option.id)}
              >
                <span className="room-select-check">{checked && <Icon name="check" size={18} />}</span>
                <span className="room-select-text">
                  <span className="room-select-name">{option.label}</span>
                  {option.isDefault && <span className="room-select-note">Theo mặc định của hệ thống</span>}
                </span>
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}

/** A picker inside the room: switches the device the call is using (SCRUM-42). */
export function DeviceSelect({ kind, icon, title }: { kind: MediaDeviceKind; icon: IconName; title: string }) {
  const toast = useToast()
  const { devices, activeDeviceId, setActiveMediaDevice } = useMediaDeviceSelect({
    kind,
    requestPermissions: kind !== 'audiooutput',
  })

  return (
    <DevicePicker
      title={title}
      icon={icon}
      options={deviceOptions(devices, title)}
      activeId={activeDeviceId}
      emptyReason={noDeviceReason(kind, title)}
      onPick={(id) =>
        setActiveMediaDevice(id).catch((err: unknown) =>
          toast.show(
            kind === 'audiooutput' ? errorMessage(err) : deviceErrorMessage(err, kind === 'audioinput' ? 'micro' : 'camera'),
            'error',
          ),
        )
      }
    />
  )
}
