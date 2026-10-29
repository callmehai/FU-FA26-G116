import { Icon } from './Icon'
import { Select } from './Select'

/**
 * Page numbers to show: always the first and last page, the current one and its neighbours, with a
 * gap marker where two or more pages are skipped. Seven or eight slots, so the bar barely changes width.
 */
function pageWindow(page: number, totalPages: number): (number | 'gap')[] {
  if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1)
  let start = Math.max(2, Math.min(page - 1, totalPages - 4))
  let end = Math.min(totalPages - 1, Math.max(page + 1, 5))
  // A gap standing for a single page hides nothing worth hiding: show that page instead.
  if (start === 3) start = 2
  if (end === totalPages - 2) end = totalPages - 1
  const middle = Array.from({ length: end - start + 1 }, (_, i) => start + i)
  return [
    1,
    ...(start > 2 ? ['gap' as const] : []),
    ...middle,
    ...(end < totalPages - 1 ? ['gap' as const] : []),
    totalPages,
  ]
}

/**
 * The footer of a server-paged table: where you are ("21–40 trong 45"), how many rows per page, and
 * the page buttons. Counts come from the server, so they cover every page, not just this one.
 */
export function Pagination({
  page,
  pageSize,
  totalCount,
  totalPages,
  pageSizes,
  itemLabel,
  onPageChange,
  onPageSizeChange,
  disabled = false,
}: {
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  pageSizes: readonly number[]
  /** What is being counted, e.g. "cuộc họp". */
  itemLabel: string
  onPageChange: (page: number) => void
  onPageSizeChange: (pageSize: number) => void
  disabled?: boolean
}) {
  const first = totalCount === 0 ? 0 : (page - 1) * pageSize + 1
  const last = Math.min(page * pageSize, totalCount)
  const count = (n: number) => n.toLocaleString('vi-VN')

  return (
    <nav className="pagination" aria-label="Phân trang">
      <p className="pagination-summary" aria-live="polite">
        {totalCount === 0 ? (
          `0 ${itemLabel}`
        ) : (
          <>
            Hiển thị{' '}
            <b>
              {count(first)}–{count(last)}
            </b>{' '}
            trong <b>{count(totalCount)}</b> {itemLabel}
          </>
        )}
      </p>

      <span className="pagination-size">
        Số dòng
        <Select
          value={String(pageSize)}
          options={pageSizes.map((size) => ({ value: String(size), label: String(size) }))}
          onChange={(value) => onPageSizeChange(Number(value))}
          label="Số dòng mỗi trang"
        />
      </span>

      <div className="pagination-controls">
        <button
          type="button"
          className="icon-button pagination-step"
          onClick={() => onPageChange(page - 1)}
          disabled={disabled || page <= 1}
          aria-label="Trang trước"
          data-tooltip="Trang trước"
        >
          <Icon name="chevronLeft" size={16} />
        </button>
        {pageWindow(page, totalPages).map((item, index) =>
          item === 'gap' ? (
            <span key={`gap-${index}`} className="pagination-gap" aria-hidden="true">
              …
            </span>
          ) : (
            <button
              key={item}
              type="button"
              className={
                item === page
                  ? 'pagination-page pagination-page-current'
                  : // A phone keeps only the first, the current and the last page: the bar fits one line.
                    item === 1 || item === totalPages
                    ? 'pagination-page'
                    : 'pagination-page pagination-page-near'
              }
              onClick={() => onPageChange(item)}
              disabled={disabled}
              aria-current={item === page ? 'page' : undefined}
              aria-label={`Trang ${item}`}
            >
              {item}
            </button>
          ),
        )}
        <button
          type="button"
          className="icon-button pagination-step"
          onClick={() => onPageChange(page + 1)}
          disabled={disabled || page >= totalPages}
          aria-label="Trang sau"
          data-tooltip="Trang sau"
        >
          <Icon name="chevronRight" size={16} />
        </button>
      </div>
    </nav>
  )
}
