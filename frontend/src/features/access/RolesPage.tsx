import { useCallback, useEffect, useState } from 'react'
import { getRolePermissionMatrix, updateRolePermissions } from '../../api/rolePermissions'
import { Badge } from '../../components/Badge'
import { PageHeader } from '../../components/PageHeader'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { PermissionDescriptor, RolePermissionMatrix } from '../../types/rolePermission'
import { ROLE_LABEL, type Capability, type Role } from '../../types/user'
import { useAccessLost } from '../auth/useAccessLost'
import { useAuth } from '../auth/useAuth'

/** The capability that opens this screen, and the one the lockout rule protects. */
const ADMIN_CAPABILITY: Capability = 'AdminUsers'

interface RowError {
  message: string
  /** Set when the backend refused because nobody active would be left holding `AdminUsers`. */
  lockout: boolean
}

function sameSet(a: readonly Capability[], b: readonly Capability[]) {
  return a.length === b.length && a.every((c) => b.includes(c))
}

/** Consecutive columns sharing a functional group, for the grouped header row. */
function groupColumns(columns: PermissionDescriptor[]) {
  const groups: { name: string; span: number }[] = []
  for (const column of columns) {
    const last = groups.at(-1)
    if (last?.name === column.functionalGroup) last.span++
    else groups.push({ name: column.functionalGroup, span: 1 })
  }
  return groups
}

/**
 * The role ↔ capability matrix (SCRUM-60): view it (UC-09) and set one role's capabilities
 * (UC-10).
 *
 * Columns and their labels come from the backend, never from a table here, and there is no way to
 * add a capability or a role — both are enums in code. Ticking boxes only edits a draft; each role
 * is saved on its own, because the backend takes one role per request and sending its complete
 * end state is what keeps one administrator's edit from overwriting another's.
 *
 * The lockout rule (someone active must keep `AdminUsers`) counts accounts, which this screen
 * cannot see, so it is left entirely to the backend: warn, let the user try, show the refusal.
 */
