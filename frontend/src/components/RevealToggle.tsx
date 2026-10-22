import { Icon } from './Icon'

/** The show/hide button inside a password box (`.input-reveal`). */
export function RevealToggle({ shown, onToggle }: { shown: boolean; onToggle: () => void }) {
  const label = shown ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'
  return (
    <button
      type="button"
      className="icon-button"
      onClick={onToggle}
      data-tooltip={label}
      aria-label={label}
      aria-pressed={shown}
    >
      <Icon name={shown ? 'eyeOff' : 'eye'} size={16} />
    </button>
  )
}
