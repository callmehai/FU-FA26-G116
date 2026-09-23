import { useCallback, useEffect, useState } from 'react'
import { deleteRole, listRoles } from '../../api/roles'
import { getRolePermissionMatrix, updateRolePermissions } from '../../api/rolePermissions'
import { Badge } from '../../components/Badge'
import { ConfirmDialog } from '../../components/ConfirmDialog'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { RoleRecord } from '../../types/role'
import type { PermissionDescriptor, RolePermissionMatrix } from '../../types/rolePermission'
import type { Capability } from '../../types/user'
import { useAccessLost } from '../auth/useAccessLost'
import { useAuth } from '../auth/useAuth'
import { RoleFormDialog, type RoleFormTarget } from './RoleFormDialog'

const ADMIN_CAPABILITY: Capability = 'AdminUsers'

interface RowError {
  message: string
  lockout: boolean
}

function sameSet(a: readonly Capability[], b: readonly Capability[]) {
  return a.length === b.length && a.every((capability) => b.includes(capability))
}

function groupColumns(columns: PermissionDescriptor[]) {
  const groups: { name: string; span: number }[] = []
  for (const column of columns) {
    const last = groups.at(-1)
    if (last?.name === column.functionalGroup) last.span++
    else groups.push({ name: column.functionalGroup, span: 1 })
  }
  return groups
}

