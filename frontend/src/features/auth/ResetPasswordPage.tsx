import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { resetPassword } from '../../api/auth'
import { BrandMark } from '../../components/BrandMark'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { routes } from '../../lib/routes'
import { useAuth } from './useAuth'

/** The backend's own minimum, as for UC-05. Checked here only to save a round trip. */
const MIN_PASSWORD_LENGTH = 12

/**
 * MSG93's wording, shown without calling the API when the link carries no token: that is the same
 * situation — a link that cannot reset anything. A real MSG93 shows the backend's own message.
 */
const DEAD_LINK_MESSAGE = 'Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng liên hệ quản trị viên.'

type Field = 'newPassword' | 'confirmPassword'
type FieldErrors = Partial<Record<Field, string>>

function validate(newPassword: string, confirmPassword: string): FieldErrors {
  const errors: FieldErrors = {}
  if (newPassword.length < MIN_PASSWORD_LENGTH) {
    errors.newPassword = `Mật khẩu phải có ít nhất ${MIN_PASSWORD_LENGTH} ký tự.`
  }
  if (confirmPassword !== newPassword) errors.confirmPassword = 'Mật khẩu nhập lại không khớp.'
  return errors
}

/** While typing, errors only go away: keep those already shown that the new values still break. */
function stillFailing(shown: FieldErrors, now: FieldErrors): FieldErrors {
  const kept: FieldErrors = {}
  for (const field of Object.keys(shown) as Field[]) if (now[field]) kept[field] = now[field]
  return kept
}

/**
 * UC-08, second half: the page the reset email links to. Public — whoever forgot their password has
 * no session; the token is the proof.
 *
 * The token arrives in the fragment (`#token=…`), which the browser never sends to a server, nor
 * puts in `Referer`. It is read once and then dropped from the address bar and the back/forward
 * entry, so it is not copied along with the URL. The browser's own history still keeps the original
 * address — single use and the 60-minute expiry bound that.
 */
