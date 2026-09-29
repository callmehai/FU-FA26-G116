# Development Workflow

How the SEP490-G116 team builds TrustCabinet. Read this before starting any feature — it is
written for both human developers and AI coding agents, and it is the answer to "how does this
team work?".

This file is in English by deliberate exception to the language rules in
[AGENTS.md](../AGENTS.md), because AI coding agents read it as their operating instructions.
Everything else in `docs/` stays Vietnamese.

---

## 1. The shape of the project

| Fact | Value |
|---|---|
| Duration | 12 weeks |
| Sprint length | 1 week = 1 Sprint |
| Source of truth for requirements | Jira (project `SCRUM`, <https://sep490g116.atlassian.net>) |
| Source of truth for scope | [README.md](../README.md) → *Phạm vi chức năng dự kiến* and *Không làm* |
| Source of truth for design decisions | [docs/05-decisions/](05-decisions/) (ADRs) |
| Jira statuses | `To Do` → `Business Analyst` → `Backend` → `Frontend` → `BA Final Review` → `Done` |
| Roles | BA · BE · FE · Tester |

Jira holds requirements, user stories/use cases, tasks, bugs and Sprint planning. If Jira and a
document disagree, that is a conflict to raise — not something to silently resolve in code.

---

## 2. The pipeline

We do **not** write all BA documentation first and then start development. Work flows
continuously, one Use Case at a time.

```
Jira Requirement
    ↓
Use Case
    ↓
BE: basic UC Specification + Sequence Diagram        (Jira: Backend)
    ↓
BE: Backend implementation — changes noted in Jira
    ↓
FE: Frontend integration — changes noted in Jira     (Jira: Frontend)
    ↓
BA: reconcile docs with what was built               (Jira: BA Final Review)
    ↓
Tester: Test → Bug Fix / Retest
    ↓
Review
    ↓
Done
```

**Documentation follows the code, then BA settles it.** BE writes the basic UC Specification and
Sequence Diagram first, just enough to build against. While implementing, BE and FE change
whatever the Use Case turns out to need and record each change in the Jira ticket. Only after FE
has integrated does the ticket go back to BA, who brings the specification, diagrams and business
rules in line with what was built. See [§2.1](#21-changing-a-requirement-during-implementation).

**This is a pipeline, not phases.** Different Use Cases sit at different stages at the same time.
A hand-off happens as soon as one Use Case is ready, never when the whole Sprint is ready.

Example of a normal week:

| | Mon–Tue | Wed–Thu | Fri |
|---|---|---|---|
| **BE** | UC01 basic docs + implementation | UC02 basic docs + implementation | UC03 basic docs |
| **FE** | — | UC01 integration | UC02 integration |
| **BA** | clarify UC02, UC03 in Jira | — | UC01 final review |
| **Tester** | prepare test cases | prepare test cases | UC01 testing |

Rules that follow from this:

- BE starts a Use Case **as soon as the Jira requirement is understood**, writing the basic docs
  itself rather than waiting for BA.
- FE starts **as soon as the API it needs is usable**, not when all of BE is finished.
- Tester starts **as soon as a feature is usable**, not at the end of the Sprint.
- Never block the whole team waiting for every Use Case to be finished at one stage.

### 2.1 Changing a requirement during implementation

BE or FE may find that the Use Case needs something the Jira ticket or the basic docs did not say:
a field that has to be optional, an extra error branch, an endpoint FE turns out to need.

- **Make the change.** Do not stop and wait for BA.
- **Note it in the Jira ticket** as a comment, in the same working session: what changed, why, and
  which UC Specification, diagram or business rule is now out of date. This note is what BA works
  from in `BA Final Review`; a change that is not in the ticket gets lost.
- Keep the API contract in [docs/03-api/](03-api/) current in the same PR — FE builds against it.
- The UC Specification, the diagrams and the business rules may lag behind the code until
  `BA Final Review`. BE/FE may edit them, but are not required to.
- **Do not ask BA to update documentation while the ticket is in `Backend` or `Frontend`.** BA
  updates it once, at the end, with everything that changed.

This freedom is about the Use Case's own details. Scope boundaries (§8), the security rules in
[AGENTS.md](../AGENTS.md) and accepted ADRs still hold; changing those needs a team decision.

---

## 3. Sprint planning

Once a week, before the Sprint starts:

1. Review the Jira backlog.
2. Pick a realistic set of Use Cases/features. **Do not put the whole backlog into one Sprint.**
3. Write a Sprint Goal — one sentence describing what will demonstrably work at the end.
4. Identify dependencies and blockers between Use Cases and between roles.
5. Drop any Use Case that is not ready. A Use Case with unclear requirements does not go into
   development; it goes back to BA for clarification.

A Use Case is ready for the Sprint when the requirement is understood, the scope is agreed, and
nothing external is blocking it.

---

## 4. Responsibilities

### 4.1 BA

Before development (Jira `Business Analyst`):

- Make sure the Jira requirement itself is clear enough for BE to start. An unclear requirement
  is BA's to clarify; the basic docs are BE's to write.

After FE integration (Jira `BA Final Review`):

- Read the change notes BE and FE left in the ticket.
- Bring the UC Specification, the Sequence Diagram, the Class Diagram and the business rules in
  line with what was built.
- Check that specification and diagrams agree with each other and with the code.
- If a change noted in the ticket is not acceptable as a requirement, reopen it with BE/FE rather
  than documenting around it.

BA documentation describes **business and system behaviour**, not implementation details. It says
what happens and under which rules, not which C# class or React component does it.

Diagrams and specs live in [docs/02-design/](02-design/); diagram sources and exported images both
go in [docs/assets/](assets/).

### 4.2 Backend (BE)

When a Use Case moves to `Backend`:

- Read the Jira requirement and any existing docs for the Use Case.
- Write the basic UC Specification and Sequence Diagram — enough to build against, not final.
- Identify: API endpoints, business rules, data requirements, validation, dependencies.
- Implement the backend. Where it needs to differ from the ticket or the basic docs, change it
  and note the change in the ticket ([§2.1](#21-changing-a-requirement-during-implementation)).
- Provide or update the API contract FE needs — see [docs/03-api/](03-api/).
- Test the backend (`dotnet test`).
- **Notify FE** when the API is ready to integrate against.
- If the Jira requirement itself is unclear, ask. Do not guess silently.

Every endpoint re-authorizes server-side and list endpoints filter rows server-side. The UI is
never the security boundary — see [AGENTS.md](../AGENTS.md).

### 4.3 Frontend (FE)

When the API is sufficiently ready:

- Read the UC Specification and understand the expected user flow.
- Implement the UI/UX for the Use Case.
- Integrate with the BE APIs through `src/api/` — never `fetch` directly from a component.
- Handle all four states: loading, success, validation error, server error.
- Test the flow locally against the running backend.
- Note any change to the requirement in the ticket, the same way BE does
  ([§2.1](#21-changing-a-requirement-during-implementation)).
- Move the ticket to `BA Final Review` and hand the finished feature to Tester.

### 4.4 Tester

- Prepare test cases from the requirement and the UC Specification.
- Test the completed FE/BE functionality.
- Cover the happy path **and** the important edge and error cases.
- Report bugs in Jira with enough detail to reproduce: steps, expected, actual, environment.
- Retest after fixes.
- Do not mark a feature Done while important bugs remain open.

---

## 5. Review

At the end of each Sprint:

- Review the Use Cases that were completed.
- **Demo working functionality**, not diagrams and not code.
- Review unfinished work and move it deliberately into the next Sprint.
- Review bugs and technical issues that came up.
- Short retrospective:
  - What went well?
  - What went wrong?
  - What changes in the next Sprint?

---

## 6. Definition of Done

A Use Case is Done only when **all** of these hold:

- [ ] BA documentation is complete, internally consistent, and matches what was built — settled in
      `BA Final Review` from the ticket's change notes.
- [ ] BE implementation is complete.
- [ ] FE implementation/integration is complete, where the Use Case has a UI.
- [ ] Testing has been completed.
- [ ] Important/critical bugs are resolved.
- [ ] The feature has been reviewed.
- [ ] Jira status and the related documentation are updated.

"The code is merged" is not Done.

---

## 7. Working in the repository

| Check | Command |
|---|---|
| Backend build | `dotnet build TrustCabinet.sln` |
| Backend tests | `dotnet test TrustCabinet.sln` |
| Frontend lint | `cd frontend && npm run lint` |
| Frontend build (includes `tsc -b`) | `cd frontend && npm run build` |

CI runs all four on every pull request — see [.github/workflows/ci.yml](../.github/workflows/ci.yml).

Branch and PR conventions:

- Branch off `main`, one branch per Use Case or task.
- Commit messages in Vietnamese, PR title and description in English, PR review comments in
  Vietnamese. Full table in [AGENTS.md](../AGENTS.md).
- Open a PR and wait for a review before merging. Do not merge your own PR unreviewed.
- When a change affects the API contract, an ADR or the project scope, update `docs/` **in the
  same commit** as the code. The UC Specification, diagrams and business rules may wait for
  `BA Final Review` ([§2.1](#21-changing-a-requirement-during-implementation)).

---

## 8. Design and documentation rules

- The **Context Diagram** describes the system boundary, actors and external systems, and
  high-level data flows. It does **not** need to enumerate every entity or class in the system.
- Detailed entities and classes are refined progressively — through requirements, Use Cases,
  Sequence Diagrams, data requirements, and implementation. Do not try to finish the data model in
  one sitting.
- **Do not invent requirements to make a diagram look complete.** When implementation forces a
  decision the ticket did not make, make it and record it in the ticket for BA — a decision nobody
  wrote down is the problem, not the decision itself.
- A significant technical decision becomes an ADR in [docs/05-decisions/](05-decisions/). Never
  edit an accepted ADR — write a new one that supersedes it.

### Scope boundaries

- **Do not add task management or action-item tracking to TrustCabinet** unless Jira and the
  project scope explicitly require it. TrustCabinet stops at the approved minutes; tracking
  execution belongs to a different system. See *Không làm* in [README.md](../README.md).
- Do not introduce features that are not in the current project scope. The deliberately excluded
  list is in the README.

### How AI is used in the minutes flow

The current meeting-minutes flow is: uploaded audio/MP3 → speech-to-text → secretary notes →
AI-generated draft minutes → decisions and voting results.

- **The AI produces a draft.** The secretary reviews, edits and finalizes the minutes and remains
  responsible for them.
- All AI-generated content must be visibly marked as AI-generated.
- Never describe or build the AI as independently making final decisions. Minutes are a legally
  meaningful document and a person must be accountable for them.
- Recordings and transcripts are personal data under Decree 13/2023/NĐ-CP. When touching this
  area, state the consent and deletion obligations explicitly rather than silently persisting the
  data.

---

## 9. Rules for AI coding agents

### Before modifying code

1. Read this file.
2. Read the relevant Jira/task context, if available.
3. Inspect the existing code structure.
4. Understand the current architecture and conventions ([AGENTS.md](../AGENTS.md)).
5. Do not redesign unrelated parts of the system.

### When implementing a Use Case

- Stay within that Use Case's scope.
- Start from the Jira ticket and the existing docs. If there are no docs yet, write the basic UC
  Specification and Sequence Diagram first.
- If the Use Case needs to differ from the ticket or the docs, change it — but never silently: list
  the change so it can be noted in the Jira ticket (§2.1), and tell the user.
- Do not invent APIs, entities, fields or business rules that the Use Case does not need.
- Reuse existing project patterns instead of introducing new ones.
- Avoid refactoring that the task does not require.
- Keep changes focused and reviewable — one Use Case per branch.

### When reviewing a pull request

- Judge the code against the Jira ticket **and the change notes in it**, not only against the BA
  docs. A difference that is noted in the ticket is an accepted change, not a defect.
- Do not request changes because the UC Specification, diagrams or business rules are behind the
  code, and do not ask for BA to update them before merge — that happens in `BA Final Review`.
- **Do** flag a requirement change that appears in the code but is not noted in the ticket; the
  author must add the note.
- Still flag docs the author wrote that contradict themselves, and an API contract in
  [docs/03-api/](03-api/) that does not match the code.

### Never

- Never treat the mocked browser-side auth (`features/auth/`, `types/user.ts`) or
  `features/meetings/sampleData.ts` as working authentication or real data.
- Never edit `backend/TrustCabinet.Web/wwwroot/` — it is build output.
- Never copy source files from the AI-eCabinet reference repository into this one.
- Never commit connection strings, API keys or credentials. Use `dotnet user-secrets` locally.

---

**Do not modify this document unless the team explicitly changes the development process.**
