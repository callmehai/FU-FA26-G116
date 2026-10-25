import { useId } from 'react'
import type { SelectOption } from './Select'

/**
 * A short, fixed set of mutually exclusive choices shown side by side — "Trực tiếp / Trực tuyến /
 * Kết hợp". Every option is visible and one click away, where a dropdown would hide them behind
 * an extra click. Use a `Select` once there are more than four options or the labels grow long.
 *
 * Built on native radio inputs, so arrow keys, form semantics and screen readers work unchanged;
 * the styling only hides the dots.
 */
export function SegmentedControl<T extends string>({
  value,
  options,
  onChange,
  label,
  disabled = false,
  counts,
}: {
  value: T
  options: SelectOption<T>[]
  onChange: (value: T) => void
  /** Accessible name of the group. */
  label: string
  disabled?: boolean
  /** A number after an option's label — how many items it holds. */
  counts?: Partial<Record<T, number>>
}) {
  const name = useId()

  return (
    <div className="segmented" role="radiogroup" aria-label={label}>
      {options.map((option) => (
        <label key={option.value} className="segmented-option">
          <input
            type="radio"
            name={name}
            value={option.value}
            checked={option.value === value}
            onChange={() => onChange(option.value)}
            disabled={disabled}
          />
          <span>
            {option.label}
            {counts?.[option.value] !== undefined && <b className="segmented-count">{counts[option.value]}</b>}
          </span>
        </label>
      ))}
    </div>
  )
}
