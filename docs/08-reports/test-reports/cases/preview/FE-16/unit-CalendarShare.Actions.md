# CalendarShare.Actions — Unit (FE-16, CAL)

Description: Calendar share state machine: who may request, share, respond, change, withdraw and end.

Precondition: Calendar share with known state, owner, viewer and sensitivity level.

## CalendarShareRules.Actions

Layer: Domain · `backend/TrustCabinet.Domain/CalendarSharing/CalendarShareRules.cs:122`

| ID | Pre-conditions | Inputs | Expected | Type | Note |
|---|---|---|---|---|---|
| UTC001 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Pending share started by Binh (viewer). | actor: An (owner); acceptBlocked: false. | Return: [accept, decline].<br>Exception: N/A<br>Log Message: N/A | Normal | UC-84c |
| UTC002 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Pending share started by Binh. | actor: An; acceptBlocked: true. | Return: [decline]; the share can no longer become Effective.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84c · BR-424 · BR-430 |
| UTC003 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Pending share started by Binh. | actor: Binh; acceptBlocked: true. | Return: [withdraw]; acceptBlocked only affects the asked side.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84e |
| UTC004 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Effective share; pending end 2026-11-17 is before today (no longer valid). | actor: An (owner). | Return: [decline, update, end]; the stale change can only be declined.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84c · BR-431 |
| UTC005 | 1. Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18.<br>2. Effective share, nothing pending. | actor: An (owner); acceptBlocked: true. | Return: [update, end]; acceptBlocked is ignored for an Effective share.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84d |
| UTC006 | Today is 2026-11-18; share between owner An and viewer Binh, range 2026-11-18 to 2026-12-18. | actor: a third user (not owner or viewer). | Return: [] empty.<br>Exception: N/A<br>Log Message: N/A | Abnormal | UC-84c |
| UTC007 | Share end date 2026-11-17, today is 2026-11-18 (lapsed); share status Effective. | actor: An. | Return: [] empty; a share past its end date allows nothing.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84c · BR-425 |
| UTC008 | Share end date 2026-11-18, today is 2026-11-18; share status Effective. | actor: An. | Return: [update, end]; the last day still counts.<br>Exception: N/A<br>Log Message: N/A | Boundary | UC-84d · BR-425 |
