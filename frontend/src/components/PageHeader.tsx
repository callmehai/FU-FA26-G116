import type { ReactNode } from 'react'

/**
 * Title block every screen opens with: an optional section name above the heading, one line of
 * context, and an optional action on the right.
 */
export function PageHeader({
  title,
  eyebrow,
  description,
  action,
}: {
  title: string
  /** The section the screen belongs to ("Quản trị"), shown small above the title. */
  eyebrow?: string
  description?: string
  action?: ReactNode
}) {
  return (
    <div className="page-header">
      <div>
        {eyebrow && <p className="page-eyebrow">{eyebrow}</p>}
        <h1>{title}</h1>
        {description && <p className="page-header-desc">{description}</p>}
      </div>
      {action}
    </div>
  )
}
