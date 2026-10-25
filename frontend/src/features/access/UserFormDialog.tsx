import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { createUser, getUser, updateUser } from '../../api/users'
import { Dialog } from '../../components/Dialog'
import { Icon } from '../../components/Icon'
import { Select } from '../../components/Select'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { generatePassword } from '../../lib/password'
import type { RoleRecord } from '../../types/role'
import type { UserAccount, UserMutationResponse } from '../../types/user'

/** The backend's own minimum. Checked here only to save a round trip; the backend checks again. */
const MIN_PASSWORD_LENGTH = 12
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

type Field = 'fullName' | 'email' | 'password' | 'roleId'
type FieldErrors = Partial<Record<Field, string>>

export type UserFormTarget = { mode: 'create' } | { mode: 'edit'; id: string }

/**
 * UC-06a (create) and UC-06b (edit) in one form.
 *
 * Editing starts with GET /api/users/{id} — UC-06b's first half — rather than trusting the list
 * row, which another administrator may have changed since it loaded. There is no password on the
 * edit form (resetting one is UC-07) and no status (that is UC-06c, with its own rules).
 */
export function UserFormDialog({
  target,
  roles,
  currentUserId,
  onSaved,
  onGone,
  onAccessLost,
  onClose,
}: {
  target: UserFormTarget
  roles: RoleRecord[]
  currentUserId: string | undefined
  onSaved: (response: UserMutationResponse) => void
  /** The account no longer exists (404). */
  onGone: () => void
  /** Returns true when it has handled the error as a mid-session 403. */
  onAccessLost: (error: unknown) => boolean
  onClose: () => void
}) {
  const editing = target.mode === 'edit' ? target.id : null
  const toast = useToast()

  const [original, setOriginal] = useState<UserAccount | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [roleId, setRoleId] = useState(
    roles.find((role) => role.isDefault)?.id ?? '',
  )

  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  const fill = useCallback((account: UserAccount) => {
    setOriginal(account)
    setFullName(account.fullName)
    setEmail(account.email)
    setRoleId(account.roleId)
  }, [])

  const fail = useCallback(
    (err: unknown) => {
      if (onAccessLost(err)) return
      if (isApiError(err, 404)) return onGone()
      setLoadError(errorMessage(err))
    },
    [onAccessLost, onGone],
  )

  useEffect(() => {
    if (!editing) return
    let cancelled = false
    getUser(editing).then(
      (account) => {
        if (!cancelled) fill(account)
      },
      (err: unknown) => {
        if (!cancelled) fail(err)
      },
    )
    // StrictMode runs effects twice in dev; only the surviving run may write state.
    return () => {
      cancelled = true
    }
  }, [editing, fill, fail])

  function retryLoad() {
    if (!editing) return
    setLoadError(null)
    getUser(editing).then(fill, fail)
  }

  /** A field's error, the server's included (MSG08, MSG09), goes as soon as that field is edited. */
  function clearError(field: Field) {
    setFieldErrors(({ [field]: _cleared, ...rest }) => rest)
  }

  /** Shown straight away: the administrator has to pass it on, so they need to read it. */
  function fillGeneratedPassword() {
    setPassword(generatePassword())
    setShowPassword(true)
    clearError('password')
  }

  async function copyPassword() {
    try {
      await navigator.clipboard.writeText(password)
      toast.show('Đã sao chép mật khẩu.')
    } catch {
      toast.show('Không sao chép được. Hãy chọn mật khẩu và sao chép thủ công.', 'error')
    }
  }

  function validate(): FieldErrors {
    const errors: FieldErrors = {}
    if (!fullName.trim()) errors.fullName = 'Họ tên không được để trống.'
    if (!EMAIL_PATTERN.test(email.trim())) errors.email = 'Email không hợp lệ.'
    if (!editing && password.length < MIN_PASSWORD_LENGTH) {
      errors.password = `Mật khẩu phải có ít nhất ${MIN_PASSWORD_LENGTH} ký tự.`
    }
    if (!roles.some((role) => role.id === roleId)) errors.roleId = 'Vui lòng chọn vai trò.'
    return errors
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const errors = validate()
    setFieldErrors(errors)
    setFormError(null)
    if (Object.keys(errors).length > 0) return

    setPending(true)
    try {
      const body = { fullName: fullName.trim(), email: email.trim(), roleId }
      onSaved(editing ? await updateUser(editing, body) : await createUser({ ...body, password }))
    } catch (err) {
      setPending(false)
      if (onAccessLost(err)) return
      if (isApiError(err, 404)) return onGone()

      if (isApiError(err, 400, 'MSG09')) {
        // Every invalid field at once, keyed by the request's own field names.
        const serverErrors = (err.body?.errors ?? {}) as FieldErrors
        setFieldErrors(serverErrors)
        if (Object.keys(serverErrors).length === 0) setFormError(errorMessage(err))
      } else if (isApiError(err, 409, 'MSG08')) {
        setFieldErrors({ email: errorMessage(err) })
      } else if (isApiError(err, 409, 'MSG12')) {
        // On this endpoint MSG12 has one cause: the role change would leave nobody holding AdminUsers.
        setFormError('Không thể đổi vai trò: sau thao tác này sẽ không còn tài khoản quản trị nào đang hoạt động.')
      } else {
        setFormError(errorMessage(err))
      }
    }
  }

  const close = () => {
    if (!pending) onClose()
  }

  // The backend ends the account's sessions on an email change (UserService.UpdateAsync).
  const emailChanged = original !== null && email.trim() !== original.email
  const isSelf = editing !== null && editing === currentUserId
  const formId = 'user-form'

  return (
    <Dialog
      title={editing ? 'Sửa tài khoản' : 'Tạo tài khoản'}
      description={
        editing ? (
          original && (
            <>
              Chỉnh sửa thông tin của <b>{original.fullName}</b>.
            </>
          )
        ) : (
          'Tài khoản mới ở trạng thái hoạt động và đăng nhập được ngay bằng email và mật khẩu này. Nếu email là tài khoản Google, người dùng cũng đăng nhập được bằng Google.'
        )
      }
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-outline" onClick={close} disabled={pending}>
            Huỷ
          </button>
          <button
            type="submit"
            form={formId}
            className="btn btn-primary"
            disabled={pending || (editing !== null && original === null)}
          >
            <Icon name={editing ? 'check' : 'userPlus'} size={16} />
            {pending ? 'Đang lưu…' : editing ? 'Lưu thay đổi' : 'Tạo tài khoản'}
          </button>
        </>
      }
    >
      {editing && !original ? (
        loadError ? (
          <div className="alert alert-error" role="alert">
            <p>{loadError}</p>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={retryLoad}
            >
              <Icon name="refresh" size={16} />
              Thử lại
            </button>
          </div>
        ) : (
          <p className="cell-muted">Đang tải thông tin tài khoản…</p>
        )
      ) : (
        <form id={formId} className="form-grid" onSubmit={handleSubmit} noValidate>
          {formError && (
            <p className="alert alert-error span-2" role="alert">
              {formError}
            </p>
          )}

          <label className="field span-2">
            <span className="field-required">Họ tên</span>
            <input
              value={fullName}
              onChange={(e) => {
                setFullName(e.target.value)
                clearError('fullName')
              }}
              aria-invalid={!!fieldErrors.fullName}
              autoFocus
            />
            {fieldErrors.fullName && <small className="field-error">{fieldErrors.fullName}</small>}
          </label>

          <label className="field span-2">
            <span className="field-required">Email</span>
            <input
              type="email"
              value={email}
              onChange={(e) => {
                setEmail(e.target.value)
                clearError('email')
              }}
              aria-invalid={!!fieldErrors.email}
              autoComplete="off"
            />
            {fieldErrors.email && <small className="field-error">{fieldErrors.email}</small>}
          </label>

          {!editing && (
            <div className="field span-2">
              <label className="field-required" htmlFor="user-form-password">
                Mật khẩu
              </label>
              <div className="input-group">
                <input
                  id="user-form-password"
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => {
                    setPassword(e.target.value)
                    clearError('password')
                  }}
                  aria-invalid={!!fieldErrors.password}
                  autoComplete="new-password"
                  spellCheck={false}
                />
                <button
                  type="button"
                  className="icon-button"
                  onClick={() => setShowPassword((v) => !v)}
                  data-tooltip={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                  aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
                >
                  <Icon name={showPassword ? 'eyeOff' : 'eye'} size={16} />
                </button>
                <button
                  type="button"
                  className="icon-button"
                  onClick={copyPassword}
                  disabled={!password}
                  data-tooltip="Sao chép mật khẩu"
                  aria-label="Sao chép mật khẩu"
                >
                  <Icon name="copy" size={16} />
                </button>
                <button type="button" className="btn btn-outline" onClick={fillGeneratedPassword}>
                  <Icon name="refresh" size={16} />
                  Tạo mật khẩu
                </button>
              </div>
              {fieldErrors.password ? (
                <small className="field-error">{fieldErrors.password}</small>
              ) : (
                <small className="field-hint">
                  Tối thiểu {MIN_PASSWORD_LENGTH} ký tự. Gửi mật khẩu cho người dùng qua kênh riêng, không qua nhóm
                  chat chung.
                </small>
              )}
            </div>
          )}

          <div className="field span-2">
            <span className="field-required">Vai trò</span>
            <Select
              value={roleId}
              options={roles.map(({ id, name }) => ({ value: id, label: name }))}
              onChange={(value) => {
                setRoleId(value)
                clearError('roleId')
              }}
              label="Vai trò"
            />
            {fieldErrors.roleId && <small className="field-error">{fieldErrors.roleId}</small>}
          </div>

          {original && (
            <p className="field-hint span-2">
              Đăng nhập bằng: {signInMethods(original)}.{' '}
              {original.hasGoogleLink
                ? 'Đổi email không đổi tài khoản Google đã liên kết.'
                : 'Lần đầu người này đăng nhập bằng Google với đúng email trên, tài khoản sẽ tự liên kết.'}{' '}
              Đổi mật khẩu không thực hiện ở đây.
            </p>
          )}

          {/* A role change keeps the session and applies at once (SCRUM-105); only an email change signs out. */}
          {emailChanged && (
            <p className="alert alert-warning span-2">
              {isSelf
                ? 'Bạn đang đổi email của chính mình. Sau khi lưu, bạn sẽ bị đăng xuất và cần đăng nhập lại.'
                : 'Đổi email sẽ đăng xuất người này khỏi mọi thiết bị.'}
            </p>
          )}
        </form>
      )}
    </Dialog>
  )
}

function signInMethods(account: Pick<UserAccount, 'hasPassword' | 'hasGoogleLink'>) {
  const methods = [account.hasPassword && 'mật khẩu', account.hasGoogleLink && 'Google'].filter(Boolean)
  return methods.length > 0 ? methods.join(', ') : 'chưa có'
}
