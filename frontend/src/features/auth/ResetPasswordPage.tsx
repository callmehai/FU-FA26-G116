import { useEffect, useRef, useState, type FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { resetPassword } from '../../api/auth'
import { BrandMark } from '../../components/BrandMark'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { routes } from '../../lib/routes'

/** The backend's own minimum, as for UC-05. Checked here only to save a round trip. */
const MIN_PASSWORD_LENGTH = 12

/**
 * MSG93's wording, also shown without calling the API when the link carries no token: that is the
 * same situation — a link that cannot reset anything.
 */
const DEAD_LINK_MESSAGE = 'Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng liên hệ quản trị viên.'

type Field = 'newPassword' | 'confirmPassword'
type FieldErrors = Partial<Record<Field, string>>

/**
 * UC-08, second half: the page the reset email links to. Public — whoever forgot their password has
 * no session; the token is the proof.
 *
 * The token arrives in the fragment (`#token=…`), which the browser never sends to a server. It is
 * read once and then dropped from the address bar, so it does not linger in history or get copied
 * along with the URL.
 */
export function ResetPasswordPage() {
  const location = useLocation()
  const navigate = useNavigate()
  const toast = useToast()

  const hashToken = new URLSearchParams(location.hash.slice(1)).get('token')?.trim() || null
  const [token, setToken] = useState(hashToken)
  const [linkDead, setLinkDead] = useState(hashToken === null)

  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const newPasswordRef = useRef<HTMLInputElement>(null)
  const confirmPasswordRef = useRef<HTMLInputElement>(null)

  // A newer link pasted into this same tab only changes the fragment — no reload, no remount — so
  // pick its token up here rather than keep answering for the old one.
  if (hashToken !== null && hashToken !== token) {
    setToken(hashToken)
    setLinkDead(false)
    setFieldErrors({})
    setFormError(null)
  }

  useEffect(() => {
    if (location.hash) navigate({ pathname: location.pathname, search: location.search }, { replace: true })
  }, [location.hash, location.pathname, location.search, navigate])

  function validate(): FieldErrors {
    const errors: FieldErrors = {}
    if (newPassword.length < MIN_PASSWORD_LENGTH) {
      errors.newPassword = `Mật khẩu phải có ít nhất ${MIN_PASSWORD_LENGTH} ký tự.`
    }
    if (confirmPassword !== newPassword) errors.confirmPassword = 'Mật khẩu nhập lại không khớp.'
    return errors
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!token) return
    const errors = validate()
    setFieldErrors(errors)
    setFormError(null)
    if (Object.keys(errors).length > 0) {
      ;(errors.newPassword ? newPasswordRef : confirmPasswordRef).current?.focus()
      return
    }

    setPending(true)
    try {
      const response = await resetPassword({ token, newPassword })
      toast.show(response.message)
      // No session comes back: the user signs in with the password they just chose.
      navigate(routes.login, { replace: true })
    } catch (err) {
      setPending(false)
      if (isApiError(err, 400, 'MSG93')) {
        setLinkDead(true)
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

  // On both fields, so the two inputs share one width; either button shows or hides both.
  const visibilityToggle = (
    <button
      type="button"
      className="icon-button"
      onClick={() => setShowPassword((v) => !v)}
      title={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
      aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
      aria-pressed={showPassword}
    >
      <Icon name={showPassword ? 'eyeOff' : 'eye'} size={16} />
    </button>
  )

  return (
    <div className="login-screen">
      <div className="login-card">
        <div className="login-head">
          <BrandMark size={64} />
          <h1>Đặt lại mật khẩu</h1>
          <p className="login-sub">
            {linkDead ? 'Liên kết này không dùng được nữa.' : 'Chọn mật khẩu mới cho tài khoản của bạn.'}
          </p>
        </div>

        {linkDead ? (
          <>
            <div className="alert alert-error" role="alert">
              <p>{DEAD_LINK_MESSAGE}</p>
              <p className="alert-hint">
                Liên kết chỉ dùng được một lần trong 60 phút, và hết hiệu lực khi có liên kết mới hơn được gửi.
              </p>
            </div>
            <Link className="btn btn-outline btn-block" to={routes.login}>
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
              <div className="input-group">
                <input
                  ref={newPasswordRef}
                  id="reset-new-password"
                  type={showPassword ? 'text' : 'password'}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  aria-invalid={!!fieldErrors.newPassword}
                  aria-describedby="reset-new-password-note"
                  autoComplete="new-password"
                  spellCheck={false}
                  autoFocus
                />
                {visibilityToggle}
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
              <div className="input-group">
                <input
                  ref={confirmPasswordRef}
                  id="reset-confirm-password"
                  type={showPassword ? 'text' : 'password'}
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  aria-invalid={!!fieldErrors.confirmPassword}
                  aria-describedby={fieldErrors.confirmPassword ? 'reset-confirm-password-error' : undefined}
                  autoComplete="new-password"
                  spellCheck={false}
                />
                {visibilityToggle}
              </div>
              {fieldErrors.confirmPassword && (
                <small id="reset-confirm-password-error" className="field-error">
                  {fieldErrors.confirmPassword}
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
