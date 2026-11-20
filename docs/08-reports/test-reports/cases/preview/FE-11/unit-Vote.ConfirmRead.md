# Vote.ConfirmRead — Unit (FE-11, VOTE)

Description: Confirming that a voter has read the attached material.

Precondition: Open vote with attached documents.

## VoteService.ConfirmReadAsync

Layer: Application · `backend/TrustCabinet.Application/Votes/VoteService.cs:258`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting InProgress; Open vote that links the document; actor is eligible and has not confirmed it. | actor: eligible Member; input.LastPageReached: true. | Return: Success: confirmation for the document with the current time, AlreadyConfirmed false; MSG253.<br>Exception: N/A<br>Log Message: One document read confirmation row saved for the user and the document, with no link to any ballot. | Normal | UC-74 · BR-289 · BR-290 · MSG253 |
| UTC002 | Same document already confirmed by the actor earlier. | Second call with input.LastPageReached: true. | Return: Success with the original confirmation time and AlreadyConfirmed true; MSG253.<br>Exception: N/A<br>Log Message: No new row written. | Normal | UC-74 · BR-291 · MSG253 |
| UTC003 | Another Open or Closed vote links the same document and the actor confirmed it for the first vote. | actor confirms again via the second vote. | Return: Success with the first confirmation time, AlreadyConfirmed true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-74 · BR-291 |
| UTC004 | Actor's account is deactivated. | meetingId, meetingDocumentId, actor, input. | Return: Failure Inactive; 403 with no message code.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 |
| UTC005 | Meeting the actor cannot read. | actor: stranger. | Return: Failure DocumentUnavailable; 404 MSG156.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 · MSG156 |
| UTC006 | Document removed from the meeting, or linked only to a vote the actor may not see (Draft vote, actor is a Member). | actor: Member. | Return: Failure DocumentUnavailable; 404 MSG156, the same answer as for a missing meeting.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 · BR-275 · MSG156 |
| UTC007 | Open vote links the document but the actor is not on the fixed eligible list. | actor: Member added after the vote opened. | Return: Failure NotEligible; 409 MSG224.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 · UC-55 · BR-190 · MSG224 |
| UTC008 | Vote linking the document is Closed. | actor: eligible Member. | Return: Failure NotAccepting; 409 MSG225; nothing recorded.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 · UC-55 · MSG225 |
| UTC009 | Open vote but the meeting has Ended. | actor: eligible Member. | Return: Failure NotAccepting; 409 MSG225.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 · UC-55 · MSG225 |
| UTC010 | Eligible voter has not reached the last page. | input.LastPageReached: false. | Return: Failure LastPageNotReached; 400 MSG254; nothing recorded.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 · BR-290 · MSG254 |
| UTC011 | Eligible voter, body without the lastPageReached field. | input.LastPageReached: null. | Return: Failure LastPageNotReached; 400 MSG254.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-74 · BR-290 · MSG254 |
| UTC012 | Request body sent with a content type other than JSON. | input.ParseFailure: UnsupportedMedia. | Return: Failure UnsupportedMedia; 415 MSG34.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 · MSG34 |
| UTC013 | Request body larger than 64 KiB. | input.ParseFailure: TooLarge. | Return: Failure TooLarge; 413 MSG87.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 · MSG87 |
| UTC014 | Two confirmations by the same person arrive at once; the database keeps the first. | Concurrent duplicate. | Return: Success with the stored confirmation time, AlreadyConfirmed true; MSG253.<br>Exception: N/A<br>Log Message: Second write discarded. | Normal | UC-74 · BR-291 · MSG253 |
| UTC015 | The database fails while saving the confirmation. | input.LastPageReached: true. | Return: Failure SaveFailed; 503 MSG255.<br>Exception: N/A<br>Log Message: Error logged with document and meeting ids. | Abnormal | UC-74 · MSG255 |
