import { useRef, useState, type FormEvent } from 'react'
import { matchPath, Navigate, useLocation } from 'react-router-dom'
import { BrandMark } from '../../components/BrandMark'
import { errorMessage, isApiError } from '../../lib/errors'
import { JUST_SIGNED_IN, routes } from '../../lib/routes'
import { GoogleSignInButton } from './GoogleSignInButton'
import { useAuth } from './useAuth'
import { Icon } from '../../components/Icon'
import { RevealToggle } from '../../components/RevealToggle'

interface LoginError {
  message: string
  hint?: string
}

/**
 * UC-02. Kept apart from the password flow, which just shows the backend's message for its 401
 * codes: MSG05 is Google-only and means "no account for this Google identity". A deactivated
 * account is MSG02, the same as on /login.
 *
 * The first Google sign-in links itself to the active account with the same verified email
 * (ADR-0009), so MSG05 means no such account exists — or that one is linked to another Google
 * identity. Either way only an administrator can fix it.
 */
function googleSignInError(error: unknown): LoginError {
  // The backend answers 404 when Google sign-in is switched off in its configuration.
  if (isApiError(error, 404)) return { message: 'Đăng nhập bằng Google chưa được bật trên hệ thống này.' }
  if (isApiError(error, 401, 'MSG05')) {
    return {
      message: errorMessage(error),
      hint: 'Hãy chọn đúng tài khoản Google có email đã đăng ký với hệ thống, hoặc liên hệ quản trị viên để được cấp tài khoản.',
    }
  }
  return { message: errorMessage(error) }
}

/** A shape check only, to save a round trip; the backend decides whether the account exists. */
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

type FieldErrors = { email?: string; password?: string }

/** UC-02's outer branch: Google itself failed, so there is no token and nothing reached the backend. */
const GOOGLE_FAILED: LoginError = { message: 'Đăng nhập bằng Google không thành công.' }

/**
 * Sign-in screen: email and password (UC-01) or Google (UC-02). Which role the user has is the
 * backend's answer, never something picked here.
 */
export function LoginPage() {
  const { status, sessionExpired, signIn, signInWithGoogle } = useAuth()
  const location = useLocation()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [pending, setPending] = useState<'password' | 'google' | null>(null)
  const [error, setError] = useState<LoginError | null>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const emailRef = useRef<HTMLInputElement>(null)
  const passwordRef = useRef<HTMLInputElement>(null)

  const from = (location.state as { from?: { pathname: string; search?: string } } | null)?.from
  // Arriving from a meeting link — the invitation email's, typically (SCRUM-15). Saying so tells the
  // user the link worked; it names no meeting, so nothing leaks before sign-in.
  const toMeeting = from !== undefined && matchPath(routes.meeting(':meetingId'), from.pathname) !== null

  if (status === 'loading') return <div className="route-fallback">Đang khôi phục phiên đăng nhập…</div>

  // Signed in — including right after a successful submit — go where the user was headed, else the
  // calendar. `from` may be the previous account's page, so the flag lets NotFound send this one to
  // the calendar instead when it cannot open it (SCRUM-66).
  if (status === 'authenticated') {
    return (
      <Navigate
        to={from ? `${from.pathname}${from.search ?? ''}` : routes.calendar}
        replace
        state={{ [JUST_SIGNED_IN]: true }}
      />
    )
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    // Under each field, the first one focused — as on the reset-password page (frontend-ui-design §2).
    const errors: FieldErrors = {}
    if (!email.trim()) errors.email = 'Vui lòng nhập email.'
    else if (!EMAIL_PATTERN.test(email.trim())) errors.email = 'Email không hợp lệ.'
    if (!password) errors.password = 'Vui lòng nhập mật khẩu.'
    setFieldErrors(errors)
    if (errors.email || errors.password) {
      setError(null)
      ;(errors.email ? emailRef : passwordRef).current?.focus()
      return
    }

    setPending('password')
    setError(null)
    try {
      await signIn(email.trim(), password)
    } catch (err) {
      setError({ message: errorMessage(err) })
      setPending(null)
    }
  }

  async function handleGoogleCredential(idToken: string) {
    setPending('google')
    setError(null)
    try {
      await signInWithGoogle(idToken)
    } catch (err) {
      setError(googleSignInError(err))
      setPending(null)
    }
  }

  return (
    <div className="login-screen">
      <div className="login-card">
        <div className="login-head">
          <span className="login-mark">
            <BrandMark size={72} />
          </span>
          <h1>TrustCabinet</h1>
          <p className="login-sub">Đăng nhập hệ thống phòng họp không giấy</p>
        </div>

        {error ? (
          <div className="alert alert-error login-alert" role="alert">
            <p>{error.message}</p>
            {error.hint && <p className="alert-hint">{error.hint}</p>}
          </div>
        ) : sessionExpired ? (
          <p className="alert alert-info login-alert" role="status">
            Phiên đăng nhập đã kết thúc. Vui lòng đăng nhập lại.
          </p>
        ) : (
          toMeeting && (
            <p className="alert alert-info login-alert" role="status">
              Đăng nhập để mở cuộc họp.
            </p>
          )
        )}

        <form onSubmit={handleSubmit} noValidate>
          <label className="field">
            <span>Email</span>
            <input
              ref={emailRef}
              type="email"
              name="email"
              value={email}
              onChange={(e) => {
                setEmail(e.target.value)
                setFieldErrors(({ email: _cleared, ...rest }) => rest)
              }}
              aria-invalid={!!fieldErrors.email}
              aria-describedby={fieldErrors.email ? 'login-email-error' : undefined}
              autoComplete="username"
              autoFocus
            />
            {fieldErrors.email && (
              <small id="login-email-error" className="field-error">
                {fieldErrors.email}
              </small>
            )}
          </label>

          {/* Not a wrapping <label>: the reveal button inside would become part of its label. */}
          <div className="field">
            <label htmlFor="login-password">Mật khẩu</label>
            <div className="input-reveal">
              <input
                ref={passwordRef}
                id="login-password"
                type={showPassword ? 'text' : 'password'}
                name="password"
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value)
                  setFieldErrors(({ password: _cleared, ...rest }) => rest)
                }}
                aria-invalid={!!fieldErrors.password}
                aria-describedby={fieldErrors.password ? 'login-password-error' : undefined}
                autoComplete="current-password"
                spellCheck={false}
              />
              <RevealToggle shown={showPassword} onToggle={() => setShowPassword((v) => !v)} />
            </div>
            {fieldErrors.password && (
              <small id="login-password-error" className="field-error">
                {fieldErrors.password}
              </small>
            )}
          </div>

          <button className="btn btn-primary btn-block btn-cta" type="submit" disabled={pending !== null}>
            <Icon name="logIn" size={18} />
            {pending === 'password' ? 'Đang đăng nhập…' : 'Đăng nhập bằng mật khẩu'}
          </button>
        </form>

        <p className="login-divider">hoặc</p>

        <GoogleSignInButton
          onCredential={handleGoogleCredential}
          onError={() => setError(GOOGLE_FAILED)}
          disabled={pending !== null}
        />
        {pending === 'google' && <p className="login-hint">Đang đăng nhập bằng Google…</p>}
      </div>
    </div>
  )
}
