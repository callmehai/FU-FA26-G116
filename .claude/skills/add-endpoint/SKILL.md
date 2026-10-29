---
name: add-endpoint
description: Add or change a TrustCabinet API endpoint end to end — backend route, server-side authorization, MSGnn message codes, frontend type and api file, tests and docs. Use for any new or changed /api/... endpoint.
---

# Add an endpoint

An endpoint is never one file. Work through every step; skipping one is what breaks CI or leaks data.

## Before you write code
1. Read the Jira ticket (project `SCRUM`) and `docs/DEVELOPMENT_WORKFLOW.md`. Note any deviation from the ticket so it can be commented there.
2. Read `docs/02-design/02-erd.md` for the entities involved. Do not invent tables.
3. Read `docs/03-api/00-danh-muc-ma-thong-bao.md` for message codes.
4. Find a neighbouring endpoint (for example under `backend/TrustCabinet.Web`) and copy its shape, including how it checks capabilities. Do not invent a new authorization mechanism.

## Backend
- Declare the route in `Program.cs`, or in an extension method such as `MapXxxEndpoints()` if the file already splits that way.
- **Authorize server-side on every endpoint.** The UI (`RequireCapability`, sidebar) is never the security boundary.
- **List endpoints filter rows server-side.** A record the caller may not see must never reach the browser.
- Status codes:
  - not signed in: `401`
  - signed in, missing capability, fixed endpoint such as `/api/users`: `403`
  - not allowed to read one specific record (document, meeting): `404`, so existence is not leaked
- Message codes (`MSGnn`):
  - take the next free code from the catalogue ("Mã tiếp theo"), never restart numbering
  - one code means one situation; never renumber or reuse a code that reached `main`
  - declare `private const string MsgNNName = "MSGnn";` at the top of the endpoint file
  - no inline `"MSGnn"` literals
- Enums are stored as strings, keys are `Guid.CreateVersion7()`, users are deactivated not deleted, `MeetingStatusHistory` is append-only.
- Recordings are personal data and a voice used to recognise someone is sensitive biometric data (Law 91/2025/QH15, Decree 356/2025/NĐ-CP): state consent and deletion obligations explicitly. Voice embeddings live only encrypted in `voice_samples`.

## Frontend
1. `frontend/src/types/<name>.ts` — type mirroring the response.
2. `frontend/src/api/<name>.ts` — call through `api.get/post/...` from `client.ts`. Never call `fetch` from a component.
3. Match errors on the code with `isApiError`, never on message text.
4. Relative paths only (`/api/...`): no CORS, no `VITE_API_BASE_URL`, no hardcoded `localhost:5225`.
5. UI work then follows `frontend-ui-design` and `verify-ui-in-browser`.

## Tests and docs (same commit as the code)
- Add a test that asserts each new `MSGnn` code. `MessageCodeCatalogueTests` fails if a code is missing from the catalogue, asserted by no test, or written inline.
- Update `docs/03-api/` and the message-code catalogue (table row and "Mã tiếp theo").

## Verify
```bash
dotnet build TrustCabinet.sln -warnaserror
dotnet test TrustCabinet.sln
cd frontend && npm run lint && npm run build
```
Report which checks you actually ran and their result. Consider running the `authz-reviewer` agent on the change.
