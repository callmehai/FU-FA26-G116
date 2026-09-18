import type { ReactNode } from 'react'

/** Title block every screen opens with: heading, one line of context, and an optional action. */
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
