# Minutes.Save — Unit (FE-12, MIN)

Description: Saving minutes content: version check, sanitising and editor permission.

Precondition: Minutes draft exists; editor actor.

## MinutesService.SaveAsync

Layer: Application · `backend/TrustCabinet.Application/Summaries/MinutesService.cs:98`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Draft revision with content and UpdatedAt T.<br>2. Actor is the current Secretary who manages the meeting; the caller read the revision at T. | minutesId: the Draft revision; actor: the current Secretary; content: "<p>Biên bản họp quý 3.</p>"; loadedAt: T. | Return: Content stored as the filtered HTML; UpdatedAt moves to now; 200 MSG396.<br>Exception: N/A<br>Log Message: Saved: content and UpdatedAt on the Draft revision; realtime minutes-changed push to the meeting's readers. | Normal | UC-60 · MSG396 · Deviation: success also emits a realtime minutes-changed push (working-tree change). |
| UTC002 | 1. Draft revision exists.<br>2. Actor is a Member who reads the meeting but is neither Chair nor Secretary. | minutesId: the Draft revision; actor: the Member; content: "<p>A.</p>"; loadedAt: T. | Return: Failure NotFound; 404 with no message code; nothing saved and nothing revealed.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-60 |
| UTC003 | 1. Revision WaitingApproval with UpdatedAt T.<br>2. Actor is the current Secretary. | minutesId: the waiting revision; actor: the current Secretary; content: "<p>A.</p>"; loadedAt: T. | Return: Failure Locked; 409 MSG400; a sent revision is no longer edited here.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-60 · MSG400 |
| UTC004 | Draft revision; actor is the current Secretary; the caller read it at T. | minutesId: the Draft revision; content: 300001 characters with text; loadedAt: T. | Return: Failure Invalid; 400 MSG402; errors[content] holds the limit message for the 300000-character limit (formatted with N0 in the process culture); nothing saved.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-60 · MSG402 · Deviation: the limit message formats the 300000 limit with N0 in the process culture, so the separator follows the server locale. |
| UTC005 | 1. Draft revision read at T, then saved by someone else.<br>2. Actor is the current Secretary. | minutesId: the Draft revision; content: "<p>A.</p>"; loadedAt: T. | Return: Failure Stale; 409 MSG401; the newer save is kept, this one is discarded.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-60 · BR-389 · MSG401 |
| UTC006 | 1. Draft revision; actor is the current Secretary; the caller read it at T.<br>2. The database fails on save. | minutesId: the Draft revision; content: "<p>A.</p>"; loadedAt: T. | Return: Failure SaveFailed; 503 MSG404; nothing saved.<br>Exception: N/A<br>Log Message: Error logged with the minutes id. | Abnormal | UC-60 · MSG404 |
