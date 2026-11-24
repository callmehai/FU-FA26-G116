# EX cuối cùng — nhóm hop

Sinh tự động từ các `.txt` sequence (script `scripts/rutgon_ex_final.py`). Đây là bản chuẩn: UC Spec phải khớp.

## SYS_AutoEndForgottenMeeting
- Không có EX.

## UC14_CreateMeeting
- EX-1 (MSG31): Meeting information is invalid
- EX-2 (MSG277): Participants are invalid

## UC15_UpdateMeeting
- EX-1 (MSG31): Invalid or unauthorized update
- EX-2 (MSG33): Reschedule not permitted
- EX-3 (không MSG): Planned start is more than 24 hours ago

## UC16_ViewMeetingDetails
- EX-1 (MSG37): Meeting does not exist or is not accessible

## UC17_ViewMeetingList
- EX-1 (MSG32): Criteria are invalid

## UC18_SubmitMeetingForApproval
- EX-1 (không MSG): Incomplete meeting information
- EX-2 (MSG41): Meeting status not eligible
- EX-3 (MSG140): No eligible approver available

## UC19_ReviewMeetingApproval
- EX-1 (MSG83): Rejection reason is missing
- EX-2 (không MSG): Meeting is no longer awaiting approval
- EX-3 (MSG84): Rejection reason is too long

## UC20_PublishMeeting
- EX-1 (MSG46): Meeting is no longer eligible for publication

## UC21_StartMeeting
- EX-1 (MSG112): Meeting cannot be started

## UC22_EndMeeting
- EX-1 (MSG114): Meeting cannot be ended

## UC23a_CreateAgendaItem
- EX-1 (MSG66): Invalid agenda item information
- EX-2 (MSG67): Agenda planning no longer permitted

## UC23b_UpdateAgendaItem
- EX-1 (MSG66): Invalid agenda item information
- EX-2 (MSG67): Editing no longer permitted

## UC23c_DeleteAgendaItem
- EX-1 (MSG67): Agenda planning no longer permitted
- EX-2 (MSG142): Agenda item has already started

## UC23d_ReorderAgendaItems
- EX-1 (MSG67): Reordering no longer permitted
- EX-2 (MSG143): Invalid or outdated agenda order

## UC24_SetCurrentAgendaItem
- EX-1 (MSG73): Agenda item cannot become current
- EX-2 (MSG72): Another agenda item is in progress

## UC25_CompleteAgendaItem
- EX-1 (MSG74): Agenda item cannot be completed

## UC26_ViewMeetingAgenda
- EX-1 (không MSG): Meeting not accessible

## UC27_ViewMeetingParticipants
- EX-1 (không MSG): Roster is not accessible

## UC28a_AddMeetingParticipant
- EX-1 (MSG97): Participant information or selection is invalid
- EX-2 (MSG85): Actor is not allowed to manage participants
- EX-3 (MSG98): Participant management is no longer permitted

## UC28b_AssignChangeMeetingRole
- EX-1 (MSG101): Meeting role is invalid
- EX-2 (MSG109): Required meeting role would be lost
- EX-3 (MSG98): Actor, target or meeting is no longer eligible

## UC28c_RemoveMeetingParticipant
- EX-1 (MSG98): Removal is no longer permitted
- EX-2 (MSG109): Removal would violate a required meeting role
- EX-3 (MSG85): Actor is not allowed to manage participants

## UC29_RespondToMeetingInvitation
- EX-1 (MSG107): Invitation is no longer answerable
- EX-2 (MSG106): Response value is invalid
- EX-3 (MSG285): Different response after answering

## UC30_ViewParticipantResponseStatus
- EX-1 (không MSG): Responses are not accessible

## UC31_RecordAttendance
- EX-1 (MSG148): Submitted attendance value is invalid
- EX-2 (MSG149): Meeting is no longer in progress
- EX-3 (không MSG): Secretary is no longer authorized

## UC37_JoinMeetingRoom
- EX-1 (MSG331): Meeting has ended
- EX-2 (MSG280): Meeting is held in person
- EX-3 (MSG281): Room service is not configured

## UC38_LeaveMeetingRoom
- EX-1 (MSG331): Meeting has ended while in the room
- EX-2 (MSG329): Same account joined elsewhere
- EX-3 (MSG330): Actor is no longer on the participant list

## UC39a_ManageMicrophone
- EX-1 (MSG334): Browser blocks the microphone
- EX-2 (MSG335): No microphone is available
- EX-3 (MSG320): Meeting cannot be recorded now

## UC39a_RecordSpeechWhileMicrophoneOn
- EX-1 (MSG320): Meeting cannot be recorded now

## UC39b_ManageCamera
- EX-1 (MSG334): The browser blocks the camera
- EX-2 (MSG335): No camera is available

## UC39c_ManageScreenSharing
- Không có EX.

## UC39d_ChooseAudioAndVideoDevices
- EX-1 (MSG335): The chosen device is not found
- EX-2 (MSG337): The device cannot be opened

## UC40_ViewMeetingRoomParticipants
- Không có EX.

## UC41_ModerateMeetingRoom
- EX-1 (MSG123): Actor is no longer the chair
- EX-2 (MSG117): Meeting is no longer in progress
- EX-3 (MSG282): Target is no longer in the room or has no microphone on

## UC42_UseInRoomChat
- Không có EX.

## UC73_HandOverChair
- EX-1 (MSG259): Selected participant can no longer receive the chair role
- EX-2 (MSG260): Hand-over is no longer permitted

## UC75_CancelMeeting
- EX-1 (MSG262): Cancellation reason is missing
- EX-2 (MSG345): Cancellation reason is too long
- EX-3 (MSG263): Meeting can no longer be cancelled

## UC77_RecordAgendaItemConclusion
- EX-1 (MSG272): Conclusion is blank or too long
- EX-2 (MSG273): Conclusion can no longer be changed

## UC78_FloatingWindow
- Không có EX.

## UC79_RegisterToSpeak
- EX-1 (MSG117): Meeting is no longer in progress
- EX-2 (không MSG): Actor already has an open request
- EX-3 (không MSG): Actor is no longer the chair

## UC82_TakeMeetingNotes
- EX-1 (MSG416): Actor is not the current secretary
- EX-2 (MSG417): Meeting no longer accepts notes
- EX-3 (MSG418): Note was changed in another window
