import { avatarColors, initials } from '../../lib/avatar'

/**
 * Avatar, name and email: one person in the roster, the picker and its dialogs. `self` marks the
 * signed-in user's own row with "(tôi)".
 */
export function Person({ name, email, self = false }: { name: string; email: string; self?: boolean }) {
  const colors = avatarColors(name)
  return (
    <span className="person">
      <span className="avatar" style={{ background: colors.bg, color: colors.fg }} aria-hidden="true">
        {initials(name)}
      </span>
      <span className="person-text">
        <span className="person-name">
          {name}
          {/* A no-break space keeps "(tôi)" with the last word when the name wraps. */}
          {self && <span className="person-self">{'\u00a0'}(tôi)</span>}
        </span>
        <span className="cell-sub">{email}</span>
      </span>
    </span>
  )
}
