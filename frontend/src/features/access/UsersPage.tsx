import { useCallback, useEffect, useMemo, useState } from 'react'
import { listRoles } from '../../api/roles'
import { changeUserStatus, getUser, listUsers, requestPasswordReset } from '../../api/users'
import { Badge } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { Select } from '../../components/Select'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { formatDate } from '../../lib/format'
import type { RoleRecord } from '../../types/role'
import type { UserAccount, UserMutationResponse } from '../../types/user'
import { useAccessLost } from '../auth/useAccessLost'
import { useAuth } from '../auth/useAuth'
import { UserFormDialog, type UserFormTarget } from './UserFormDialog'

type RoleFilter = string | 'all'
type StatusFilter = 'all' | 'active' | 'inactive'

const STATUS_FILTER_OPTIONS: { value: StatusFilter; label: string }[] = [
  { value: 'all', label: 'Mọi trạng thái' },
  { value: 'active', label: 'Đang hoạt động' },
  { value: 'inactive', label: 'Đã vô hiệu hoá' },
]

const DEACTIVATED_RESET_MESSAGE =
  'Tài khoản này đã bị vô hiệu hoá nên không đặt lại mật khẩu được. Danh sách đã được cập nhật.'

/** Lower-case with diacritics stripped, so "nguyen" finds "Nguyễn" and "dung" finds "Dũng". */
function fold(text: string) {
  return text.normalize('NFD').replace(/\p{M}/gu, '').replace(/đ/gi, 'd').toLowerCase()
}

/**
 * User management (SCRUM-13): list (UC-04), create (UC-05), edit (UC-06), deactivate and
 * reactivate (UC-07), and emailing a password reset link (UC-08). Reaching this screen needs
 * `AdminUsers`; the backend checks it again on every request, so a 403 halfway through is handled
 * as lost access rather than as an error.
 */
