import type { ComponentProps } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { isPlainClick, returnToMeetingTab, showInTab } from './meetingTabs'

type LinkProps = Omit<ComponentProps<typeof Link>, 'to'> & { to: string }

/**
 * "Vào phòng họp": opens the call room in a tab of its own, or brings it forward when it is already open, so the
 * page the person came from stays put. A browser that blocks the new tab gets the room here, as before.
 */
export function RoomLink({ to, onClick, ...props }: LinkProps) {
  const navigate = useNavigate()
  return (
    <Link
      {...props}
      to={to}
      onClick={(event) => {
        onClick?.(event)
        if (event.defaultPrevented || !isPlainClick(event)) return
        event.preventDefault()
        if (!showInTab(to)) navigate(to)
      }}
    />
  )
}

/**
 * The way back from the room's tab once the call is over or not joined: back to the meeting page's tab, closing this
 * one; here when this tab cannot close itself.
 */
export function MeetingPageLink({ to, onClick, ...props }: LinkProps) {
  return (
    <Link
      {...props}
      to={to}
      onClick={(event) => {
        onClick?.(event)
        if (!event.defaultPrevented && isPlainClick(event) && returnToMeetingTab(to)) event.preventDefault()
      }}
    />
  )
}
