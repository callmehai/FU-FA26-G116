import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type ClipboardEvent,
  type FormEvent,
  type KeyboardEvent,
} from 'react'
import { sendGuestCode, verifyGuestCode } from '../../api/guest'
import { BrandMark } from '../../components/BrandMark'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { GuestInvitation } from '../../types/guest'
import { readGuestEntry, updateGuestEntry } from './guestStore'

const DIGITS = 6
/** From the third miss of five, a wrong code says how many tries are left, so the lock (MSG372) is no surprise. */
const WARN_FROM_REMAINING = 2

/** "45 giây", "3 phút", "2 giờ 5 phút": MSG379's wait can run to hours. */
function formatWait(seconds: number) {
  if (seconds < 60) return `${seconds} giây`
  const minutes = Math.ceil(seconds / 60)
  if (minutes < 60) return `${minutes} phút`
  const hours = Math.floor(minutes / 60)
  return minutes % 60 ? `${hours} giờ ${minutes % 60} phút` : `${hours} giờ`
}

/**
 * Screen 1.3's code step (UC-04 steps 2–4). A code is sent as the screen opens — the email's link is the
 * request for one — and "Gửi lại mã" waits as long as the backend says (MSG373, MSG379), counted down
 * from its `retryAfterSeconds`, never guessed. A wrong code (MSG126) empties the boxes and goes back to the first,
 * so the next code is typed from the start, and says how many tries are left near the end; a code locked after
 * five misses (MSG372) can only be replaced, so the resend link is what the message points to.
 */
