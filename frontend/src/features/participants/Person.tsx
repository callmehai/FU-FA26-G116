import { avatarColors, initials } from '../../lib/avatar'

/** Avatar, name and email: one person in the roster, the picker and its dialogs. */
export function Person({ name, email }: { name: string; email: string }) {
  const colors = avatarColors(name)
  return (
    <span className="person">
      <span className="avatar" style={{ background: colors.bg, color: colors.fg }} aria-hidden="true">
        {initials(name)}
      </span>
      <span className="person-text">
        <span className="person-name">{name}</span>
        <span className="cell-sub">{email}</span>
      </span>
    </span>
  )
}