export function UsersPage() {
  const { user: me, reloadSession } = useAuth()
  const toast = useToast()
  const handleAccessLost = useAccessLost()

  const [users, setUsers] = useState<UserAccount[] | null>(null)
  const [roles, setRoles] = useState<RoleRecord[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [query, setQuery] = useState('')
  const [roleFilter, setRoleFilter] = useState<RoleFilter>('all')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all')

  const [formTarget, setFormTarget] = useState<UserFormTarget | null>(null)
  const [statusTarget, setStatusTarget] = useState<UserAccount | null>(null)
  const [resetTarget, setResetTarget] = useState<UserAccount | null>(null)

  const load = useCallback(async () => {
    try {
      const [accounts, availableRoles] = await Promise.all([listUsers(), listRoles()])
      setUsers(accounts)
      setRoles(availableRoles)
      setLoadError(null)
    } catch (err) {
      if (!handleAccessLost(err)) setLoadError(errorMessage(err))
    }
  }, [handleAccessLost])

  useEffect(() => {
    // Let load own the request and defer its state updates past the effect's synchronous phase.
    void Promise.resolve().then(load)
  }, [load])

  async function openUserForm(target: UserFormTarget) {
    try {
      setRoles(await listRoles())
      setFormTarget(target)
    } catch (error) {
      if (!handleAccessLost(error)) toast.show(errorMessage(error), 'error')
    }
  }

  function retry() {
    setLoadError(null)
    setUsers(null)
    void load()
  }

  const patch = useCallback(
    (account: UserAccount) => setUsers((all) => all?.map((u) => (u.id === account.id ? account : u)) ?? all),
    [],
  )

  const visible = useMemo(() => {
    const needle = fold(query.trim())
    // Filter only — the backend already sorted with PostgreSQL's Vietnamese collation, which a
    // JavaScript sort would undo ("Đ" would land after "Z").
    return (users ?? []).filter(
      (u) =>
        (roleFilter === 'all' || u.roleId === roleFilter) &&
        (statusFilter === 'all' || u.isActive === (statusFilter === 'active')) &&
        (!needle || fold(u.fullName).includes(needle) || fold(u.email).includes(needle)),
    )
  }, [users, query, roleFilter, statusFilter])

  const handleGone = useCallback(() => {
    setFormTarget(null)
    toast.show('Tài khoản này không còn tồn tại. Danh sách đã được tải lại.', 'error')
    void load()
  }, [toast, load])

  function handleSaved(response: UserMutationResponse) {
    const created = formTarget?.mode === 'create'
    setFormTarget(null)
    toast.show(response.message)

    if (created) {
      // Reload rather than insert: only the database knows where the new name sorts.
      void load()
    } else {
      patch(response.user)
      // Editing yourself: re-read name, email and capabilities from /api/auth/me so the shell and
      // the sidebar match what the backend now enforces. A new role or email has revoked this
      // session on the backend; the reload notices and ends it here too, rather than minutes later.
      if (response.user.id === me?.id) void reloadSession()
    }
  }

  /**
   * UC-07. MSG12 has three causes and one code; the backend leaves telling them apart to us.
   * Targeting yourself cannot happen — the button is disabled — so re-read the account: if it is
   * already in the requested state the list was stale, otherwise the change would have left no
   * active administrator.
   */
  async function changeStatus(target: UserAccount) {
    const activate = !target.isActive
    try {
      const response = await changeUserStatus(target.id, activate)
      patch(response.user)
      toast.show(response.message)
    } catch (err) {
      if (handleAccessLost(err)) return
      if (isApiError(err, 404)) {
        void load()
        throw new Error('Tài khoản này không còn tồn tại. Danh sách đã được tải lại.')
      }
      if (!isApiError(err, 409, 'MSG12')) throw err

      let fresh: UserAccount
      try {
        fresh = await getUser(target.id)
      } catch (lookupErr) {
        if (handleAccessLost(lookupErr)) return
        // Cannot tell the causes apart without the account: say what the backend said.
        throw err
      }
      patch(fresh)
      if (fresh.isActive === activate) {
        throw new Error(
          activate
            ? 'Tài khoản này đã được kích hoạt từ trước. Danh sách đã được cập nhật.'
            : 'Tài khoản này đã bị vô hiệu hoá từ trước. Danh sách đã được cập nhật.',
        )
      }
      throw new Error(
        activate
          ? errorMessage(err)
          : 'Không thể vô hiệu hoá: sau thao tác này sẽ không còn tài khoản quản trị nào đang hoạt động.',
      )
    }
  }

  /**
   * UC-08. Errors stay in the dialog, so the administrator can retry MSG15 from where they are.
   * MSG14 means the list was stale — someone deactivated the account meanwhile — so re-read it;
   * the dialog's blocker then keeps the confirm button disabled.
   */
  async function sendPasswordReset(target: UserAccount) {
    try {
      const response = await requestPasswordReset(target.id)
      patch(response.user)
      toast.show(response.message)
    } catch (err) {
      if (handleAccessLost(err)) return
      if (isApiError(err, 404)) {
        void load()
        throw new Error('Tài khoản này không còn tồn tại. Danh sách đã được tải lại.')
      }
      if (!isApiError(err, 409, 'MSG14')) throw err

      let fresh: UserAccount
      try {
        fresh = await getUser(target.id)
      } catch (lookupErr) {
        if (handleAccessLost(lookupErr)) return
        throw err
      }
      patch(fresh)
      if (fresh.isActive) throw err
      throw new Error(DEACTIVATED_RESET_MESSAGE)
    }
  }

  // The row as the list now has it: a reload or a MSG14 re-read may have changed it under the dialog.
  const resetRow = resetTarget && users?.find((u) => u.id === resetTarget.id)

  const activeCount = users?.filter((u) => u.isActive).length ?? 0
  const roleFilterOptions: { value: RoleFilter; label: string }[] = [
    { value: 'all', label: 'Tất cả vai trò' },
    ...(roles ?? []).map(({ id, name }) => ({ value: id, label: name })),
  ]

  return (
    <>
      <PageHeader
        title="Người dùng & Quyền"
        description={
          users
            ? `Quản lý người dùng và phân quyền hệ thống · ${users.length} tài khoản, ${activeCount} đang hoạt động.`
            : 'Quản lý người dùng và phân quyền hệ thống.'
        }
      />

      <div className="toolbar">
        <label className="search">
          <Icon name="search" size={16} />
          <input
            type="search"
            placeholder="Tìm theo tên hoặc email…"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            aria-label="Tìm theo họ tên hoặc email"
          />
        </label>
        <Select value={roleFilter} options={roleFilterOptions} onChange={setRoleFilter} label="Lọc theo vai trò" />
        <Select
          value={statusFilter}
          options={STATUS_FILTER_OPTIONS}
          onChange={setStatusFilter}
          label="Lọc theo trạng thái"
        />
        <button
          className="btn btn-primary toolbar-end"
          type="button"
          onClick={() => void openUserForm({ mode: 'create' })}
          disabled={roles === null}
        >
          Tạo tài khoản
        </button>
      </div>

      <div className="table-card table-fill">
        <table className="data-table">
          <thead>
            <tr>
              <th>Họ tên</th>
              <th>Email</th>
              <th>Vai trò</th>
              <th>Đăng nhập bằng</th>
              <th>Trạng thái</th>
              <th>Ngày tạo</th>
              <th className="col-action">Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {loadError ? (
              <tr>
                <td colSpan={7} className="cell-empty">
                  <p className="cell-error">{loadError}</p>
                  <button type="button" className="btn btn-outline btn-sm" onClick={retry}>
                    Thử lại
                  </button>
                </td>
              </tr>
            ) : users === null ? (
              <tr>
                <td colSpan={7} className="cell-empty">
                  Đang tải danh sách tài khoản…
                </td>
              </tr>
            ) : visible.length === 0 ? (
              <tr>
                <td colSpan={7} className="cell-empty">
                  {users.length === 0 ? 'Chưa có tài khoản nào.' : 'Không có tài khoản nào khớp bộ lọc.'}
                </td>
              </tr>
            ) : (
              visible.map((u) => {
                const isSelf = u.id === me?.id
                return (
                  <tr key={u.id} className={u.isActive ? undefined : 'row-inactive'}>
                    <td className="cell-title">
                      {u.fullName}
                      {isSelf && (
                        <>
                          {' '}
                          <Badge tone="blue">Bạn</Badge>
                        </>
                      )}
                    </td>
                    <td className="cell-muted">{u.email}</td>
                    <td>
                      <Badge tone="slate">{u.roleName}</Badge>
                    </td>
                    <td>
                      <span className="chip-row">
                        {u.hasPassword && <Badge tone="outline">Mật khẩu</Badge>}
                        {u.hasGoogleLink && <Badge tone="outline">Google</Badge>}
                      </span>
                    </td>
                    <td>{u.isActive ? 'Đang hoạt động' : 'Đã vô hiệu hoá'}</td>
                    <td className="cell-muted">{formatDate(u.createdAt)}</td>
                    <td className="col-action">
                      <span className="row-actions">
                        <button
                          type="button"
                          className="icon-button"
                          onClick={() => void openUserForm({ mode: 'edit', id: u.id })}
                          title="Sửa hồ sơ"
                          aria-label={`Sửa tài khoản ${u.fullName}`}
                        >
                          <Icon name="pencil" size={16} />
                        </button>
                        <button
                          type="button"
                          className="icon-button"
                          onClick={() => setStatusTarget(u)}
                          disabled={isSelf}
                          title={
                            isSelf
                              ? 'Không thể tự đổi trạng thái tài khoản của mình'
                              : u.isActive
                                ? 'Vô hiệu hoá'
                                : 'Kích hoạt lại'
                          }
                          aria-label={`${u.isActive ? 'Vô hiệu hoá' : 'Kích hoạt lại'} tài khoản ${u.fullName}`}
                        >
                          <Icon name={u.isActive ? 'lock' : 'unlock'} size={16} />
                        </button>
                        <button
                          type="button"
                          className="icon-button"
                          onClick={() => setResetTarget(u)}
                          disabled={!u.isActive}
                          title={
                            u.isActive
                              ? 'Gửi liên kết đặt lại mật khẩu'
                              : 'Tài khoản đã bị vô hiệu hoá — kích hoạt lại trước khi đặt lại mật khẩu'
                          }
                          aria-label={`Gửi liên kết đặt lại mật khẩu cho ${u.fullName}`}
                        >
                          <Icon name="key" size={16} />
                        </button>
                      </span>
                    </td>
                  </tr>
                )
              })
            )}
          </tbody>
        </table>
      </div>

      {formTarget && (
        <UserFormDialog
          target={formTarget}
          roles={roles ?? []}
          currentUserId={me?.id}
          onSaved={handleSaved}
          onGone={handleGone}
          onAccessLost={handleAccessLost}
          onClose={() => setFormTarget(null)}
        />
      )}

      {statusTarget &&
        (statusTarget.isActive ? (
          <ConfirmDialog
            title="Vô hiệu hoá tài khoản"
            tone="danger"
            confirmLabel="Vô hiệu hoá"
            pendingLabel="Đang vô hiệu hoá…"
            onConfirm={() => changeStatus(statusTarget)}
            onClose={() => setStatusTarget(null)}
          >
            Vô hiệu hoá tài khoản của <b>{statusTarget.fullName}</b>? Người này bị đăng xuất khỏi mọi thiết bị
            và không đăng nhập được cho tới khi được kích hoạt lại. Lịch sử cuộc họp của họ vẫn được giữ
            nguyên.
          </ConfirmDialog>
        ) : (
          <ConfirmDialog
            title="Kích hoạt lại tài khoản"
            confirmLabel="Kích hoạt lại"
            pendingLabel="Đang kích hoạt…"
            onConfirm={() => changeStatus(statusTarget)}
            onClose={() => setStatusTarget(null)}
          >
            Kích hoạt lại tài khoản của <b>{statusTarget.fullName}</b>? Người này sẽ đăng nhập lại được với
            vai trò hiện có.
          </ConfirmDialog>
        ))}

      {resetTarget && (
        <ConfirmDialog
          title="Đặt lại mật khẩu"
          confirmLabel="Gửi liên kết"
          pendingLabel="Đang gửi email…"
          blocker={
            resetRow === undefined
              ? 'Tài khoản này không còn tồn tại.'
              : resetRow?.isActive === false
                ? DEACTIVATED_RESET_MESSAGE
                : undefined
          }
          onConfirm={() => sendPasswordReset(resetTarget)}
          onClose={() => setResetTarget(null)}
        >
          Gửi liên kết đặt lại mật khẩu tới <b>{resetTarget.email}</b>? Liên kết dùng được một lần trong 60 phút; liên
          kết gửi trước đó (nếu có) sẽ hết hiệu lực. Mật khẩu hiện tại vẫn dùng được cho tới khi {resetTarget.fullName}{' '}
          đặt mật khẩu mới.
          {resetTarget.id === me?.id &&
            ' Đây là tài khoản của bạn: khi đặt mật khẩu mới qua liên kết, mọi phiên đăng nhập của bạn sẽ kết thúc.'}
          {!resetTarget.hasPassword &&
            ' Tài khoản này hiện chỉ đăng nhập bằng Google; sau khi đặt, người này đăng nhập được thêm bằng mật khẩu.'}
        </ConfirmDialog>
      )}
    </>
  )
}
