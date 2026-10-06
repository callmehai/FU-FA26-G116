import { Link } from 'react-router-dom'
import { Icon } from './Icon'

/**
 * The one link above a record or form page that leads back to where it was opened from. `state`
 * carries a back target one level further (detail → edit → detail → calendar).
 */
export function BackLink({ to, state, children }: { to: string; state?: unknown; children: string }) {
  return (
    <Link className="back-link" to={to} state={state}>
      <Icon name="chevronLeft" size={16} />
      {children}
    </Link>
  )
}
