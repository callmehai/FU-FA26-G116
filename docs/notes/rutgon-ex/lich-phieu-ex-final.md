# EX cuối cùng — nhóm lich-phieu

Sinh tự động từ các `.txt` sequence (script `scripts/rutgon_ex_final.py`). Đây là bản chuẩn: UC Spec phải khớp.

## UC52a_ViewMeetingVotes
- EX-1 (MSG205): Meeting votes are not accessible

## UC52b_ViewVoteStatus
- EX-1 (MSG205): Meeting or Vote unavailable or Draft not visible

## UC53_CreateConfigureVote
- EX-1 (MSG209): Invalid Vote information
- EX-2 (MSG213): Draft operation is no longer allowed
- EX-3 (MSG205): Meeting or Vote is not accessible

## UC54_OpenVote
- EX-1 (MSG209): Configuration is invalid
- EX-2 (MSG218): Opening is no longer allowed
- EX-3 (MSG219): Another Vote is Open

## UC55_CastVote
- EX-1 (MSG222): No option selected, or the option is not valid
- EX-2 (MSG224): Actor is not eligible, or has not confirmed reading the documents
- EX-3 (MSG225): Balloting has ended, or the actor already voted

## UC56_CloseVote
- EX-1 (MSG230): Closing is no longer allowed
- EX-2 (MSG205): Meeting or Vote is not accessible

## UC57_RecordVoteConclusion
- EX-1 (MSG232): Conclusion is invalid
- EX-2 (MSG233): Conclusion already recorded
- EX-3 (MSG234): Recording is no longer allowed

## UC58_ViewMeetingMinutes
- EX-1 (không MSG): Revision is not readable by the actor
- EX-2 (không MSG): Meeting is not accessible
- EX-3 (MSG405): Minutes are Secret and download requested

## UC59_GenerateMeetingMinutes
- EX-1 (MSG397): Actor is not the current Chair or Secretary
- EX-2 (MSG398): Meeting has not ended
- EX-3 (MSG399): A draft already exists

## UC60_EditMeetingMinutes
- EX-1 (MSG401): Someone else saved first
- EX-2 (MSG402): Content is invalid
- EX-3 (MSG400): Minutes can no longer be changed

## UC61_SubmitMeetingMinutesForApproval
- EX-1 (MSG425): Actor is the current Chair, not the Secretary
- EX-2 (MSG426): Meeting has not ended
- EX-3 (MSG400): Revision was already submitted or issued

## UC64_ApproveAndIssueMeetingMinutes
- EX-1 (MSG428): Actor is the current Secretary, not the Chair
- EX-2 (MSG429): Minutes are not waiting for approval
- EX-3 (MSG401): Minutes changed after the actor opened them

## UC65_ReturnMeetingMinutesForRevision
- EX-1 (MSG428): Actor is the current Secretary, not the Chair
- EX-2 (MSG429): Minutes are not waiting for approval
- EX-3 (MSG431): Reason is missing or too long

## UC76a_AttachDocumentsToVote
- EX-1 (MSG213): Vote documents can no longer be changed
- EX-2 (MSG170): Document is no longer in the Meeting
- EX-3 (MSG266): Document is already attached

## UC76b_DetachDocumentFromVote
- EX-1 (MSG213): Vote documents can no longer be changed
- EX-2 (MSG269): Document is no longer attached to the Vote
- EX-3 (MSG205): Meeting or Vote is not accessible

## UC83_CorrectIssuedMeetingMinutes
- EX-1 (MSG397): Actor is not the current Chair or Secretary
- EX-2 (MSG433): Selected revision is not issued
- EX-3 (MSG434): A correction is already in progress

## UC84a_RequestCalendarShare
- EX-1 (MSG448): Requester has no right to create meetings
- EX-2 (MSG449): A share already exists
- EX-3 (MSG443): Range is invalid

## UC84b_ShareCalendarDirectly
- EX-1 (MSG455): Recipient cannot receive a share
- EX-2 (MSG449): A share already exists
- EX-3 (MSG443): Range is invalid

## UC84c_RespondToCalendarShare
- EX-1 (không MSG): Actor is not the asked side
- EX-2 (MSG450): Share is no longer Pending
- EX-3 (MSG443): Range is invalid

## UC84d_ChangeCalendarShare
- EX-1 (không MSG): Actor is not one of the two people
- EX-2 (MSG450): Change is not allowed
- EX-3 (MSG443): Range is invalid

## UC84e_WithdrawOrEndCalendarShare
- EX-1 (không MSG): Actor is not one of the two people
- EX-2 (MSG450): Nothing to withdraw or end

## UC84f_ViewCalendarShares
- Không có EX.

## UC85a_FindMeetingTimeSlots
- EX-1 (MSG444): Search parameters are invalid
- EX-2 (MSG448): Actor has no right to create meetings

## UC85b_CreateDraftMeetingFromSlot
- EX-1 (MSG446): Slot is no longer free
- EX-2 (MSG448): Actor has no right to create meetings

## UC85c_ViewSharedCalendar
- EX-1 (MSG448): Actor cannot create meetings