export function ResetPasswordPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const toast = useToast()
  const { status, signOut } = useAuth()

  const hashToken = new URLSearchParams(location.hash.slice(1)).get('token')?.trim() || null
  const [token, setToken] = useState(hashToken)
  /** Set once the link is known to be dead; the form gives way to this message. */
  const [deadMessage, setDeadMessage] = useState<string | null>(hashToken === null ? DEAD_LINK_MESSAGE : null)

  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showNew, setShowNew] = useState(false)
  const [showConfirm, setShowConfirm] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const newPasswordRef = useRef<HTMLInputElement>(null)
  const confirmPasswordRef = useRef<HTMLInputElement>(null)
  const loginLinkRef = useRef<HTMLAnchorElement>(null)

  // A newer link pasted into this same tab only changes the fragment — no reload, no remount — so
  // pick its token up here rather than keep answering for the old one.
  if (hashToken !== null && hashToken !== token) {
    setToken(hashToken)
    setDeadMessage(null)
    setFieldErrors({})
    setFormError(null)
  }

  useEffect(() => {
    if (location.hash) navigate({ pathname: location.pathname, search: location.search }, { replace: true })
  }, [location.hash, location.pathname, location.search, navigate])

  // The form the user was typing in has just gone: put focus on the one way forward.
  useEffect(() => {
    if (deadMessage) loginLinkRef.current?.focus()
  }, [deadMessage])

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!token) return
    const errors = validate(newPassword, confirmPassword)
    setFieldErrors(errors)
    setFormError(null)
    if (Object.keys(errors).length > 0) {
      ;(errors.newPassword ? newPasswordRef : confirmPasswordRef).current?.focus()
      return
    }

    setPending(true)
    try {
      const response = await resetPassword({ token, newPassword })
      // No session comes back: the user signs in with the password they just chose. A session this
      // browser already holds is ended first — /login would otherwise wave it through to the app,
      // under a toast asking to sign in, and if it is this account it dies at the next refresh anyway.
      if (status === 'authenticated') await signOut()
      toast.show(response.message)
      navigate(routes.login, { replace: true })
    } catch (err) {
      setPending(false)
      if (isApiError(err, 400, 'MSG93')) {
        setDeadMessage(errorMessage(err))
      } else if (isApiError(err, 400, 'MSG94')) {
        // The link is still good: keep the form and let the user fix the password.
        const message = err.body?.errors?.newPassword ?? errorMessage(err)
        setFieldErrors({ newPassword: message })
        newPasswordRef.current?.focus()
      } else {
        setFormError(errorMessage(err))
      }
    }
  }

  return (
    <div className="login-screen">
      <div className="login-card">
        <div className="login-head">
          <BrandMark size={64} />
          <h1>Đặt lại mật khẩu</h1>
          <p className="login-sub">
            {deadMessage ? 'Liên kết này không dùng được nữa.' : 'Chọn mật khẩu mới cho tài khoản của bạn.'}
          </p>
        </div>

        {deadMessage ? (
          <>
            <div className="alert alert-error" role="alert">
              <p>{deadMessage}</p>
              <p className="alert-hint">
                Liên kết chỉ dùng được một lần trong 60 phút, và hết hiệu lực khi có liên kết mới hơn được gửi.
              </p>
            </div>
            <Link ref={loginLinkRef} className="btn btn-outline btn-block" to={routes.login}>
              Về trang đăng nhập
            </Link>
          </>
        ) : (
          <form onSubmit={handleSubmit} noValidate>
            {formError && (
              <p className="alert alert-error" role="alert">
                {formError}
              </p>
            )}

            <div className="field">
              <label className="field-required" htmlFor="reset-new-password">
                Mật khẩu mới
              </label>
              <div className="input-reveal">
                <input
                  ref={newPasswordRef}
                  id="reset-new-password"
                  type={showNew ? 'text' : 'password'}
                  value={newPassword}
                  onChange={(e) => {
                    setNewPassword(e.target.value)
                    setFieldErrors((shown) => stillFailing(shown, validate(e.target.value, confirmPassword)))
                  }}
                  aria-invalid={!!fieldErrors.newPassword}
                  aria-describedby="reset-new-password-note"
                  autoComplete="new-password"
                  spellCheck={false}
                  autoFocus
                />
                <RevealToggle shown={showNew} onToggle={() => setShowNew((v) => !v)} />
              </div>
              {fieldErrors.newPassword ? (
                <small id="reset-new-password-note" className="field-error">
                  {fieldErrors.newPassword}
                </small>
              ) : (
                <small id="reset-new-password-note" className="field-hint">
                  Tối thiểu {MIN_PASSWORD_LENGTH} ký tự.
                </small>
              )}
            </div>

            <div className="field">
              <label className="field-required" htmlFor="reset-confirm-password">
                Nhập lại mật khẩu mới
              </label>
              <div className="input-reveal">
                <input
                  ref={confirmPasswordRef}
                  id="reset-confirm-password"
                  type={showConfirm ? 'text' : 'password'}
                  value={confirmPassword}
                  onChange={(e) => {
                    setConfirmPassword(e.target.value)
                    setFieldErrors((shown) => stillFailing(shown, validate(newPassword, e.target.value)))
                  }}
                  aria-invalid={!!fieldErrors.confirmPassword}
                  aria-describedby="reset-confirm-password-note"
                  autoComplete="new-password"
                  spellCheck={false}
                />
                <RevealToggle shown={showConfirm} onToggle={() => setShowConfirm((v) => !v)} />
              </div>
              {fieldErrors.confirmPassword ? (
                <small id="reset-confirm-password-note" className="field-error">
                  {fieldErrors.confirmPassword}
                </small>
              ) : (
                <small id="reset-confirm-password-note" className="field-hint">
                  Nhập lại đúng mật khẩu ở trên.
                </small>
              )}
            </div>

            <button className="btn btn-primary btn-block" type="submit" disabled={pending}>
              {pending ? 'Đang đặt lại…' : 'Đặt mật khẩu mới'}
            </button>
            <p className="login-hint">Đặt lại xong, mọi thiết bị đang đăng nhập tài khoản này sẽ bị đăng xuất.</p>
          </form>
        )}
      </div>
    </div>
  )
}

/** The show/hide button inside a password box (`.input-reveal`). */
function RevealToggle({ shown, onToggle }: { shown: boolean; onToggle: () => void }) {
  const label = shown ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'
  return (
    <button
      type="button"
      className="icon-button"
      onClick={onToggle}
      title={label}
      aria-label={label}
      aria-pressed={shown}
    >
      <Icon name={shown ? 'eyeOff' : 'eye'} size={16} />
    </button>
  )
}
