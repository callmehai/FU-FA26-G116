import { Icon } from './Icon'

/**
 * A list toolbar's "Xoá bộ lọc", icon-only so the toolbar stays on one line. It keeps its place while there is
 * nothing to clear (hidden, not removed), so picking a filter neither shrinks the search box nor shifts the
 * filters beside it. A funnel with a cross, so it does not read as the search box's own "×" next to it.
 */
export function ClearFiltersButton({ shown, onClear }: { shown: boolean; onClear: () => void }) {
  return (
    <button
      type="button"
      className="icon-button clear-filters"
      onClick={onClear}
      data-tooltip={shown ? 'Xoá bộ lọc' : undefined}
      aria-label="Xoá bộ lọc"
      aria-hidden={!shown || undefined}
      tabIndex={shown ? undefined : -1}
      data-hidden={shown ? undefined : ''}
      disabled={!shown}
    >
      <Icon name="filterOff" size={16} />
    </button>
  )
}
