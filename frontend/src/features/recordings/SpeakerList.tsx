import { useState } from 'react'
import { assignSpeaker } from '../../api/transcripts'
import { Icon } from '../../components/Icon'
import { Select } from '../../components/Select'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import type { Transcript, TranscriptSpeaker } from '../../types/transcript'
import { formatDuration } from './format'
import { speakerColor, speakerName } from './speakers'

/**
 * SCRUM-35 — who each voice is. The machine's guess reads as a question ("Có thể là …") until the
 * Secretary confirms it or picks someone else; only then does the name replace "Người nói N".
 *
 * Each voice is one small cell, several to a row: its picker shows "Người nói N" until it is named,
 * as AI-eCabinet's speaker grid does, so six voices no longer push the transcript a screen down.
 */

const NOBODY = ''

export function SpeakerList({ transcript, onChanged }: { transcript: Transcript; onChanged: () => void }) {
  if (transcript.speakers.length === 0) return null
  return (
    <ul className="speaker-list" aria-label="Người nói">
      {transcript.speakers.map((speaker) => (
        <SpeakerRow
          key={speaker.label}
          recordingId={transcript.recordingId}
          speaker={speaker}
          canEdit={transcript.canEdit}
          people={transcript.people}
          onChanged={onChanged}
        />
      ))}
    </ul>
  )
}

function SpeakerRow({
  recordingId,
  speaker,
  canEdit,
  people,
  onChanged,
}: {
  recordingId: string
  speaker: TranscriptSpeaker
  canEdit: boolean
  people: Transcript['people']
  onChanged: () => void
}) {
  const toast = useToast()
  const [saving, setSaving] = useState(false)

  const assign = async (userId: string | null) => {
    setSaving(true)
    try {
      toast.show((await assignSpeaker(recordingId, speaker.label, userId)).message, 'success')
      onChanged()
    } catch (error) {
      toast.show(errorMessage(error), 'error')
    } finally {
      setSaving(false)
    }
  }

  const label = speakerName(speaker.label)
  const spoke = formatDuration(speaker.speechMs)
  const suggestion =
    speaker.assignedUserId === null && speaker.suggestedUserId && speaker.suggestedName ? speaker : null
  return (
    <li className={suggestion ? 'speaker-row speaker-row-wide' : 'speaker-row'}>
      <span className="speaker-head">
        {canEdit ? (
          <Select
            size="sm"
            label={`Tên của ${label}`}
            value={speaker.assignedUserId}
            placeholder={label}
            marker={speakerColor(speaker.label)}
            disabled={saving}
            options={[
              ...people.map((p) => ({ value: p.id, label: p.fullName })),
              ...(speaker.assignedUserId ? [{ value: NOBODY, label: `Bỏ tên, để lại "${label}"` }] : []),
            ]}
            onChange={(value) => void assign(value === NOBODY ? null : value)}
          />
        ) : (
          <span className="speaker-name">
            <span className="select-dot" style={{ background: speakerColor(speaker.label) }} aria-hidden="true" />
            {speaker.assignedName ?? label}
          </span>
        )}
        <span className="speaker-time" tabIndex={0} data-tooltip={`Nói tổng cộng ${spoke}`}>
          {spoke}
        </span>
      </span>
      {suggestion && (
        <span className="speaker-suggestion">
          <span
            className="speaker-suggestion-name"
            tabIndex={0}
            data-tooltip="Máy so giọng với giọng mẫu của người trong cuộc họp đã bật Voice ID. Thư ký xác nhận mới thành tên."
          >
            Có thể là <strong>{suggestion.suggestedName}</strong>
          </span>
          <span className="field-hint">{Math.round((suggestion.suggestionScore ?? 0) * 100)}%</span>
          {canEdit && (
            <button
              type="button"
              className="btn btn-outline btn-icon btn-sm"
              disabled={saving}
              onClick={() => void assign(suggestion.suggestedUserId)}
              aria-label={`Đúng là ${suggestion.suggestedName}`}
              data-tooltip={`Đúng là ${suggestion.suggestedName}`}
            >
              <Icon name="check" size={16} />
            </button>
          )}
        </span>
      )}
    </li>
  )
}