export function RolesPage() {
  const { user: me, signOut } = useAuth()
  const toast = useToast()
  const handleAccessLost = useAccessLost()

  /** UC-03's MSG01 when the sign-out never reached the server, as the account menu shows it. */
  async function signOutToApply() {
    if (!(await signOut())) toast.show('Không thể đăng xuất. Vui lòng thử lại.', 'error')
  }

  const [matrix, setMatrix] = useState<RolePermissionMatrix | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  /** Unsaved edits, per role. A role is absent here when it matches the saved matrix. */
  const [drafts, setDrafts] = useState<Partial<Record<Role, Capability[]>>>({})
  const [saving, setSaving] = useState<Role | null>(null)
  const [rowErrors, setRowErrors] = useState<Partial<Record<Role, RowError>>>({})
  const [ownRoleChanged, setOwnRoleChanged] = useState(false)

  const load = useCallback(async () => {
    try {
      setMatrix(await getRolePermissionMatrix())
      setLoadError(null)
    } catch (err) {
      if (!handleAccessLost(err)) setLoadError(errorMessage(err))
    }
  }, [handleAccessLost])

  useEffect(() => {
    let cancelled = false
    getRolePermissionMatrix().then(
      (result) => {
        if (!cancelled) setMatrix(result)
      },
      (err: unknown) => {
        if (!cancelled && !handleAccessLost(err)) setLoadError(errorMessage(err))
      },
    )
    // StrictMode runs effects twice in dev; only the surviving run may write state.
    return () => {
      cancelled = true
    }
  }, [handleAccessLost])

  const columns = matrix?.capabilities ?? []
  const groups = groupColumns(columns)
  const groupStarts = new Set(
    columns.filter((c, i) => c.functionalGroup !== columns[i - 1]?.functionalGroup).map((c) => c.code),
  )
  const groupStart = (code: Capability) => (groupStarts.has(code) ? ' matrix-group-start' : '')

  function saved(role: Role) {
    return matrix?.roles.find((r) => r.role === role)?.capabilities ?? []
  }

  function effective(role: Role) {
    return drafts[role] ?? saved(role)
  }

  function toggle(role: Role, capability: Capability) {
    const current = effective(role)
    // Keep the column order, so the request lists capabilities the way the screen shows them.
    const next = columns
      .map((c) => c.code)
      .filter((code) => (code === capability ? !current.includes(code) : current.includes(code)))

    setDrafts((all) => {
      const rest = { ...all }
      if (sameSet(next, saved(role))) delete rest[role]
      else rest[role] = next
      return rest
    })
    setRowErrors((all) => ({ ...all, [role]: undefined }))
  }

  function discard(role: Role) {
    setDrafts((all) => {
      const rest = { ...all }
      delete rest[role]
      return rest
    })
    setRowErrors((all) => ({ ...all, [role]: undefined }))
  }

  /** UC-10 for one role. `capabilities` is the role's whole end state, never a delta. */
  async function save(role: Role) {
    const capabilities = drafts[role]
    if (!capabilities) return

    setSaving(role)
    setRowErrors((all) => ({ ...all, [role]: undefined }))
    try {
      const response = await updateRolePermissions(role, capabilities)
      setMatrix(response.matrix)
      // Drop this draft, and any other that the new matrix happens to match already.
      setDrafts((all) => {
        const rest: typeof all = {}
        for (const [r, caps] of Object.entries(all) as [Role, Capability[]][]) {
          const now = response.matrix.roles.find((x) => x.role === r)?.capabilities ?? []
          if (r !== role && !sameSet(caps, now)) rest[r] = caps
        }
        return rest
      })
      toast.show(response.message)
      if (role === me?.role) setOwnRoleChanged(true)
    } catch (err) {
      if (handleAccessLost(err)) return
      // MSG17 names each problem under its own key; `adminUsers` is the lockout rule. MSG18 is a
      // 500 with a body and means "send the same again", which errorMessage already conveys.
      const invalid = isApiError(err, 400, 'MSG17') ? (err.body?.errors ?? {}) : null
      const lockoutMessage = invalid?.adminUsers
      const details = invalid ? Object.values(invalid).join(' ') : ''
      setRowErrors((all) => ({
        ...all,
        [role]: {
          lockout: lockoutMessage !== undefined,
          message: lockoutMessage ?? (details || errorMessage(err)),
        },
      }))
    } finally {
      setSaving(null)
    }
  }

  /**
   * Soft warning only: the screen cannot know who holds which role, so it cannot tell whether the
   * backend will accept this — it may well, if the capability lives on in a role people hold.
   */
  function dropsLastVisibleAdminCapability(role: Role) {
    if (!matrix || effective(role).includes(ADMIN_CAPABILITY) || !saved(role).includes(ADMIN_CAPABILITY)) {
      return false
    }
    return !matrix.roles.some((r) => r.role !== role && effective(r.role).includes(ADMIN_CAPABILITY))
  }

  const columnCount = columns.length + 2

  return (
    <>
      <PageHeader
        title="Vai trò"
        description="Chọn những quyền mỗi vai trò được mang. Mỗi vai trò lưu riêng."
      />

      {ownRoleChanged && (
        <div className="alert alert-warning page-alert" role="status">
          <p>
            Bạn vừa đổi quyền của chính vai trò mình đang mang. Phiên đăng nhập hiện tại vẫn giữ quyền cũ và sẽ
            kết thúc ở lần gia hạn tiếp theo — hãy đăng nhập lại để áp dụng quyền mới.
          </p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void signOutToApply()}>
            Đăng nhập lại
          </button>
        </div>
      )}

      <div className="table-card">
        {loadError ? (
          <div className="cell-empty">
            <p className="cell-error">{loadError}</p>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              onClick={() => {
                setLoadError(null)
                void load()
              }}
            >
              Thử lại
            </button>
          </div>
        ) : !matrix ? (
          <p className="cell-empty">Đang tải ma trận phân quyền…</p>
        ) : (
          <table className="data-table matrix-table">
            <thead>
              <tr>
                <th rowSpan={2}>Vai trò</th>
                {groups.map((g) => (
                  <th key={g.name} colSpan={g.span} className="matrix-group matrix-group-start">
                    {g.name}
                  </th>
                ))}
                <th rowSpan={2} className="col-action">
                  <span className="visually-hidden">Thao tác</span>
                </th>
              </tr>
              <tr>
                {columns.map((c) => (
                  <th key={c.code} className={`matrix-cap${groupStart(c.code)}`}>
                    {c.name}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {matrix.roles.map(({ role }) => {
                const dirty = drafts[role] !== undefined
                const error = rowErrors[role]
                const current = effective(role)
                const roleLabel = ROLE_LABEL[role] ?? role
                const warn = dirty && !error && dropsLastVisibleAdminCapability(role)

                return [
                  <tr key={role} className={dirty ? 'matrix-row-dirty' : undefined}>
                    <td className="cell-title">
                      {roleLabel}
                      {role === me?.role && (
                        <>
                          {' '}
                          <Badge tone="blue">Vai trò của bạn</Badge>
                        </>
                      )}
                    </td>
                    {columns.map((c) => {
                      const checked = current.includes(c.code)
                      const changed = checked !== saved(role).includes(c.code)
                      const flagged = error?.lockout && c.code === ADMIN_CAPABILITY
                      return (
                        <td
                          key={c.code}
                          className={`matrix-cell${groupStart(c.code)}${changed ? ' matrix-cell-changed' : ''}${flagged ? ' matrix-cell-error' : ''}`}
                        >
                          <input
                            type="checkbox"
                            checked={checked}
                            disabled={saving !== null}
                            onChange={() => toggle(role, c.code)}
                            aria-label={`${roleLabel} — ${c.name}`}
                          />
                        </td>
                      )
                    })}
                    <td className="col-action">
                      {dirty && (
                        <span className="row-actions">
                          <button
                            type="button"
                            className="btn btn-ghost btn-sm"
                            onClick={() => discard(role)}
                            disabled={saving !== null}
                          >
                            Hoàn tác
                          </button>
                          <button
                            type="button"
                            className="btn btn-primary btn-sm"
                            onClick={() => void save(role)}
                            disabled={saving !== null}
                          >
                            {saving === role ? 'Đang lưu…' : 'Lưu'}
                          </button>
                        </span>
                      )}
                    </td>
                  </tr>,
                  (error || warn) && (
                    <tr key={`${role}-note`} className="matrix-note-row">
                      <td colSpan={columnCount}>
                        {error ? (
                          <p className="alert alert-error" role="alert">
                            {error.message}
                          </p>
                        ) : (
                          <p className="alert alert-warning">
                            Sau khi lưu, không vai trò nào trên bảng còn quyền quản trị người dùng. Hệ thống sẽ từ
                            chối nếu thao tác này khiến không còn ai quản trị được.
                          </p>
                        )}
                      </td>
                    </tr>
                  ),
                ]
              })}
            </tbody>
          </table>
        )}
      </div>

      {matrix && (
        <dl className="capability-legend">
          {columns.map((c) => (
            <div key={c.code}>
              <dt>{c.name}</dt>
              <dd>{c.description}</dd>
            </div>
          ))}
        </dl>
      )}

      <p className="page-note">
        Lưu quyền của một vai trò sẽ kết thúc phiên đăng nhập của những người đang mang vai trò đó, để quyền mới có
        hiệu lực. Danh mục quyền và vai trò do hệ thống định nghĩa, không thêm mới từ màn hình này.
      </p>
    </>
  )
}