export function GuestVerifyForm({
  invitationId,
  invitation,
  notice,
  onVerified,
  onClosed,
}: {
  invitationId: string
  invitation: GuestInvitation
  /** Why the code is asked again: the previous session ended (MSG378). */
  notice: string | null
  onVerified: () => void
  onClosed: (message: string) => void
}) {
  const toast = useToast()
  const [digits, setDigits] = useState<string[]>(() => Array(DIGITS).fill(''))
  const [error, setError] = useState<{ message: string; locked: boolean } | null>(null)
  const [pending, setPending] = useState(false)
  const [sending, setSending] = useState(false)
  /** When "Gửi lại mã" opens again, as a timestamp; the label counts down to it. */
  const [resendAt, setResendAt] = useState(0)
  const [validMinutes, setValidMinutes] = useState<number | null>(null)
  /** Opened within a minute of the last code (MSG373): that code may already have been used, or not. */
  const [recent, setRecent] = useState(false)
  const [now, setNow] = useState(() => Date.now())
  const boxes = useRef<(HTMLInputElement | null)[]>([])
  const sentOnOpen = useRef(false)

  const wait = Math.max(0, Math.ceil((resendAt - now) / 1000))
  useEffect(() => {
    if (resendAt <= Date.now()) return
    const timer = window.setInterval(() => {
      setNow(Date.now())
      if (Date.now() >= resendAt) window.clearInterval(timer)
    }, 1000)
    return () => window.clearInterval(timer)
  }, [resendAt])

  const send = useCallback(
    async (onOpen: boolean) => {
      const linkToken = readGuestEntry(invitationId).linkToken
      if (!linkToken) return
      setSending(true)
      try {
        const sent = await sendGuestCode(invitationId, linkToken)
        setNow(Date.now())
        setResendAt(Date.now() + sent.resendAfterSeconds * 1000)
        setValidMinutes(Math.round(sent.expiresInSeconds / 60))
        setRecent(false)
        if (!onOpen) {
          toast.show(sent.message)
          setDigits(Array(DIGITS).fill(''))
          setError(null)
          boxes.current[0]?.focus()
        }
      } catch (err) {
        if (isApiError(err, 404)) return onClosed(errorMessage(err))
        if (isApiError(err, 429)) {
          setNow(Date.now())
          setResendAt(Date.now() + (err.body?.retryAfterSeconds ?? 60) * 1000)
          // Opened again within the minute: no new code. The last one still works unless it was used (a guest
          // coming back in after Thoát, or after a lapsed session), so say both.
          if (onOpen && err.code === 'MSG373') return setRecent(true)
        }
        setError({ message: errorMessage(err), locked: false })
      } finally {
        setSending(false)
      }
    },
    [invitationId, toast, onClosed],
  )

  // Once per screen: a reload of the page asks again, and the backend's one-minute wait answers that.
  useEffect(() => {
    if (sentOnOpen.current) return
    sentOnOpen.current = true
    void send(true)
  }, [send])

  function fill(from: number, typed: string) {
    const incoming = typed
      .replace(/\D/g, '')
      .slice(0, DIGITS - from)
      .split('')
    if (incoming.length === 0) return
    const next = [...digits]
    incoming.forEach((d, i) => (next[from + i] = d))
    setDigits(next)
    setError(null)
    boxes.current[Math.min(from + incoming.length, DIGITS - 1)]?.focus()
  }

  function onKeyDown(index: number, event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Backspace' && !digits[index] && index > 0) {
      event.preventDefault()
      const next = [...digits]
      next[index - 1] = ''
      setDigits(next)
      boxes.current[index - 1]?.focus()
    } else if (event.key === 'ArrowLeft' && index > 0) boxes.current[index - 1]?.focus()
    else if (event.key === 'ArrowRight' && index < DIGITS - 1) boxes.current[index + 1]?.focus()
  }

  function onPaste(index: number, event: ClipboardEvent<HTMLInputElement>) {
    event.preventDefault()
    fill(index, event.clipboardData.getData('text'))
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const code = digits.join('')
    if (code.length !== DIGITS) {
      setError({ message: `Nhập đủ ${DIGITS} chữ số của mã.`, locked: false })
      boxes.current[digits.findIndex((d) => !d)]?.focus()
      return
    }
    const linkToken = readGuestEntry(invitationId).linkToken
    if (!linkToken) return
    setPending(true)
    try {
      const verified = await verifyGuestCode(invitationId, linkToken, code)
      updateGuestEntry(invitationId, { accessToken: verified.accessToken, expiresAt: verified.expiresAt })
      onVerified()
    } catch (err) {
      setPending(false)
      if (isApiError(err, 404)) return onClosed(errorMessage(err))
      const locked = isApiError(err, 400, 'MSG372')
      const left = isApiError(err, 400, 'MSG126') ? err.body?.remainingAttempts : undefined
      setError({
        message: left !== undefined && left <= WARN_FROM_REMAINING ? `${errorMessage(err)} Còn ${left} lần thử.` : errorMessage(err),
        locked,
      })
      if (!locked) {
        // As after "Gửi lại mã": the next code goes in from the first box, not onto the end of the wrong one.
        setDigits(Array(DIGITS).fill(''))
        boxes.current[0]?.focus()
      }
    }
  }

  return (
    <>
      <div className="login-head">
        <span className="login-mark">
          <BrandMark size={56} />
        </span>
        <p className="guest-caption">TrustCabinet mời bạn họp</p>
        <h1 className="guest-title">{invitation.meeting.title}</h1>
        <p className="login-sub">
          {sending && validMinutes === null ? (
            <>
              Đang gửi mã {DIGITS} số tới <strong>{invitation.maskedEmail}</strong>…
            </>
          ) : (
            <>
              Nhập mã {DIGITS} số đã gửi tới <strong>{invitation.maskedEmail}</strong> để xem cuộc họp.
            </>
          )}
        </p>
      </div>

      {notice && (
        <p className="alert alert-info" role="status">
          {notice}
        </p>
      )}

      <form onSubmit={handleSubmit} noValidate>
        <div className="field">
          <div className="otp" role="group" aria-label={`Mã xác minh ${DIGITS} số`}>
            {digits.map((digit, i) => (
              <input
                key={i}
                ref={(el) => {
                  boxes.current[i] = el
                }}
                className="otp-box"
                value={digit}
                inputMode="numeric"
                autoComplete={i === 0 ? 'one-time-code' : 'off'}
                pattern="[0-9]*"
                maxLength={i === 0 ? DIGITS : 1}
                aria-label={`Chữ số ${i + 1}`}
                aria-invalid={error !== null}
                aria-describedby="guest-code-note"
                autoFocus={i === 0}
                onFocus={(e) => e.target.select()}
                onChange={(e) => {
                  const typed = e.target.value.replace(/\D/g, '')
                  if (typed.length > 1) return fill(i, typed)
                  const next = [...digits]
                  next[i] = typed
                  setDigits(next)
                  setError(null)
                  if (typed && i < DIGITS - 1) boxes.current[i + 1]?.focus()
                }}
                onKeyDown={(e) => onKeyDown(i, e)}
                onPaste={(e) => onPaste(i, e)}
              />
            ))}
          </div>
          {error ? (
            <small id="guest-code-note" className="field-error" role="alert">
              {error.message}
            </small>
          ) : (
            <small id="guest-code-note" className="field-hint">
              {recent
                ? 'Mã đã được gửi, vui lòng nhập mã mới nhất hoặc yêu cầu gửi lại mã.'
                : validMinutes
                  ? `Mã có hiệu lực trong ${validMinutes} phút.`
                  : 'Mã chỉ dùng được một lần.'}
            </small>
          )}
        </div>

        <div className="guest-actions">
          <button className="btn btn-primary btn-cta" type="submit" disabled={pending || error?.locked}>
            <Icon name="checkCircle" size={18} />
            {pending ? 'Đang xác minh…' : 'Xác minh'}
          </button>
          <button
            type="button"
            className={error?.locked ? 'btn btn-outline' : 'btn btn-ghost'}
            disabled={sending || wait > 0}
            onClick={() => void send(false)}
          >
            <Icon name="mail" size={16} />
            {sending ? 'Đang gửi…' : wait > 0 ? `Gửi lại mã (${formatWait(wait)})` : 'Gửi lại mã'}
          </button>
        </div>
      </form>

      <p className="alert alert-warning">
        Cuộc họp có thể được ghi âm để lập biên bản. Thông tin của bạn chỉ dùng cho cuộc họp này.
      </p>
      <p className="guest-scope">Bạn chỉ xem được cuộc họp này và tài liệu của nó.</p>
    </>
  )
}
