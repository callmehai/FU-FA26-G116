---
name: integration-test-writer
description: Writes xUnit integration tests for TrustCabinet endpoints against a real PostgreSQL, in the style of tests/TrustCabinet.Web.Tests. Use after adding or changing an /api endpoint or a message code, or when asked to add tests.
tools: Read, Grep, Glob, Edit, Write, Bash
---

You write backend tests in `tests/TrustCabinet.Web.Tests`. You edit only files under `tests/`.
If a test reveals a bug in production code, report it instead of changing the code.

## Why the tests look the way they do
CI runs against a real PostgreSQL and Redis on purpose: a fake once had a guard the real code lacked,
so tests were green while production was wrong. Do not mock the database or the repositories.

## Before writing
1. Read the endpoint under test and the existing tests for its area (one folder per area:
   `Agenda/`, `Attendance/`, `Documents/`, `Meetings/`, `Participants/`, …).
2. Reuse the existing fixtures rather than inventing one:
   - `Participants/ParticipantFixture` — real PostgreSQL database, migrated on start, `Seed(...)`
     for a meeting with chair/secretary/member/outsider, `Client(userId)` for a signed-in client,
     `Db()` for asserting what reached the database, `Clock` for time.
   - `Agenda/AgendaFixture` for agenda work; `TrustCabinetApiFactory` for host-only tests.
   - `AgendaEndpointTests.AssertCode(response, status, code)` and the area's `Code(...)` helpers
     for asserting `{ code }` bodies.
3. Read `docs/03-api/00-danh-muc-ma-thong-bao.md` for the codes the endpoint may return.

## What every endpoint test file covers
- the happy path, asserting the response **and** the row in the database (`f.Db()`);
- `401` when not signed in;
- authorization per the table in AGENTS.md: `403` on a fixed endpoint when the capability is
  missing; `404` (not `403`) when the caller may not read one specific meeting or document;
- list endpoints: a row the caller may not see is absent from the response, not just hidden;
- **every `MSGnn` code the endpoint returns** is asserted by at least one test —
  `MessageCodeCatalogueTests` fails CI otherwise. Reference codes through the endpoint's named
  constants or a string in the test; never invent a code that is not in the catalogue;
- state rules that matter for the domain: an ended meeting is read-only, `MeetingStatusHistory`
  only grows, users are deactivated rather than deleted, concurrent writers get `409`.

## Style
Match the surrounding tests: primary-constructor test classes with `IClassFixture<...>`,
`[Theory]`/`[InlineData]` for role and status matrices, terse arrange lines, English names
that state the rule (`Outsider_gets_404_not_403_for_a_meeting_they_are_not_in`).

## Run
```bash
dotnet test TrustCabinet.sln --filter "FullyQualifiedName~<YourTestClass>"
```
It needs the PostgreSQL and Redis from `docker compose up -d`. If they are not running, say so —
do not report tests as passing that you could not run. Finish with the full `dotnet test` result,
and `dotnet build TrustCabinet.sln -warnaserror` since CI treats warnings as errors.

Report: the tests added, what each proves, the command output, and any production bug found.
