# doc-audit: how to check each kind of claim

Ids are stable so a ledger and a re-audit can cite them. Skip a row that does not apply; never mark it `MATCH`.

## Text and numbers (Layer A)

| Id | Claim kind | Check |
|---|---|---|
| A-FLOW | UC normal / alternative / exception flow | Trace each step through endpoint → service → port; run the happy path and one exception on the app. Actor, order, preconditions, postconditions and the message shown at each branch all agree |
| A-RULE | business rule `BR-nn` | A place that enforces it (Domain `*Rules`/`*Policy` or a service) and a test that fails without it. A rule enforced only in the UI is `DRIFT` |
| A-MSG | message `MSGnn` | Code in `docs/03-api/00-danh-muc-ma-thong-bao.md`, same status, same text in the UI, asserted by a test, raised in the situation the document names |
| A-ROLE | role × action | Seed capabilities and the API's 401/403/404 per AGENTS.md; the sidebar and the route for the same role; a denied role really is denied |
| A-STATE | status name or transition | Enum values, the policy that allows each transition, `MeetingStatusHistory` rows, the Vietnamese label in the UI, the state machine diagram |
| A-DATA | entity, field, type, key, enum | `ModelSnapshot` and `02-erd.md`; names, nullability, string-stored enums, `Guid.CreateVersion7()` keys; what deliberately has no table |
| A-NUM | any count, size, limit, date | Recount from source (UC list, endpoint list, tests, `.trx`); a limit from the code constant, not from memory |
| A-STRUCT | layers, projects, dependency direction | The four projects and `ArchitectureTests`; Web does not name Infrastructure outside `Program.cs` |
| A-SCOPE | built / dropped / out of scope | AGENTS.md "Status" and "Watch out", `scopeData.ts`, the UI. Dropped features absent from the app and not listed as done |
| A-NFR | performance, security, availability, privacy | A measurable fact: config, CSP in `deploy/Caddyfile`, rate limit (MSG414), encryption of `voice_samples`, consent and deletion for biometric data. A claim with no mechanism is `CODE-GAP` |
| A-TRACE | requirement ↔ test | The test case exists in the workbook **and** a real test or QA run backs it; the stated result equals a recomputed one |
| A-API | endpoint contract | Route, verb, request and response shape, status codes against `docs/03-api/` and `Web/Endpoints/` |
| A-TERM | wording | Same name for the same thing across Reports 1–5, slides, UI and code identifiers |
| A-CONSIST | two documents | Same UC id, name, actor, status and count everywhere it appears; report both locations on a clash |

## Images (Layer B)

| Id | Image kind | What "matches" means |
|---|---|---|
| B-SEQ | sequence diagram | Participants are the real layers; messages follow the real call chain in order; alt/opt/loop branches exist in the code; the message codes shown are the real ones; the UC id and title match the UC list |
| B-CLASS | class diagram | Classes, attributes, types, visibility and relationships exist in Domain/Application; layers drawn where the code puts them; no class that was removed |
| B-ERD | ERD (`13-erd`, Chen and target) | Tables, columns, keys and cardinality equal `ModelSnapshot`; no table the ERD says deliberately does not exist (org, org unit, task) |
| B-STATE | state machine | States = enum, transitions = what the policy allows, guards named as in the rule; terminal states correct |
| B-UC | use case diagram | Actors and use cases equal the UC list; each actor's set equals the role's real capabilities; dropped UCs absent; include/extend correct |
| B-BF | business flow | Steps and swimlanes match the real workflow and the roles; hand-offs between roles exist in the app |
| B-CTX | context diagram | External systems are the real ones (LiveKit, SeaweedFS/R2, speech worker, local LLM, Google sign-in, email) and none that was dropped (eOffice, HSM) |
| B-ARCH | architecture / package diagram | Deployed pieces and project dependencies equal `deploy/` and the `.csproj` references; direction of every arrow |
| B-FLOW | screen flow | Every screen and transition exists as a route; a restricted route is not drawn as reachable by a role that gets 404 |
| B-WIRE | wireframe | Same functions and fields as the built screen; layout may differ, functions may not |
| B-SHOT | app screenshot | A fresh screenshot of the same screen, role and data shows the same labels, buttons, columns, statuses and Vietnamese text; sidebar and badges as built; no debug UI |
| B-SLIDE | slide image or table | Same facts as the source slice it quotes; numbers equal the recounted ones |

Every image also passes **B-FIT**: legible at report width and when printed, not cropped, no overlap or dangling arrow,
correct UML notation, figure number and caption present and cited from the text, no secrets or real personal data.

## Staleness

`STALE` when the image is older than a source change that affects it: compare the last commit touching the source
(`.drawio`, `.txt`, the screen's `.tsx`, the entity) with the image's, then confirm by re-exporting or re-shooting into
the scratchpad and comparing. A re-export that looks different from the submitted PNG is a finding even if both look
plausible.

## Per-slice emphasis

- **R3:FE-xx:** A-FLOW, A-RULE, A-MSG, A-ROLE, A-STATE per UC; B-SEQ and B-SHOT for the UCs of the Feature; A-SCOPE.
- **R4:** A-STRUCT, A-DATA, A-API; all of B-CLASS, B-SEQ, B-ERD, B-STATE, B-ARCH.
- **R5:** A-TRACE, A-NUM (pass/fail/total recomputed), no case whose steps contradict the UC, no case marked passed that
  no run backs; the sheet's role and expected message equal A-ROLE and A-MSG.
- **R1/R2:** A-SCOPE, A-NUM, A-TERM; dates and sprint facts against Jira and git.
- **SL:** everything the slides state is re-verified in its source slice; the demo path runs on the test server.
