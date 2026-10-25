import type { ReactNode } from 'react'

/**
 * Title block every screen opens with: the heading, an optional line of data about the list (a
 * count, not an explanation of the screen), and an optional action on the right.
 */
export function PageHeader({
  title,
  description,
  action,
}: {
  title: string
  description?: string
  action?: ReactNode
}) {
  return (
    <div className="page-header">
      <div>
        <h1>{title}</h1>
        {description && <p className="page-header-desc">{description}</p>}
      </div>
      {action}
    </div>
  )
}
