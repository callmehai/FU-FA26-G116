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

  /** UC-03's MSG06 when the sign-out never reached the server, as the account menu shows it. */
  async function signOutToApply() {
    if (!(await signOut())) toast.show('Không thể đăng xuất. Vui lòng thử lại.', 'error')
  }

  const [matrix, setMatrix] = useState<RolePermissionMatrix | null>(null)
  const [roles, setRoles] = useState<RoleRecord[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [drafts, setDrafts] = useState<Record<string, Capability[]>>({})
  const [saving, setSaving] = useState<string | null>(null)
  const [rowErrors, setRowErrors] = useState<Record<string, RowError | undefined>>({})
  const [ownRoleChanged, setOwnRoleChanged] = useState(false)
  const [activeTab, setActiveTab] = useState<'roles' | 'matrix'>('roles')
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
    // Let load own the request and defer its state updates past the effect's synchronous phase.
    void Promise.resolve().then(load)
  }, [load])

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
        await load()
        throw error
      }
      if (isApiError(error, 404)) {
        // Nothing left to retry: close the dialog and say why, rather than offer the same 404 again.
        toast.show('Vai trò này không còn tồn tại. Danh sách đã được cập nhật.', 'error')
        await load()
        return
      }
      if (isApiError(error, 409, 'MSG24')) await load()
      throw error
    }
  }

  const columnCount = columns.length + 2

  return (
    <>
      <PageHeader
        eyebrow="Quản trị"
        title="Vai trò"
        description="Mỗi vai trò là một nhóm quyền; gán vai trò cho tài khoản ở màn Người dùng & Quyền."
        action={
          <button type="button" className="btn btn-primary btn-lg" onClick={() => setFormTarget({ mode: 'create' })}>
            <Icon name="plus" size={18} />
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
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void signOutToApply()}>
            Đăng nhập lại
          </button>
        </div>
      )}

      <div className="tabs" role="tablist" aria-label="Vai trò">
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'roles'}
          className={`tab${activeTab === 'roles' ? ' tab-active' : ''}`}
          onClick={() => setActiveTab('roles')}
        >
          Danh sách vai trò
          {roles && <span className="tab-count">{roles.length}</span>}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'matrix'}
          className={`tab${activeTab === 'matrix' ? ' tab-active' : ''}`}
          onClick={() => setActiveTab('matrix')}
        >
          Ma trận phân quyền
        </button>
      </div>

      {loadError ? (
        <div className="cell-empty">
          <p className="cell-error">{loadError}</p>
          <button type="button" className="btn btn-outline btn-sm" onClick={() => void load()}>
            Thử lại
          </button>
        </div>
      ) : activeTab === 'roles' ? (
        !roles ? (
          <p className="cell-empty">Đang tải danh sách vai trò…</p>
        ) : (
          <div className="list-panel table-card">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Tên vai trò</th>
                  <th>Mô tả</th>
                  <th>Tài khoản</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {roles.map((role) => (
                  <tr key={role.id}>
                    <td className="cell-title">
                      {role.name}{' '}
                      {role.isBuiltIn && <Badge tone="outline">Mặc định</Badge>}{' '}
                      {role.id === me?.roleId && <Badge tone="blue">Vai trò của bạn</Badge>}
                    </td>
                    <td className="cell-muted">{role.description ?? '—'}</td>
                    <td>{role.userCount}</td>
                    <td>
                      {!role.isBuiltIn && (
                        <span className="row-actions">
                          <button
                            type="button"
                            className="icon-button"
                            onClick={() => setFormTarget({ mode: 'edit', role })}
                            title="Sửa vai trò"
                            aria-label={`Sửa vai trò ${role.name}`}
                          >
                            <Icon name="pencil" size={16} />
                          </button>
                          <button
                            type="button"
                            className="icon-button"
                            onClick={() => setDeleteTarget(role)}
                            title="Xoá vai trò"
                            aria-label={`Xoá vai trò ${role.name}`}
                          >
                            <Icon name="trash" size={16} />
                          </button>
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )
      ) : (
        !matrix ? (
          <p className="cell-empty">Đang tải ma trận phân quyền…</p>
        ) : (
          <>
            <div className="list-panel table-card">
              <table className="data-table matrix-table">
                <thead>
                  <tr>
                    <th rowSpan={2} className="matrix-role">Vai trò</th>
                    {groups.map((group) => (
                      <th key={group.name} colSpan={group.span} className="matrix-group matrix-group-start">
                        {group.name}
                      </th>
                    ))}
                    <th rowSpan={2} className="col-action">Thao tác</th>
                  </tr>
                  <tr>
                    {columns.map((column) => (
                      <th key={column.code} className={`matrix-cap${groupStart(column.code)}`} title={column.description}>
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
                              {/* The label covers the whole cell, so a click anywhere in it toggles. */}
                              <label className="matrix-hit">
                                <input
                                  type="checkbox"
                                  checked={checked}
                                  disabled={saving !== null}
                                  onChange={() => toggle(id, column.code)}
                                  aria-label={`${name} — ${column.name}`}
                                />
                              </label>
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
            </div>
          </>
        )
      )}

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
            toast.show('Vai trò này không còn tồn tại.', 'error')
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
          // The count is already known, so say why up front instead of sending a request bound to
          // fail with MSG25. The backend still rechecks on delete: this count may be stale.
          blocker={
            deleteTarget.userCount > 0
              ? `Vai trò này vẫn còn ${deleteTarget.userCount} tài khoản mang, kể cả tài khoản đã vô hiệu hoá. Hãy chuyển họ sang vai trò khác trước khi xoá.`
              : undefined
          }
        >
          {deleteTarget.userCount > 0
            ? `Chưa thể xoá vai trò “${deleteTarget.name}”.`
            : `Bạn có chắc muốn xoá vai trò “${deleteTarget.name}”? Thao tác này không thể hoàn tác.`}
        </ConfirmDialog>
      )}
    </>
  )
}
