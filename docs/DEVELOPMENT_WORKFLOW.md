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
| Jira statuses | `To Do` → `In Progress` → `In Review` → `Done` |
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
BA: UC Specification
    ↓
BA: Sequence Diagram
    ↓
BA: Update Class Diagram
    ↓
BE: Backend implementation
    ↓
FE: Frontend implementation
    ↓
Tester: Test
    ↓
Bug Fix / Retest
    ↓
Review
    ↓
Done
```

**This is a pipeline, not phases.** Different Use Cases sit at different stages at the same time.
A hand-off happens as soon as one Use Case is ready, never when the whole Sprint is ready.

Example of a normal week:

| | Mon–Tue | Wed–Thu | Fri |
|---|---|---|---|
| **BA** | UC01 spec + diagrams | UC02 spec + diagrams | UC03 spec + diagrams |
| **BE** | — | UC01 implementation | UC02 implementation |
| **FE** | — | — | UC01 implementation |
| **Tester** | prepare test cases | prepare test cases | UC01 testing |

Rules that follow from this:

- BA hands a Use Case to BE **as soon as that one Use Case is sufficiently defined**.
- FE starts **as soon as the API it needs is usable**, not when all of BE is finished.
- Tester starts **as soon as a feature is usable**, not at the end of the Sprint.
- Never block the whole team waiting for every Use Case to be finished at one stage.

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

For each Use Case:

- Understand the requirement from Jira.
- Clarify anything ambiguous **before** implementation starts, not during it.
- Write the UC Specification.
- Create or update the Sequence Diagram.
- Update the Class Diagram where the Use Case changes it.
- Check that specification and diagrams agree with each other.
- Hand the Use Case to BE as soon as it is sufficiently defined. Do not wait for other Use Cases.

BA documentation describes **business and system behaviour**, not implementation details. It says
what happens and under which rules, not which C# class or React component does it.

Diagrams and specs live in [docs/02-design/](02-design/); diagram sources and exported images both
go in [docs/assets/](assets/).

### 4.2 Backend (BE)

When a Use Case arrives from BA:

- Read the UC Specification, the Sequence Diagram and the relevant part of the Class Diagram.
- Identify: API endpoints, business rules, data requirements, validation, dependencies.
- Implement the backend.
- Keep the implementation consistent with the BA documentation.
- Provide or update the API contract FE needs — see [docs/03-api/](03-api/).
- Test the backend (`dotnet test`).
- **Notify FE** when the API is ready to integrate against.
- If the requirement or the BA documentation is unclear, ask. Do not guess silently.

Every endpoint re-authorizes server-side and list endpoints filter rows server-side. The UI is
never the security boundary — see [AGENTS.md](../AGENTS.md).

### 4.3 Frontend (FE)

When the API is sufficiently ready:

- Read the UC Specification and understand the expected user flow.
- Implement the UI/UX for the Use Case.
- Integrate with the BE APIs through `src/api/` — never `fetch` directly from a component.
- Handle all four states: loading, success, validation error, server error.
- Test the flow locally against the running backend.
- Hand the finished feature to Tester.

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

- [ ] BA documentation is complete and internally consistent.
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
- When a change affects design or scope, update `docs/` **in the same commit** as the code.

---

## 8. Design and documentation rules

- The **Context Diagram** describes the system boundary, actors and external systems, and
  high-level data flows. It does **not** need to enumerate every entity or class in the system.
- Detailed entities and classes are refined progressively — through requirements, Use Cases,
  Sequence Diagrams, data requirements, and implementation. Do not try to finish the data model in
  one sitting.
- **Do not invent requirements to make a diagram look complete.** A gap in a diagram is a question
  for BA, not a blank to fill in.
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
- Follow the BA specification and diagrams.
- If the code and the documentation conflict, **flag the conflict**. Do not silently change the
  requirement to match the code, or the code to match a document you suspect is stale.
- Do not invent APIs, entities, fields or business rules without evidence in Jira or in `docs/`.
- Reuse existing project patterns instead of introducing new ones.
- Avoid refactoring that the task does not require.
- Keep changes focused and reviewable — one Use Case per branch.

### Never

- Never treat the mocked browser-side auth (`features/auth/`, `types/user.ts`) or
  `features/meetings/sampleData.ts` as working authentication or real data.
- Never edit `backend/TrustCabinet.Web/wwwroot/` — it is build output.
- Never copy source files from the AI-eCabinet reference repository into this one.
- Never commit connection strings, API keys or credentials. Use `dotnet user-secrets` locally.

---

**Do not modify this document unless the team explicitly changes the development process.**
