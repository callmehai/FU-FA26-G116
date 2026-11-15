import { useState, type FormEvent } from 'react'
import { saveGuestProfile } from '../../api/guest'
import { BrandMark } from '../../components/BrandMark'
import { Icon } from '../../components/Icon'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import type { GuestSession } from '../../types/guest'
import { readGuestEntry } from './guestStore'

const MAX_LENGTH = 200

/**
 * UC-04 step 5 (BR-180): after the code, the guest names themselves for this meeting. Only the email
 * was verified; the name and job title are what the guest says, and are labelled so wherever they show.
 * Starts from the name the Secretary invited them under. The backend re-checks everything (MSG377).
 */
export function GuestProfileForm({
  invitationId,
  session,
  onSaved,
  onSessionEnded,
}: {
  invitationId: string
  session: GuestSession
  onSaved: (session: GuestSession) => void
  onSessionEnded: () => void
}) {
  const toast = useToast()
  const [displayName, setDisplayName] = useState(session.displayName ?? session.invitedName)
  const [jobTitle, setJobTitle] = useState(session.jobTitle ?? '')
  const [errors, setErrors] = useState<{ displayName?: string; jobTitle?: string; form?: string }>({})
  const [pending, setPending] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!displayName.trim()) return setErrors({ displayName: 'Nhập tên hiển thị.' })
    setPending(true)
    setErrors({})
    try {
      const accessToken = readGuestEntry(invitationId).accessToken
      if (!accessToken) return onSessionEnded()
      const saved = await saveGuestProfile(accessToken, displayName.trim(), jobTitle.trim())
      toast.show(saved.message)
      onSaved(saved.session)
    } catch (err) {
      setPending(false)
      if (isApiError(err, 404) || isApiError(err, 401)) return onSessionEnded()
      // Named already, from another tab (MSG381): the name stands; re-reading the session opens the meeting.
      if (isApiError(err, 409, 'MSG381')) {
        toast.show(errorMessage(err), 'error')
        return onSessionEnded()
      }
      if (isApiError(err, 400, 'MSG377')) {
        const fields = (err.body?.errors ?? {}) as Record<string, string | string[]>
        const first = (v: string | string[] | undefined) => (Array.isArray(v) ? v[0] : v)
        return setErrors({
          displayName: first(fields.displayName),
          jobTitle: first(fields.jobTitle),
          form: fields.displayName || fields.jobTitle ? undefined : errorMessage(err),
        })
      }
      setErrors({ form: errorMessage(err) })
    }
  }

  return (
    <>
      <div className="login-head">
        <span className="login-mark">
          <BrandMark size={56} />
        </span>
        <p className="guest-caption">Thông tin hiển thị</p>
        <h1 className="guest-title">{session.meeting.title}</h1>
        <p className="login-sub">
          Email <strong>{session.email}</strong> đã xác minh. Tên và chức vụ do bạn tự khai.
        </p>
      </div>

      <form onSubmit={handleSubmit} noValidate>
        {errors.form && (
          <p className="alert alert-error" role="alert">
            {errors.form}
          </p>
        )}
        <div className="field">
          <label className="field-required" htmlFor="guest-display-name">
            Tên hiển thị
          </label>
          <input
            id="guest-display-name"
            value={displayName}
            maxLength={MAX_LENGTH}
            autoComplete="name"
            autoFocus
            aria-invalid={!!errors.displayName}
            aria-describedby={errors.displayName ? 'guest-display-name-error' : 'guest-display-name-hint'}
            onChange={(e) => {
              setDisplayName(e.target.value)
              setErrors((s) => ({ ...s, displayName: undefined }))
            }}
          />
          {errors.displayName ? (
            <small id="guest-display-name-error" className="field-error">
              {errors.displayName}
            </small>
          ) : (
            // Declared once per invitation (MSG381): say so before the guest commits to a name.
            <small id="guest-display-name-hint" className="field-hint">
              Tên hiển thị sẽ không thể thay đổi sau bước này.
            </small>
          )}
        </div>
        <div className="field">
          <label htmlFor="guest-job-title">Chức vụ</label>
          <input
            id="guest-job-title"
            value={jobTitle}
            maxLength={MAX_LENGTH}
            autoComplete="organization-title"
            aria-invalid={!!errors.jobTitle}
            aria-describedby={errors.jobTitle ? 'guest-job-title-error' : undefined}
            onChange={(e) => {
              setJobTitle(e.target.value)
              setErrors((s) => ({ ...s, jobTitle: undefined }))
            }}
          />
          {errors.jobTitle && (
            <small id="guest-job-title-error" className="field-error">
              {errors.jobTitle}
            </small>
          )}
        </div>
        <button className="btn btn-primary btn-block btn-cta" type="submit" disabled={pending}>
          <Icon name="arrowRight" size={18} />
          {pending ? 'Đang lưu…' : 'Vào cuộc họp'}
        </button>
      </form>
    </>
  )
}
