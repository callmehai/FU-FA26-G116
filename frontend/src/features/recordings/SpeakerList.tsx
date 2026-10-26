import { useState } from 'react'
import { assignSpeaker } from '../../api/transcripts'
import { Select } from '../../components/Select'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import type { Transcript, TranscriptSpeaker } from '../../types/transcript'
import { formatDuration } from './format'
import { speakerColor, speakerName } from './speakers'

/**
 * SCRUM-35 — who each voice is. The machine's guess is only a suggestion ("Đề xuất" at the top of
 * the picker) until the Secretary picks someone; only then does the name replace "Người nói N".
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
  // The machine's guess is a person, not a score: it leads the picker as "Đề xuất" and names itself on
  // hover. A percentage meant nothing to the people reading it.
  const suggested = speaker.assignedUserId === null ? speaker.suggestedUserId : null
  const hint = suggested && speaker.suggestedName ? `Giọng giống ${speaker.suggestedName}` : undefined
  const ordered = suggested
    ? [...people].sort((a, b) => Number(b.id === suggested) - Number(a.id === suggested))
    : people
  return (
    <li className="speaker-row">
      {canEdit ? (
        <Select
          size="sm"
          label={`Tên của ${label}`}
          value={speaker.assignedUserId}
          placeholder={label}
          marker={speakerColor(speaker.label)}
          tooltip={hint}
          disabled={saving}
          options={[
            ...ordered.map((p) => ({
              value: p.id,
              label: p.fullName,
              hint: p.id === suggested ? 'Đề xuất' : undefined,
            })),
            ...(speaker.assignedUserId ? [{ value: NOBODY, label: `Bỏ tên, để lại "${label}"` }] : []),
          ]}
          onChange={(value) => void assign(value === NOBODY ? null : value)}
        />
      ) : (
        <span className="speaker-name" tabIndex={hint ? 0 : undefined} data-tooltip={hint}>
          <span className="select-dot" style={{ background: speakerColor(speaker.label) }} aria-hidden="true" />
          {speaker.assignedName ?? label}
        </span>
      )}
      <span className="speaker-time" tabIndex={0} data-tooltip={`Nói tổng cộng ${spoke}`}>
        {spoke}
      </span>
    </li>
  )
}
