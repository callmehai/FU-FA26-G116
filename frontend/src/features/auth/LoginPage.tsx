import { useState, type FormEvent } from 'react'
import { Navigate, useLocation, useNavigate } from 'react-router-dom'
import { BrandMark } from '../../components/BrandMark'
import { Select } from '../../components/Select'
import { routes } from '../../lib/routes'
import { ROLE_LABEL, type Role } from '../../types/user'

const ROLE_OPTIONS = (Object.keys(ROLE_LABEL) as Role[]).map((value) => ({
  value,
  label: ROLE_LABEL[value],
}))

/** Stand-in identity when the form is submitted empty. */
const DEMO_EMAIL = 'demo@trustcabinet.local'
import { useAuth } from './useAuth'

/**
 * Sign-in screen.
 *
 * TEMPORARY: there is no user table yet, so the form does not validate anything — submitting it
 * empty signs you in as a demo account. The role picker lets the team exercise RBAC before the
 * backend issues tokens; both it and this whole shortcut go away once `/api/auth/login` exists.
 */
export function LoginPage() {
  const { user, signIn } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState<Role>('secretary')

  // Already signed in — go straight through instead of showing the form again.
  if (user) return <Navigate to={routes.meetings} replace />

  const from = (location.state as { from?: { pathname: string } } | null)?.from?.pathname

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    // No backend to check against, so an empty form is still a valid way in.
    signIn(email.trim() || DEMO_EMAIL, role)
    navigate(from ?? routes.meetings, { replace: true })
  }

  return (
    <div className="login-screen">
      <div className="login-card">
        <BrandMark size={56} />
        <h1>TrustCabinet</h1>
        <p className="login-sub">Đăng nhập hệ thống phòng họp không giấy</p>

        <form onSubmit={handleSubmit}>
          <label className="field">
            <span>Tên đăng nhập</span>
            <input
              type="email"
              name="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="username"
            />
          </label>

          <label className="field">
            <span>Mật khẩu</span>
            <input
              type="password"
              name="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
            />
          </label>

          <div className="field">
            <span>Vai trò (tạm thời)</span>
            <Select value={role} options={ROLE_OPTIONS} onChange={setRole} label="Vai trò" />
          </div>

          <button className="btn btn-primary btn-block" type="submit">
            Đăng nhập bằng mật khẩu
          </button>
        </form>

        <p className="login-warning">
          Chưa có cơ sở dữ liệu nên đăng nhập đang được giả lập: bấm nút là vào, bỏ trống cũng
          được. Ô vai trò để thử phân quyền trước khi backend có xác thực thật.
        </p>
      </div>
    </div>
  )
}
