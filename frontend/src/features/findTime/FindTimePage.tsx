import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { listShares } from '../../api/calendarSharing'
import { findSlots } from '../../api/meetingSlots'
import { Icon } from '../../components/Icon'
import { PageHeader } from '../../components/PageHeader'
import { SegmentedControl } from '../../components/SegmentedControl'
import { useToast } from '../../components/toastContext'
import { errorMessage, isApiError } from '../../lib/errors'
import { routes } from '../../lib/routes'
import type { Share } from '../../types/calendarSharing'
import type { Slot, SlotSearchRequest, SlotSearchResult } from '../../types/meetingSlots'
import { useAuth } from '../auth/useAuth'
import { usePhone } from '../calendar/usePhone'
import { NewShareDialog } from '../calendarSharing/ShareDialogs'
import type { ShareOutcome } from '../calendarSharing/shareOutcome'
import { SharedWeek } from '../calendarSharing/SharedWeek'
import { vietnamToday } from '../calendarSharing/shareFormat'
import { InviteePicker, type Invitee, type ShareState } from './InviteePicker'
import { SlotDraftDialog } from './SlotDraftDialog'
import { SlotResults } from './SlotResults'
import { SlotSettingsPanel } from './SlotSettingsPanel'
import {
  defaultSettings,
  minutesLabel,
  settingsDetail,
  settingsSummary,
  toRequest,
  validateSettings,
  type SettingsErrors,
  type SettingsField,
  type SlotSettings,
} from './slotRules'

type Tab = 'find' | 'shared'
const DAY = /^\d{4}-\d{2}-\d{2}$/
const SETTINGS_FIELDS: SettingsField[] = [
  'durationMinutes',
  'fromDate',
  'toDate',
  'weekdays',
  'workStart',
  'workEnd',
  'lunchStart',
  'lunchEnd',
  'bufferMinutes',
  'stepMinutes',
]

type Search =
  | { kind: 'idle' }
  | { kind: 'loading' }
  | { kind: 'done'; request: SlotSearchRequest; result: SlotSearchResult }
  | { kind: 'error'; message: string; retry: boolean }

/**
 * SCRUM-137 — Tìm giờ họp (UC-85a, UC-85b) and the week of shared hours (UC-85c), wireframe v12 16.7–16.10, laid out
 * after AI-eCabinet's page. Only calendars shared with the caller count, each at its level; the fixed sentence beside
 * the tabs says so (BR-428). The route needs `MeetingCreator`; the server checks again (403 MSG448).
 */
