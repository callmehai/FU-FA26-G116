import { Fragment } from 'react'
import { avatarColors, initials } from '../../lib/avatar'

/**
 * Avatar, name and email: one person in the roster, the picker and its dialogs. `self` marks the
 * signed-in user's own row with a quiet "(Tôi)", as AI-eCabinet does. A guest is not sent other people's
 * email, so the line is left out when there is none.
 */
export function Person({ name, email, self = false }: { name: string; email: string | null; self?: boolean }) {
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
        {/* A narrow roster wraps the email before an "@" or a "." rather than mid-word (SCRUM-151). */}
        {email && (
          <span className="cell-sub">
            {email.split(/(?=[@.])/).map((part, i) => (
              <Fragment key={i}>
                {i > 0 && <wbr />}
                {part}
              </Fragment>
            ))}
          </span>
        )}
      </span>
    </span>
  )
}
