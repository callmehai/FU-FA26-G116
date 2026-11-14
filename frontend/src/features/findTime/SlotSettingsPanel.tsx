import { useId } from 'react'
import { DateField } from '../../components/DateField'
import { Icon } from '../../components/Icon'
import { InfoTip } from '../../components/InfoTip'
import { Select } from '../../components/Select'
import { TimeField } from '../../components/TimeField'
import {
  BUFFER_OPTIONS,
  DURATION_OPTIONS,
  STEP_OPTIONS,
  WEEKDAY_LABEL,
  type SettingsErrors,
  type SlotSettings,
} from './slotRules'

/**
 * UC-85a step 1 — the search conditions, AI-eCabinet's left column: length, dates, weekdays, working hours, lunch, the
 * gap to other meetings and the start-time step. "Tìm giờ" sits at its foot.
 */
export function SlotSettingsPanel({
  value,
  onChange,
  errors,
  pending,
  onSearch,
}: {
  value: SlotSettings
  onChange: (next: SlotSettings) => void
  errors: SettingsErrors
  pending: boolean
  onSearch: () => void
}) {
  const id = useId()
  const set = <K extends keyof SlotSettings>(key: K, v: SlotSettings[K]) => onChange({ ...value, [key]: v })
  const error = (message?: string) => message && <small className="field-error">{message}</small>

  function toggleDay(day: number) {
    set('weekdays', value.weekdays.includes(day) ? value.weekdays.filter((d) => d !== day) : [...value.weekdays, day])
  }

  return (
    <form
      className="card find-settings"
      aria-label="Điều kiện tìm giờ"
      noValidate
      onSubmit={(e) => {
        e.preventDefault()
        onSearch()
      }}
    >
      <div className="field" data-field="durationMinutes">
        <span>Thời lượng họp</span>
        <Select
          label="Thời lượng họp"
          value={String(value.duration)}
          options={DURATION_OPTIONS}
          onChange={(v) => set('duration', Number(v))}
        />
        {error(errors.durationMinutes)}
      </div>

      <fieldset className="field find-pair" aria-label="Khoảng ngày">
        <span>Khoảng ngày</span>
        <div className="find-pair-row">
          <label htmlFor={`${id}-from`} className="visually-hidden">Từ ngày</label>
          <DateField id={`${id}-from`} value={value.from} onChange={(v) => set('from', v)} invalid={!!errors.fromDate} />
          <Icon name="arrowRight" size={16} />
          <label htmlFor={`${id}-to`} className="visually-hidden">Đến ngày</label>
          <DateField id={`${id}-to`} value={value.to} onChange={(v) => set('to', v)} invalid={!!errors.toDate} />
        </div>
        {error(errors.fromDate) ?? error(errors.toDate)}
      </fieldset>

      <fieldset className="field" aria-label="Ngày trong tuần">
        <span>Ngày trong tuần</span>
        <div className="find-days">
          {WEEKDAY_LABEL.map((label, i) => (
            <button
              key={label}
              type="button"
              className="chip-toggle"
              aria-pressed={value.weekdays.includes(i + 1)}
              onClick={() => toggleDay(i + 1)}
            >
              {label}
            </button>
          ))}
          {/* BR-422: slots fall Monday to Friday; the weekend shows, but cannot be chosen. */}
          <button type="button" className="chip-toggle" disabled>
            T7
          </button>
          <button type="button" className="chip-toggle" disabled>
            CN
          </button>
        </div>
        {error(errors.weekdays)}
      </fieldset>

      <fieldset className="field find-pair" aria-label="Giờ làm việc">
        <span>Giờ làm việc</span>
        <div className="find-pair-row">
          <label htmlFor={`${id}-ws`} className="visually-hidden">Giờ bắt đầu làm việc</label>
          <TimeField id={`${id}-ws`} value={value.workStart} onChange={(v) => set('workStart', v)} invalid={!!errors.workStart} columns />
          <Icon name="arrowRight" size={16} />
          <label htmlFor={`${id}-we`} className="visually-hidden">Giờ kết thúc làm việc</label>
          <TimeField id={`${id}-we`} value={value.workEnd} onChange={(v) => set('workEnd', v)} invalid={!!errors.workEnd} columns />
        </div>
        {error(errors.workStart) ?? error(errors.workEnd)}
      </fieldset>

      <fieldset className="field find-pair" aria-label="Giờ nghỉ trưa">
        <label className="toolbar-check">
          <input type="checkbox" checked={value.lunch} onChange={(e) => set('lunch', e.target.checked)} />
          Tránh giờ nghỉ trưa
        </label>
        {value.lunch && (
          <div className="find-pair-row">
            <label htmlFor={`${id}-ls`} className="visually-hidden">Bắt đầu nghỉ trưa</label>
            <TimeField id={`${id}-ls`} value={value.lunchStart} onChange={(v) => set('lunchStart', v)} invalid={!!errors.lunchStart} columns />
            <Icon name="arrowRight" size={16} />
            <label htmlFor={`${id}-le`} className="visually-hidden">Kết thúc nghỉ trưa</label>
            <TimeField id={`${id}-le`} value={value.lunchEnd} onChange={(v) => set('lunchEnd', v)} invalid={!!errors.lunchEnd} columns />
          </div>
        )}
        {error(errors.lunchStart) ?? error(errors.lunchEnd)}
      </fieldset>

      <div className="find-two">
        <div className="field" data-field="bufferMinutes">
          <span className="find-label">
            Cách cuộc họp khác
            <InfoTip text="Khung đề xuất cách mọi giờ bận ít nhất chừng này, cả trước lẫn sau." />
          </span>
          <Select
            label="Cách cuộc họp khác"
            value={String(value.buffer)}
            options={BUFFER_OPTIONS}
            onChange={(v) => set('buffer', Number(v))}
          />
          {error(errors.bufferMinutes)}
        </div>
        <div className="field" data-field="stepMinutes">
          <span className="find-label">
            Làm tròn giờ bắt đầu
            <InfoTip text="Giờ bắt đầu đề xuất rơi vào các mốc này, tính từ 00:00." />
          </span>
          <Select
            label="Làm tròn giờ bắt đầu"
            value={String(value.step)}
            options={STEP_OPTIONS}
            onChange={(v) => set('step', Number(v))}
          />
          {error(errors.stepMinutes)}
        </div>
      </div>

      <button type="submit" className="btn btn-primary find-submit" disabled={pending}>
        <Icon name="search" size={16} />
        {pending ? 'Đang tìm…' : 'Tìm giờ'}
      </button>
    </form>
  )
}
