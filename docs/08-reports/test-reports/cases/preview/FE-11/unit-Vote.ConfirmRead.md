# Vote.ConfirmRead — Unit (FE-11, VOTE)

Description: Confirming that a voter has read the attached material.

Precondition: Open vote with attached documents.

## VoteService.ConfirmReadAsync

Layer: Application · `backend/TrustCabinet.Application/Votes/VoteService.cs:258`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting InProgress; Open vote that links the document; actor is eligible and has not confirmed it. | actor: eligible Member; input.LastPageReached: true. | Return: Success: confirmation for the document with the current time, AlreadyConfirmed false; MSG253.<br>Exception: N/A<br>Log Message: One document read confirmation row saved for the user and the document, with no link to any ballot. | Normal | UC-74 · BR-289 · BR-290 · MSG253 |
| UTC002 | Same document already confirmed by the actor earlier. | Second call with input.LastPageReached: true. | Return: Success with the original confirmation time and AlreadyConfirmed true; MSG253.<br>Exception: N/A<br>Log Message: No new row written. | Normal | UC-74 · BR-291 · MSG253 |
| UTC003 | Open vote links the document but the actor is not on the fixed eligible list. | actor: Member added after the vote opened. | Return: Failure NotEligible; 409 MSG224.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 · UC-55 · BR-190 · MSG224 |
| UTC004 | Eligible voter has not reached the last page. | input.LastPageReached: false. | Return: Failure LastPageNotReached; 400 MSG254; nothing recorded.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-74 · BR-290 · MSG254 |
