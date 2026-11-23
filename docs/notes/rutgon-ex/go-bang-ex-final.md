# EX cuối cùng — nhóm go-bang

Sinh tự động từ các `.txt` sequence (script `scripts/rutgon_ex_final.py`). Đây là bản chuẩn: UC Spec phải khớp.

## UC43a_ViewMeetingRecordings
- EX-1 (không MSG): Actor is not on the current roster

## UC43b_PlayMeetingRecording
- EX-1 (không MSG): Actor no longer has access, or recording not Available

## UC43c_DownloadMeetingRecording
- EX-1 (MSG355): Actor is not the current Chair or Secretary
- EX-2 (MSG356): Meeting is Secret

## UC44a_StartRoomRecording
- EX-1 (MSG291): Confirmation missing or information invalid
- EX-2 (MSG295): Actor is not the current Secretary
- EX-3 (MSG359): Meeting or take state does not allow recording

## UC44b_StopAndSaveRoomRecording
- EX-1 (MSG362): Take is no longer open
- EX-2 (MSG295): Actor may not save this take

## UC44c_UploadMeetingAudioFile
- EX-1 (MSG291): File or recording information is invalid
- EX-2 (MSG295): Actor is not the current Secretary
- EX-3 (MSG294): Meeting does not allow uploads

## UC44d_DeleteRecordingTake
- EX-1 (MSG367): Recording has only one take
- EX-2 (MSG295): Actor is not the current Secretary

## UC44e_EraseMeetingRecording
- EX-1 (MSG364): Room microphone is still recording this take
- EX-2 (MSG295): Actor is not the current Secretary

## UC45a_ViewRecordingTranscript
- Không có EX.

## UC45c_ProcessTranscriptionQueue
- EX-2 (không MSG): Transcription failed

## UC45c_TranscribeMeetingRecording
- EX-1 (MSG301): Transcript is Pending or Processing
- EX-3 (MSG479): Recording is the combined recording of a Hybrid Meeting

## UC46a_EditTranscriptText
- EX-1 (MSG303): Text is empty or too long
- EX-2 (MSG295): Actor is not the current Secretary

## UC46b_AssignSpeaker
- EX-1 (MSG317): Person is not on the current roster
- EX-2 (MSG295): Actor is not the current Secretary
- EX-3 (không MSG): The voice is not in this recording

## UC46c_ExcludeOrRestoreSentence
- EX-1 (MSG353): Actor is not the current Chair or Secretary

## UC46d_DeleteSentence
- EX-1 (MSG323): Actor is neither the Secretary nor the speaker

## UC47_GenerateMeetingSummary
- EX-1 (MSG385): Actor is not the current Chair or Secretary
- EX-2 (MSG386): The Meeting has not ended
- EX-3 (MSG387): A draft is already being written

## UC48_ViewMeetingSummary
- EX-1 (không MSG): The Meeting is not accessible

## UC49_EditMeetingSummary
- EX-1 (MSG385): Actor is not the current Chair or Secretary
- EX-2 (MSG390): No finished draft, or the AI is writing it again
- EX-3 (MSG389): A section is empty or too long

## UC50_ApproveMeetingSummary
- EX-1 (MSG385): Actor is not the current Chair or Secretary
- EX-2 (MSG390): No finished draft
- EX-3 (MSG387): The AI is writing the draft again

## UC80_ManageMyRecordedSpeech
- EX-1 (MSG323): Actor is neither the speaker nor the Secretary

## UC81a_ViewMyVoiceId
- Không có EX.

## UC81b_GiveVoiceIdConsent
- EX-1 (MSG313): Notice version read is not the current one

## UC81c_RecordVoiceSample
- EX-1 (MSG308): No standing consent
- EX-2 (MSG309): Audio is unsupported or fails the quality rules
- EX-3 (MSG310): Sample limit reached

## UC81d_PlayVoiceSample
- EX-1 (không MSG): Sample is not the actor's, or its recording was not kept under the notice consented to

## UC81e_DeleteVoiceSample
- Không có EX.

## UC81f_WithdrawVoiceIdConsent
- Không có EX.

## UC86a_ViewVoiceCollection
- EX-1 (không MSG): No access: no Voice sample collection permission

## UC86b_RecordVoiceSampleForAttendee
- EX-1 (MSG475): Person has not agreed under the current terms
- EX-2 (MSG476): Person already has 50 collected samples
- EX-3 (MSG292): Audio is unsupported or fails the quality rules

## UC86c_PlayCollectedVoiceSample
- EX-1 (không MSG): No access: no Voice sample collection permission

## UC86d_DeleteCollectedVoiceSample
- EX-1 (không MSG): No access: no Voice sample collection permission, or the sample was recorded by the person