export function FindTimePage() {
  const { user } = useAuth()
  const toast = useToast()
  const navigate = useNavigate()
  const phone = usePhone()
  const [params, setParams] = useSearchParams()
  const tab: Tab = params.get('tab') === 'shared' ? 'shared' : 'find'
  const weekParam = params.get('week')
  const week = weekParam && DAY.test(weekParam) ? weekParam : vietnamToday()

  const [settings, setSettings] = useState<SlotSettings>(() => defaultSettings(vietnamToday()))
  const [settingsErrors, setSettingsErrors] = useState<SettingsErrors>({})
  const [invitees, setInvitees] = useState<Invitee[]>([])
  const [includeMe, setIncludeMe] = useState(false)
  const [peopleError, setPeopleError] = useState<string | undefined>()
  const [peopleRowErrors, setPeopleRowErrors] = useState<Record<string, string>>({})
  const [shares, setShares] = useState<Share[] | null>(null)
  const [search, setSearch] = useState<Search>({ kind: 'idle' })
  const [selected, setSelected] = useState<string | null>(null)
  const [draft, setDraft] = useState<{ slot: Slot; day: string } | null>(null)
  const [asking, setAsking] = useState<Invitee | 'any' | null>(null)
  const [editing, setEditing] = useState(false)

  const [sharesKey, setSharesKey] = useState(0)
  useEffect(() => {
    let cancelled = false
    listShares().then(
      (lists) => !cancelled && setShares(lists.asViewer),
      // Without the list the rows just show no share state; the search itself still tells who is counted.
      () => !cancelled && setShares([]),
    )
    return () => {
      cancelled = true
    }
  }, [sharesKey])

  /** Before searching, each invitee's share with the caller, from "Lịch được chia sẻ với tôi". */
  const shareOf = useCallback(
    (id: string): ShareState | null => {
      if (!shares) return null
      const share = shares.find((s) => s.owner.id === id && (s.status === 'Effective' || s.status === 'Pending'))
      if (!share) return { kind: 'none' }
      return share.status === 'Effective' ? { kind: 'shared', level: share.level } : { kind: 'pending' }
    },
    [shares],
  )
  const quick = useMemo(
    () =>
      (shares ?? [])
        .filter((s) => s.status === 'Effective' && !invitees.some((p) => p.id === s.owner.id))
        .map((s) => ({ id: s.owner.id, fullName: s.owner.fullName, email: s.owner.email })),
    [shares, invitees],
  )

  function setTab(next: Tab) {
    const p = new URLSearchParams(params)
    if (next === 'shared') p.set('tab', 'shared')
    else {
      p.delete('tab')
      p.delete('week')
    }
    setParams(p, { replace: true })
  }

  function setWeek(day: string) {
    const p = new URLSearchParams(params)
    if (day === vietnamToday()) p.delete('week')
    else p.set('week', day)
    setParams(p, { replace: true })
  }

  async function runSearch() {
    const errors = validateSettings(settings, vietnamToday())
    const noPeople = invitees.length === 0 ? 'Chọn ít nhất một người dự.' : undefined
    setSettingsErrors(errors)
    setPeopleError(noPeople)
    setPeopleRowErrors({})
    if (Object.keys(errors).length > 0 || noPeople) return

    const request = toRequest(
      settings,
      invitees.map((p) => p.id),
      includeMe,
    )
    setSearch({ kind: 'loading' })
    setSelected(null)
    try {
      const result = await findSlots(request)
      setSearch({ kind: 'done', request, result })
      setEditing(false)
    } catch (err) {
      if (isApiError(err, 400, 'MSG444')) {
        const fieldErrors: SettingsErrors = {}
        const rows: Record<string, string> = {}
        for (const [key, message] of Object.entries(err.body?.errors ?? {})) {
          const row = /^userIds\[(\d+)\]$/.exec(key)
          if (row && invitees[Number(row[1])]) rows[invitees[Number(row[1])].id] = message
          else if (key === 'userIds') setPeopleError(message)
          else if ((SETTINGS_FIELDS as string[]).includes(key)) fieldErrors[key as SettingsField] = message
        }
        setSettingsErrors(fieldErrors)
        setPeopleRowErrors(rows)
        setSearch({ kind: 'error', message: errorMessage(err), retry: false })
        if (phone && Object.keys(fieldErrors).length > 0) setEditing(true)
      } else {
        // MSG448: the right to create meetings is gone; trying again changes nothing.
        setSearch({ kind: 'error', message: errorMessage(err), retry: !isApiError(err, 403) })
      }
    }
  }

  function shared(outcome: ShareOutcome) {
    setAsking(null)
    toast.show(outcome.kind === 'saved' ? outcome.result.message : errorMessage(outcome.error), outcome.kind === 'saved' ? 'success' : 'error')
    setSharesKey((k) => k + 1)
  }

  const me: Invitee | null = user ? { id: user.id, fullName: user.fullName, email: user.email } : null
  const done = search.kind === 'done' ? search : null
  const peopleById = new Map((done?.result.people ?? []).map((p) => [p.id, p]))

  const settingsPanel = (
    <SlotSettingsPanel
      value={settings}
      onChange={(next) => {
        setSettings(next)
        setSettingsErrors({})
      }}
      errors={settingsErrors}
      pending={search.kind === 'loading'}
      onSearch={() => void runSearch()}
    />
  )

  return (
    <>
      <PageHeader title="Tìm giờ họp" />
      <div className="find-tabs">
        <SegmentedControl
          label="Tìm giờ họp"
          value={tab}
          options={[
            { value: 'find', label: 'Tìm khung giờ' },
            { value: 'shared', label: 'Lịch được chia sẻ' },
          ]}
          onChange={setTab}
        />
        {/* BR-428: always shown, whatever the result. */}
        <p className="find-note">Chỉ tính lịch đã được chia sẻ. Người được mời vẫn có thể bận.</p>
      </div>

      {tab === 'shared' ? (
        <SharedWeek week={week} onWeek={setWeek} onAsk={() => setAsking('any')} />
      ) : (
        <div className="find-layout">
          {phone && !editing ? (
            <div className="card find-summary">
              <b>
                {invitees.length} người, {minutesLabel(settings.duration)}
              </b>
              <span className="find-summary-sub">{settingsDetail(settings)}</span>
              <button type="button" className="btn btn-outline" onClick={() => setEditing(true)}>
                <Icon name="settings" size={16} />
                Sửa điều kiện
              </button>
            </div>
          ) : (
            settingsPanel
          )}

          <div className="find-main">
            {me && (
              <InviteePicker
                value={invitees}
                onChange={(next) => {
                  setInvitees(next)
                  setPeopleError(undefined)
                  setPeopleRowErrors({})
                }}
                me={me.id}
                includeMe={includeMe}
                onIncludeMe={setIncludeMe}
                shareOf={shareOf}
                quick={quick}
                onRequest={setAsking}
                error={peopleError}
                rowErrors={peopleRowErrors}
              />
            )}
            {phone && !editing && (
              <button
                type="button"
                className="btn btn-primary find-submit"
                disabled={search.kind === 'loading'}
                onClick={() => void runSearch()}
              >
                <Icon name="search" size={16} />
                {search.kind === 'loading' ? 'Đang tìm…' : 'Tìm giờ'}
              </button>
            )}

            {search.kind === 'idle' && (
              <div className="card find-empty">
                <Icon name="calendarSearch" size={24} />
                <p className="empty-title">Chọn người dự rồi bấm Tìm giờ</p>
                <p className="find-empty-text">Mặc định: {settingsSummary(settings)}, tránh giờ nghỉ trưa, cách cuộc họp khác 30 phút.</p>
              </div>
            )}
            {search.kind === 'loading' && <p className="find-count card-pending">Đang tìm khung giờ…</p>}
            {search.kind === 'error' && (
              <div className="alert alert-error page-alert" role="alert">
                <p>{search.message}</p>
                {search.retry && (
                  <button type="button" className="btn btn-outline btn-sm" onClick={() => void runSearch()}>
                    <Icon name="refresh" size={16} />
                    Thử lại
                  </button>
                )}
              </div>
            )}
            {done && (
              <SlotResults
                result={done.result}
                range={{ from: done.request.fromDate, to: done.request.toDate }}
                selected={selected}
                onSelect={setSelected}
                onDraft={(slot, day) => setDraft({ slot, day })}
              />
            )}
          </div>
        </div>
      )}

      {draft && done && me && (
        <SlotDraftDialog
          slot={draft.slot}
          day={draft.day}
          invitees={done.request.userIds.map((id) => {
            const p = peopleById.get(id)
            return { id, fullName: p?.fullName ?? id, email: p?.email ?? '' }
          })}
          me={me}
          includeMe={done.request.includeMe}
          bufferMinutes={done.request.bufferMinutes}
          onClose={() => setDraft(null)}
          onCreated={(response) => {
            toast.show(response.message, 'success')
            navigate(routes.meeting(response.meeting.id))
          }}
          onTaken={(message) => {
            setDraft(null)
            toast.show(message, 'error')
            void runSearch()
          }}
        />
      )}

      {asking && (
        <NewShareDialog
          mode="request"
          initialPerson={asking === 'any' ? undefined : asking}
          onClose={() => setAsking(null)}
          onDone={shared}
        />
      )}
    </>
  )
}
