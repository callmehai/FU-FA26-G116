import { useState, type FormEvent } from 'react'
import { createRole, updateRole } from '../../api/roles'
import { Dialog } from '../../components/Dialog'
import { errorMessage, isApiError } from '../../lib/errors'
import type { RoleMutationResponse, RoleRecord } from '../../types/role'
import type { PermissionDescriptor } from '../../types/rolePermission'
import type { Capability } from '../../types/user'
import { Icon } from '../../components/Icon'

export type RoleFormTarget = { mode: 'create' } | { mode: 'edit'; role: RoleRecord }
type RoleField = 'name' | 'description' | 'capabilities'
type RoleFieldErrors = Partial<Record<RoleField, string>>

function normaliseRoleName(value: string) {
  return value.normalize('NFC').replace(/\s+/gu, ' ').trim()
}

/** Create a custom role, or edit its name and description. Permissions stay in the matrix. */
export function RoleFormDialog({
  target,
  capabilities,
  onSaved,
  onAccessLost,
  onGone,
  onClose,
}: {
  target: RoleFormTarget
  capabilities: PermissionDescriptor[]
  onSaved: (response: RoleMutationResponse) => void
  onAccessLost: (error: unknown) => boolean
  onGone: () => void
  onClose: () => void
}) {
  const editing = target.mode === 'edit'
  const [name, setName] = useState(editing ? target.role.name : '')
  const [description, setDescription] = useState(editing ? (target.role.description ?? '') : '')
  const [selected, setSelected] = useState<Capability[]>([])
  const [fieldErrors, setFieldErrors] = useState<RoleFieldErrors>({})
  const [formError, setFormError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)

  function toggle(code: Capability) {
    setSelected((current) => current.includes(code) ? current.filter((item) => item !== code) : [...current, code])
    setFieldErrors((current) => ({ ...current, capabilities: undefined }))
  }

  function validate(): RoleFieldErrors {
    const errors: RoleFieldErrors = {}
    const normalizedName = normaliseRoleName(name)
    if (!normalizedName) errors.name = 'Tên vai trò không được để trống.'
    else if (normalizedName.length > 64) errors.name = 'Tên vai trò không được dài quá 64 ký tự.'
    if (description.trim().length > 256) errors.description = 'Mô tả không được dài quá 256 ký tự.'
    return errors
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    const errors = validate()
    setFieldErrors(errors)
    setFormError(null)
    if (Object.keys(errors).length > 0) return

    setPending(true)
    try {
      const body = { name: normaliseRoleName(name), description: description.trim() || null }
      const response = editing
        ? await updateRole(target.role.id, body)
        : await createRole({ ...body, capabilities: selected })
      onSaved(response)
    } catch (error) {
      if (onAccessLost(error)) return
      if (isApiError(error, 404)) {
        onGone()
        return
      }
      if (isApiError(error, 400, 'MSG20')) {
        const errors = (error.body?.errors ?? {}) as RoleFieldErrors
        setFieldErrors(errors)
        if (Object.keys(errors).length === 0) setFormError(errorMessage(error))
      } else if (isApiError(error, 409, 'MSG21')) {
        setFieldErrors({ name: errorMessage(error) })
      } else if (isApiError(error, 409, 'MSG24')) {
        setFormError(errorMessage(error))
      } else {
        setFormError(errorMessage(error))
      }
    } finally {
      setPending(false)
    }
  }

  const close = () => {
    if (!pending) onClose()
  }
  const formId = 'role-form'

  return (
    <Dialog
      title={editing ? 'Sửa vai trò' : 'Tạo vai trò'}
      description={editing ? `Cập nhật thông tin của ${target.role.name}.` : 'Tạo nhóm quyền mới cho người dùng.'}
      onClose={close}
      footer={
        <>
          <button type="button" className="btn btn-outline" onClick={close} disabled={pending}>
            Huỷ
          </button>
          <button type="submit" form={formId} className="btn btn-primary" disabled={pending}>
            <Icon name={editing ? 'check' : 'plus'} size={16} />
            {pending ? 'Đang lưu…' : editing ? 'Lưu thay đổi' : 'Tạo vai trò'}
          </button>
        </>
      }
    >
      <form id={formId} className="form-grid" onSubmit={submit} noValidate>
        {formError && <p className="alert alert-error span-2" role="alert">{formError}</p>}
        <label className="field span-2">
          <span className="field-required">Tên vai trò</span>
          <input
            value={name}
            onChange={(event) => setName(event.target.value)}
            aria-invalid={!!fieldErrors.name}
            autoFocus
          />
          {fieldErrors.name && <small className="field-error">{fieldErrors.name}</small>}
        </label>
        <label className="field span-2">
          <span>Mô tả</span>
          <textarea
            value={description}
            rows={3}
            onChange={(event) => setDescription(event.target.value)}
            aria-invalid={!!fieldErrors.description}
          />
          {fieldErrors.description && <small className="field-error">{fieldErrors.description}</small>}
        </label>
        {!editing && (
          <fieldset className="field span-2 role-capabilities">
            <legend>Quyền ban đầu (tuỳ chọn)</legend>
            <div className="role-capability-options">
              {capabilities.map((capability) => (
                <label key={capability.code}>
                  <input
                    type="checkbox"
                    checked={selected.includes(capability.code)}
                    onChange={() => toggle(capability.code)}
                  />
                  <span>{capability.name}</span>
                  <small>{capability.description}</small>
                </label>
              ))}
            </div>
            {fieldErrors.capabilities && <small className="field-error">{fieldErrors.capabilities}</small>}
            <small className="field-hint">Có thể tạo vai trò chưa được cấp quyền rồi chỉnh ma trận sau.</small>
          </fieldset>
        )}
        {editing && <p className="field-hint span-2">Thay đổi quyền thực hiện trực tiếp trong ma trận phân quyền.</p>}
      </form>
    </Dialog>
  )
}
