# Transcription & Summary — API (FE-10, TRANS)

Requirement: Transcription, transcript editing, speaker attribution, summary, Voice ID, voice collection and AI settings endpoints.

Precondition: —

## POST /api/recordings/{recordingId}/transcribe (UC-45c)

*No case yet.*

## GET /api/recordings/{recordingId}/transcript (UC-45a)

*No case yet.*

## GET /api/recordings/{recordingId}/transcript.txt (UC-45b)

*No case yet.*

## GET /api/meetings/{meetingId}/speech-clips.txt (UC-45b)

*No case yet.*

## PUT /api/transcript-segments/{segmentId} (UC-46a)

*No case yet.*

## PUT /api/speech-clips/{clipId}/text (UC-46a)

*No case yet.*

## PUT /api/transcript-segments/{segmentId}/speaker (UC-46b)

*No case yet.*

## PUT /api/recordings/{recordingId}/speakers/{label} (UC-46b)

*No case yet.*

## POST /api/meetings/{meetingId}/summary (UC-47)

*No case yet.*

## GET /api/meetings/{meetingId}/summary (UC-48)

*No case yet.*

## PUT /api/meetings/{meetingId}/summary (UC-49)

*No case yet.*

## POST /api/meetings/{meetingId}/summary/review (UC-50)

*No case yet.*

## GET /api/me/voice (UC-81a)

*No case yet.*

## POST /api/me/voice/consent (UC-81b)

*No case yet.*

## DELETE /api/me/voice/consent (UC-81f)

*No case yet.*

## POST /api/me/voice/samples (UC-81c)

*No case yet.*

## GET /api/me/voice/samples/{sampleId}/audio (UC-81d)

*No case yet.*

## DELETE /api/me/voice/samples/{sampleId} (UC-81e)

*No case yet.*

## GET /api/meetings/{meetingId}/voice-collection (UC-81c)

*No case yet.*

## POST /api/meetings/{meetingId}/voice-collection/{userId}/samples (UC-81c)

*No case yet.*

## GET /api/meetings/{meetingId}/voice-collection/samples/{sampleId}/audio (UC-81d)

*No case yet.*

## DELETE /api/meetings/{meetingId}/voice-collection/samples/{sampleId} (UC-81e)

*No case yet.*

## GET /api/admin/ai (UC-47)

*No case yet.*

## PUT /api/admin/ai (UC-47)

*No case yet.*

## POST /api/admin/ai/test (UC-47)

*No case yet.*