/** SCRUM-60/63: edit the role-capability matrix and manage custom roles. */
export function RolesPage() {
  const { user: me, signOut, reloadSession } = useAuth()
  const toast = useToast()
  const handleAccessLost = useAccessLost()

  const [matrix, setMatrix] = useState<RolePermissionMatrix | null>(null)
  const [roles, setRoles] = useState<RoleRecord[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, Capability[]>>({})
  const [saving, setSaving] = useState<string | null>(null)
  const [rowErrors, setRowErrors] = useState<Record<string, RowError | undefined>>({})
  const [ownRoleChanged, setOwnRoleChanged] = useState(false)
  const [formTarget, setFormTarget] = useState<RoleFormTarget | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<RoleRecord | null>(null)

  const load = useCallback(async () => {
    try {
      const [nextMatrix, nextRoles] = await Promise.all([getRolePermissionMatrix(), listRoles()])
      setMatrix(nextMatrix)
      setRoles(nextRoles)
      setDrafts((current) => Object.fromEntries(
        Object.entries(current).filter(([id, capabilities]) => {
          const role = nextMatrix.roles.find((item) => item.id === id)
          return role !== undefined && !sameSet(capabilities, role.capabilities)
        }),
      ))
      setRowErrors((current) => Object.fromEntries(
        Object.entries(current).filter(([id]) => nextMatrix.roles.some((role) => role.id === id)),
      ))
      setDeleteTarget((current) => {
        if (!current) return current
        return nextRoles.find((role) => role.id === current.id) ?? null
      })
      setLoadError(null)
    } catch (error) {
      if (!handleAccessLost(error)) setLoadError(errorMessage(error))
    }
  }, [handleAccessLost])

  useEffect(() => {
    let cancelled = false
    Promise.all([getRolePermissionMatrix(), listRoles()]).then(
      ([nextMatrix, nextRoles]) => {
        if (!cancelled) {
          setMatrix(nextMatrix)
          setRoles(nextRoles)
        }
      },
      (error: unknown) => {
        if (!cancelled && !handleAccessLost(error)) setLoadError(errorMessage(error))
      },
    )
    return () => {
      cancelled = true
    }
  }, [handleAccessLost])

  const columns = matrix?.capabilities ?? []
  const groups = groupColumns(columns)
  const groupStarts = new Set(
    columns.filter((column, index) => column.functionalGroup !== columns[index - 1]?.functionalGroup)
      .map((column) => column.code),
  )
  const groupStart = (code: Capability) => (groupStarts.has(code) ? ' matrix-group-start' : '')

  function saved(roleId: string) {
    return matrix?.roles.find((role) => role.id === roleId)?.capabilities ?? []
  }

  function effective(roleId: string) {
    return drafts[roleId] ?? saved(roleId)
  }

  function toggle(roleId: string, capability: Capability) {
    const current = effective(roleId)
    const next = columns.map((column) => column.code).filter((code) =>
      code === capability ? !current.includes(code) : current.includes(code),
    )
    setDrafts((all) => {
      const rest = { ...all }
      if (sameSet(next, saved(roleId))) delete rest[roleId]
      else rest[roleId] = next
      return rest
    })
    setRowErrors((all) => ({ ...all, [roleId]: undefined }))
  }

  function discard(roleId: string) {
    setDrafts((all) => {
      const rest = { ...all }
      delete rest[roleId]
      return rest
    })
    setRowErrors((all) => ({ ...all, [roleId]: undefined }))
  }

  async function save(roleId: string) {
    const capabilities = drafts[roleId]
    if (!capabilities) return

    setSaving(roleId)
    setRowErrors((all) => ({ ...all, [roleId]: undefined }))
    try {
      const response = await updateRolePermissions(roleId, capabilities)
      setMatrix(response.matrix)
      setDrafts((all) => {
        const rest: Record<string, Capability[]> = {}
        for (const [id, pendingCapabilities] of Object.entries(all)) {
          const current = response.matrix.roles.find((role) => role.id === id)?.capabilities ?? []
          if (id !== roleId && !sameSet(pendingCapabilities, current)) rest[id] = pendingCapabilities
        }
        return rest
      })
      toast.show(response.message)
      if (roleId === me?.roleId) setOwnRoleChanged(true)
    } catch (error) {
      if (handleAccessLost(error)) return
      const invalid = isApiError(error, 400, 'MSG17') ? error.body?.errors : undefined
      const lockoutMessage = invalid?.adminUsers
      setRowErrors((all) => ({
        ...all,
        [roleId]: {
          lockout: lockoutMessage !== undefined,
          message: lockoutMessage ?? (invalid ? Object.values(invalid).join(' ') : errorMessage(error)),
        },
      }))
    } finally {
      setSaving(null)
    }
  }

  function dropsLastVisibleAdminCapability(roleId: string) {
    if (!matrix || effective(roleId).includes(ADMIN_CAPABILITY) || !saved(roleId).includes(ADMIN_CAPABILITY)) {
      return false
    }
    return !matrix.roles.some((role) => role.id !== roleId && effective(role.id).includes(ADMIN_CAPABILITY))
  }

  async function confirmDelete(role: RoleRecord) {
    try {
      const response = await deleteRole(role.id)
      toast.show(response.message)
      setDrafts((current) => {
        const rest = { ...current }
        delete rest[role.id]
        return rest
      })
      await load()
    } catch (error) {
      if (handleAccessLost(error)) return
      if (isApiError(error, 409, 'MSG25')) {
        const count = error.body?.userCount
        await load()
        throw new Error(
          typeof count === 'number'
            ? `Vai trò này vẫn còn ${count} tài khoản mang. Hãy chuyển họ sang vai trò khác trước khi xoá.`
            : errorMessage(error),
        )
      }
      if (isApiError(error, 404) || isApiError(error, 409, 'MSG24')) await load()
      throw error
    }
  }

  const columnCount = columns.length + 2

  return (
    <>
      <PageHeader
        title="Vai trò"
        description="Quản lý vai trò và chọn những quyền mỗi vai trò được mang. Mỗi vai trò lưu riêng."
        action={
          <button type="button" className="btn btn-primary" onClick={() => setFormTarget({ mode: 'create' })}>
            Tạo vai trò
          </button>
        }
      />

      {ownRoleChanged && (
        <div className="alert alert-warning page-alert" role="status">
          <p>
            Bạn vừa đổi quyền của chính vai trò mình đang mang. Phiên đăng nhập hiện tại vẫn giữ quyền cũ và sẽ
            kết thúc ở lần gia hạn tiếp theo — hãy đăng nhập lại để áp dụng quyền mới.
          </p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void signOut()}>
            Đăng nhập lại
          </button>
        </div>
      )}

      <div className="table-card">
        {loadError ? (
          <div className="cell-empty">
            <p className="cell-error">{loadError}</p>
            <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
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
                {groups.map((group) => (
                  <th key={group.name} colSpan={group.span} className="matrix-group matrix-group-start">
                    {group.name}
                  </th>
                ))}
                <th rowSpan={2} className="col-action"><span className="visually-hidden">Thao tác</span></th>
              </tr>
              <tr>
                {columns.map((column) => (
                  <th key={column.code} className={`matrix-cap${groupStart(column.code)}`}>
                    {column.name}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {matrix.roles.map(({ id, name, isBuiltIn }) => {
                const dirty = drafts[id] !== undefined
                const error = rowErrors[id]
                const current = effective(id)
                const warn = dirty && !error && dropsLastVisibleAdminCapability(id)
                return [
                  <tr key={id} className={dirty ? 'matrix-row-dirty' : undefined}>
                    <td className="cell-title">
                      {name}{' '}
                      {isBuiltIn && <Badge tone="outline">Mặc định</Badge>}{' '}
                      {id === me?.roleId && <Badge tone="blue">Vai trò của bạn</Badge>}
                    </td>
                    {columns.map((column) => {
                      const checked = current.includes(column.code)
                      const changed = checked !== saved(id).includes(column.code)
                      const flagged = error?.lockout && column.code === ADMIN_CAPABILITY
                      return (
                        <td
                          key={column.code}
                          className={`matrix-cell${groupStart(column.code)}${changed ? ' matrix-cell-changed' : ''}${flagged ? ' matrix-cell-error' : ''}`}
                        >
                          <input
                            type="checkbox"
                            checked={checked}
                            disabled={saving !== null}
                            onChange={() => toggle(id, column.code)}
                            aria-label={`${name} — ${column.name}`}
                          />
                        </td>
                      )
                    })}
                    <td className="col-action">
                      {dirty && (
                        <span className="row-actions">
                          <button type="button" className="btn btn-ghost btn-sm" onClick={() => discard(id)} disabled={saving !== null}>
                            Hoàn tác
                          </button>
                          <button type="button" className="btn btn-primary btn-sm" onClick={() => void save(id)} disabled={saving !== null}>
                            {saving === id ? 'Đang lưu…' : 'Lưu'}
                          </button>
                        </span>
                      )}
                    </td>
                  </tr>,
                  (error || warn) && (
                    <tr key={`${id}-note`} className="matrix-note-row">
                      <td colSpan={columnCount}>
                        {error ? (
                          <p className="alert alert-error" role="alert">{error.message}</p>
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
          {columns.map((column) => (
            <div key={column.code}><dt>{column.name}</dt><dd>{column.description}</dd></div>
          ))}
        </dl>
      )}

      {roles && (
        <div className="table-card">
          <table className="data-table">
            <thead>
              <tr><th>Tên vai trò</th><th>Mô tả</th><th>Tài khoản</th><th className="col-action">Thao tác</th></tr>
            </thead>
            <tbody>
              {roles.map((role) => (
                <tr key={role.id}>
                  <td className="cell-title">
                    {role.name}{' '}{role.isBuiltIn && <Badge tone="outline">Mặc định</Badge>}
                  </td>
                  <td className="cell-muted">{role.description ?? '—'}</td>
                  <td>{role.userCount}</td>
                  <td className="col-action">
                    {!role.isBuiltIn && (
                      <span className="row-actions">
                        <button type="button" className="icon-button" onClick={() => setFormTarget({ mode: 'edit', role })} title="Sửa vai trò" aria-label={`Sửa vai trò ${role.name}`}>
                          <Icon name="pencil" size={16} />
                        </button>
                        <button type="button" className="btn btn-ghost btn-sm" onClick={() => setDeleteTarget(role)} aria-label={`Xoá vai trò ${role.name}`}>
                          Xoá
                        </button>
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <p className="page-note">
        Lưu quyền của một vai trò sẽ kết thúc phiên đăng nhập của những người đang mang vai trò đó, để quyền mới có
        hiệu lực. Danh mục quyền do hệ thống định nghĩa. Bạn có thể tạo vai trò mới từ màn hình này.
      </p>

      {formTarget && matrix && (
        <RoleFormDialog
          key={formTarget.mode === 'edit' ? formTarget.role.id : 'create'}
          target={formTarget}
          capabilities={matrix.capabilities}
          onSaved={(response) => {
            setFormTarget(null)
            toast.show(response.message)
            if (response.role.id === me?.roleId) void reloadSession()
            void load()
          }}
          onAccessLost={handleAccessLost}
          onGone={() => {
            setFormTarget(null)
            void load()
          }}
          onClose={() => setFormTarget(null)}
        />
      )}

      {deleteTarget && (
        <ConfirmDialog
          title="Xoá vai trò"
          tone="danger"
          confirmLabel="Xoá vai trò"
          pendingLabel="Đang xoá…"
          onConfirm={() => confirmDelete(deleteTarget)}
          onClose={() => setDeleteTarget(null)}
        >
          {deleteTarget.userCount > 0
            ? `Danh sách hiện ghi nhận ${deleteTarget.userCount} tài khoản mang vai trò “${deleteTarget.name}”, kể cả tài khoản đã vô hiệu hoá. Hệ thống sẽ kiểm tra lại khi xoá và từ chối nếu vẫn còn tài khoản.`
            : `Bạn có chắc muốn xoá vai trò “${deleteTarget.name}”? Thao tác này không thể hoàn tác.`}
        </ConfirmDialog>
      )}
    </>
  )
}
