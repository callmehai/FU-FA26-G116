import { avatarColors, initials } from '../../lib/avatar'

/**
 * Avatar, name and email: one person in the roster, the picker and its dialogs. `self` marks the
 * signed-in user's own row with a quiet "(Tôi)", as AI-eCabinet does.
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
          {self && <span className="person-self">(Tôi)</span>}
        </span>
        <span className="cell-sub">{email}</span>
      </span>
    </span>
  )
}
