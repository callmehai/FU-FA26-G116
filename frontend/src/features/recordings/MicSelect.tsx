import { Select } from '../../components/Select'
import { shortDeviceName } from '../calls/room/roomSupport'
import type { MicChoice } from './micChoice'

/**
 * The dropdown of microphones. A field with its label in a dialog; `compact` is the icon button beside the room mic's
 * switch, the chosen mic in its tooltip. Nothing is shown while there is no list to choose from.
 */
export function MicSelect({
  choice,
  disabled,
  compact = false,
}: {
  choice: MicChoice
  disabled?: boolean
  compact?: boolean
}) {
  if (choice.options.length === 0) return null
  const options = choice.options.map((o) => ({
    value: o.id,
    label: shortDeviceName(o.label),
    hint: o.isDefault ? 'Mặc định' : undefined,
  }))
  const current = options.find((o) => o.value === choice.chosenId)
  const select = (
    <Select
      value={choice.chosenId || null}
      options={options}
      onChange={choice.pick}
      label="Micro"
      size={compact ? 'sm' : undefined}
      icon={compact ? 'chevronDown' : undefined}
      tooltip={compact && current ? `Micro: ${current.label}` : undefined}
      disabled={disabled}
    />
  )
  return compact ? (
    select
  ) : (
    <div className="field mic-select">
      <span>Micro</span>
      {select}
    </div>
  )
}
