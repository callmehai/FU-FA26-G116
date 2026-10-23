import { useState } from 'react'
import { assignSpeaker } from '../../api/transcripts'
import { Badge } from '../../components/Badge'
import { Icon } from '../../components/Icon'
import { Select } from '../../components/Select'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import type { Transcript, TranscriptSpeaker } from '../../types/transcript'
import { formatDuration } from './format'
import { speakerName, speakerTone } from './speakers'

/**
 * SCRUM-35 — who each voice is. The machine's guess reads as a question ("Có thể là …") until the
 * Secretary confirms it or picks someone else; only then does the name replace "Người nói N".
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

  const suggestion =
    speaker.assignedUserId === null && speaker.suggestedUserId && speaker.suggestedName ? speaker : null
  return (
    <li className="speaker-row">
      <span className="speaker-chip" tabIndex={0} data-tooltip={`Nói tổng cộng ${formatDuration(speaker.speechMs)}`}>
        <Badge tone={speakerTone(speaker.label)}>{speakerName(speaker.label)}</Badge>
      </span>
      <span className="speaker-who">
        {speaker.assignedName ? (
          // The Secretary already reads the name in the picker next to it.
          !canEdit && <strong>{speaker.assignedName}</strong>
        ) : suggestion ? (
          <span
            className="speaker-suggestion"
            tabIndex={0}
            data-tooltip="Máy so giọng với giọng mẫu của người trong cuộc họp đã bật Voice ID. Thư ký xác nhận mới thành tên."
          >
            <span className="speaker-suggestion-name">
              Có thể là <strong>{suggestion.suggestedName}</strong>
            </span>
            <span className="field-hint">{Math.round((suggestion.suggestionScore ?? 0) * 100)}%</span>
          </span>
        ) : (
          <span className="field-hint">Chưa rõ là ai</span>
        )}
      </span>
      {canEdit && (
        <span className="speaker-actions">
          {suggestion && (
            <button
              type="button"
              className="btn btn-outline btn-sm"
              disabled={saving}
              onClick={() => void assign(suggestion.suggestedUserId)}
            >
              <Icon name="check" size={16} />
              Đúng
            </button>
          )}
          <Select
            label={`Gán ${speakerName(speaker.label)} cho`}
            value={speaker.assignedUserId ?? NOBODY}
            options={[{ value: NOBODY, label: 'Chưa gán' }, ...people.map((p) => ({ value: p.id, label: p.fullName }))]}
            onChange={(value) => void assign(value === NOBODY ? null : value)}
          />
        </span>
      )}
    </li>
  )
}
