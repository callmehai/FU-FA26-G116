import { useState } from 'react'
import { assignSpeaker } from '../../api/transcripts'
import { Icon } from '../../components/Icon'
import { Select } from '../../components/Select'
import { useToast } from '../../components/toastContext'
import { errorMessage } from '../../lib/errors'
import type { Transcript, TranscriptSpeaker } from '../../types/transcript'
import { formatDuration } from './format'
import { speakerColor, speakerName, voiceName, voicesOf, type Voice } from './speakers'

/**
 * SCRUM-35 — who each voice is. The machine's guess is only a suggestion ("Khớp giọng nhất" at the top of
 * the picker) until the Secretary picks someone; only then does the name replace "Người nói N".
 *
 * Each person is one small cell, several to a row, as AI-eCabinet's speaker grid: voices the Secretary gave one name
 * fold into one cell, with their speech added up, and naming it names every voice it holds.
 *
 * As in AI-eCabinet, two voices may be suggested as one person when the machine split them in two: the weaker
 * says "Cùng giọng với Người nói N" and its picker offers that person for both voices at once.
 */

const NOBODY = ''
const BOTH = 'both:'

export function SpeakerList({ transcript, onChanged }: { transcript: Transcript; onChanged: () => void }) {
  if (transcript.speakers.length === 0) return null
  const { voices } = voicesOf(transcript.speakers)
  return (
    <ul className="speaker-list" aria-label="Người nói">
      {[...voices.values()].map((voice) => {
        const spoke = formatDuration(voice.speechMs)
        return (
          <li key={voice.speaker.label} className="speaker-row">
            {transcript.canEdit ? (
              <SpeakerPicker transcript={transcript} voice={voice} onChanged={onChanged} />
            ) : (
              <span className="speaker-name">
                <span
                  className="select-dot"
                  style={{ background: speakerColor(voice.speaker.label) }}
                  aria-hidden="true"
                />
                {voiceName(voice.speaker)}
              </span>
            )}
            {voice.labels.length > 1 && (
              <span
                className="speaker-same"
                tabIndex={0}
                aria-label={`Gộp ${voice.labels.length} giọng`}
                data-tooltip={`Gộp ${voice.labels.length} giọng máy nghe là cùng một người`}
              >
                <Icon name="users" size={14} />
              </span>
            )}
            <span className="speaker-time" tabIndex={0} data-tooltip={`Nói tổng cộng ${spoke}`}>
              {spoke}
            </span>
          </li>
        )
      })}
    </ul>
  )
}

/** The voice suggested as the same person more surely, read as its speaker. */
function leadOf(transcript: Transcript, speaker: TranscriptSpeaker) {
  if (speaker.assignedUserId !== null || !speaker.suggestedUserId || !speaker.sameAsLabel) return null
  return transcript.speakers.find((s) => s.label === speaker.sameAsLabel) ?? null
}

/**
 * The Secretary's picker for who a speaker is, in the voice list and on each run of lines (`inline`): picking someone
 * names every voice of that speaker; a voice the machine split off the same person can be named along with it.
 */
export function SpeakerPicker({
  transcript,
  voice,
  onChanged,
  inline = false,
}: {
  transcript: Transcript
  voice: Voice
  onChanged: () => void
  /** On a run of lines: the name reads as the heading, with no dot of its own (the rail has it). */
  inline?: boolean
}) {
  const toast = useToast()
  const [saving, setSaving] = useState(false)
  const { speaker } = voice
  // A voice already gathered with the one it sounds like needs no "both" choice.
  const lead = [leadOf(transcript, speaker)].find((l) => l && !voice.labels.includes(l.label)) ?? null

  // With `alsoLead`, the voice it sounds like is named too: one person the machine split in two.
  const assign = async (userId: string | null, alsoLead = false) => {
    setSaving(true)
    try {
      const labels = alsoLead && lead ? [...voice.labels, lead.label] : voice.labels
      let message = ''
      for (const label of labels) message = (await assignSpeaker(transcript.recordingId, label, userId)).message
      toast.show(message, 'success')
      onChanged()
    } catch (error) {
      toast.show(errorMessage(error), 'error')
      onChanged()
    } finally {
      setSaving(false)
    }
  }

  const label = speakerName(speaker.label)
  // The machine's guess is a person, not a score: it leads the picker as "Khớp giọng nhất" and names itself on
  // hover. A percentage meant nothing to the people reading it.
  const suggested = speaker.assignedUserId === null ? speaker.suggestedUserId : null
  const same = lead ? voiceName(lead) : null
  const hint =
    suggested && speaker.suggestedName
      ? `Khớp giọng nhất: ${speaker.suggestedName}${same ? ` · cùng giọng với ${same}, có thể là một người bị tách làm hai` : ''}`
      : undefined
  const both = lead && lead.assignedUserId === null && speaker.suggestedName ? speaker.suggestedName : null
  const ordered = suggested
    ? [...transcript.people].sort((a, b) => Number(b.id === suggested) - Number(a.id === suggested))
    : transcript.people
  const picker = (
    <Select
      size="sm"
      label={`Tên của ${voiceName(speaker)}`}
      value={speaker.assignedUserId}
      placeholder={label}
      marker={inline ? undefined : speakerColor(speaker.label)}
      tooltip={hint}
      disabled={saving}
      options={[
        ...(both ? [{ value: `${BOTH}${suggested}`, label: both, hint: `Cả ${same}` }] : []),
        ...ordered.map((p) => ({
          value: p.id,
          label: p.fullName,
          hint: p.id === suggested ? 'Khớp giọng nhất' : undefined,
        })),
        ...(speaker.assignedUserId ? [{ value: NOBODY, label: `Bỏ tên, để lại "${label}"` }] : []),
      ]}
      onChange={(value) =>
        void (value.startsWith(BOTH) ? assign(value.slice(BOTH.length), true) : assign(value === NOBODY ? null : value))
      }
    />
  )
  return inline ? <span className="tl-name-pick">{picker}</span> : picker
}
