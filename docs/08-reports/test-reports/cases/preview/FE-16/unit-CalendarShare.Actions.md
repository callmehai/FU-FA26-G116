# CalendarShare.Actions — Unit (FE-16, CAL)

Description: Calendar share state machine: who may request, share, respond, change, withdraw and end.

Precondition: Calendar share with known state, owner, viewer and sensitivity level.

## CalendarShareRules.Actions

Layer: Domain · `backend/TrustCabinet.Domain/CalendarSharing/CalendarShareRules.cs:122`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Pending share started by Binh (viewer). | actor: An (owner); acceptBlocked: false. | Return: [accept, decline].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-84c |
| UTC002 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Pending share started by Binh. | actor: An; acceptBlocked: true. | Return: [decline]; the share can no longer become Effective.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84c · BR-424 · BR-430 |
| UTC003 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Pending share started by Binh. | actor: Binh (sender); acceptBlocked: false. | Return: [withdraw].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-84e |
| UTC004 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Pending share started by Binh. | actor: Binh; acceptBlocked: true. | Return: [withdraw]; acceptBlocked only affects the asked side.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84e |
| UTC005 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Pending share started by An (direct share). | actor: Binh (asked side); acceptBlocked: false. | Return: [accept, decline].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-84c |
| UTC006 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Pending share started by An. | actor: An (sender); acceptBlocked: false. | Return: [withdraw].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-84e |
| UTC007 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Effective share, nothing pending. | actor: An (owner). | Return: [update, end].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-84d · UC-84e |
| UTC008 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Effective share, nothing pending. | actor: Binh (viewer). | Return: [requestChange, end].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-84d · UC-84e |
| UTC009 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Effective share; Binh asked for Level2 until 2026-12-30. | actor: An (owner). | Return: [accept, decline, update, end].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-84c · UC-84d |
| UTC010 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Effective share; pending end 2026-11-17 is before today (no longer valid). | actor: An (owner). | Return: [decline, update, end]; the stale change can only be declined.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84c · BR-431 |
| UTC011 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Effective share; Binh asked for a change. | actor: Binh (viewer). | Return: [withdraw, end].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-84e |
| UTC012 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Effective share, nothing pending. | actor: An (owner); acceptBlocked: true. | Return: [update, end]; acceptBlocked is ignored for an Effective share.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84d |
| UTC013 | Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18. | actor: a third user (not owner or viewer). | Return: [] empty.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84c |
| UTC014 | Share end date 2026-11-17, today is 2026-11-18 (lapsed); share status Effective. | actor: An. | Return: [] empty; a share past its end date allows nothing.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84c · BR-425 |
| UTC015 | Share end date 2026-11-18, today is 2026-11-18; share status Effective. | actor: An. | Return: [update, end]; the last day still counts.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84d · BR-425 |
| UTC016 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Share status Declined. | actor: An. | Return: [] empty.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84c |
| UTC017 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Share status Stopped. | actor: Binh. | Return: [] empty.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84e · BR-424 |
| UTC018 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Share status Ended. | actor: An. | Return: [] empty.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84e |
| UTC019 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Share status Withdrawn. | actor: Binh. | Return: [] empty.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84e |
