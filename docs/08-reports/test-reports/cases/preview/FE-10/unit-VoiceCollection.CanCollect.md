# VoiceCollection.CanCollect — Unit (FE-10, TRANS)

Description: Collecting voice samples from a meeting: who may collect, add, hear and delete.

Precondition: Meeting with participants and consent.

## VoiceCollectionService.CanCollect

Layer: Application · `backend/TrustCabinet.Application/VoiceId/VoiceCollectionService.cs:37`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | Meeting status is Published. | status: Published. | Return: true; the people are gathered or about to be, so samples may be taken.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-86b · BR-451 |
| UTC002 | Meeting status is InProgress. | status: InProgress. | Return: true.<br>Exception: N/A<br>Log Message: N/A | Normal | UC-86b · BR-451 |
| UTC003 | Meeting status is Ended. | status: Ended. | Return: false; the gathering is over.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86b · BR-451 · MSG477 |
| UTC004 | Meeting status is Cancelled. | status: Cancelled. | Return: false.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-86b · BR-451 |
