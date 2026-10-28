# AGENTS.md

Guidance for AI coding agents working in this repository — Claude Code, Codex, or any
other. `CLAUDE.md` just points here, so this file is the single source of truth.

## Project

TrustCabinet — an AI-assisted paperless meeting system with built-in voice/video calling.
Capstone project **SEP490-G116**. The business domain is modelled on AI-eCabinet (SmartMeet), but
this is an independent .NET implementation with a scope trimmed to fit a capstone.

**Status: v0.1.0 — skeleton plus the full data model.** There is a frontend/backend skeleton,
one `/api/health` endpoint, and a 27-table EF Core model covering every story in the Jira backlog:
access, meetings, agenda, documents, voting, recordings, transcripts, minutes, notifications,
audit and calls. There is still **no authentication and no business endpoint** — the tables exist,
nothing reads or writes them yet. Do not assume domain code exists somewhere — check or ask first.

The model is documented in [docs/02-design/02-erd.md](docs/02-design/02-erd.md), which also lists
what deliberately has no table (task tracking, organisations, org units). Read it before adding
an entity.

**Before working on any feature, read [docs/DEVELOPMENT_WORKFLOW.md](docs/DEVELOPMENT_WORKFLOW.md)
and follow the team's Sprint, BE → FE → BA Final Review → Tester workflow.** It defines how a Use
Case moves from Jira to Done, what each role hands over, and what Done means. Jira (project
`SCRUM`) is the source of truth for requirements.

**Frontend work** follows the project-specific skills in [.claude/skills/](.claude/skills/):
[frontend-ui-design](.claude/skills/frontend-ui-design/SKILL.md) for how a screen is designed and
built, [verify-ui-in-browser](.claude/skills/verify-ui-in-browser/SKILL.md) for running the app
and checking the change in a real browser before the PR,
[check-responsive](.claude/skills/check-responsive/SKILL.md) for checking every change on a phone
(390px, touch) as well as at 1200px and 1440px, and
[minimize-text](.claude/skills/minimize-text/SKILL.md) for keeping each screen to the words people
scan for, with the rest in tooltips, and [sloplint](.claude/skills/sloplint/SKILL.md)
(phòng chống AI slop) for catching the generated look — gradients, glows, endless motion, KPI
tiles, eyebrows, side stripes and the other tells catalogued from published research and the big
design systems. Claude Code loads them as skills; any other agent reads the files directly.
**Skill location:** the project skills live in `.claude/skills/` (canonical — Claude Code loads
them) and are mirrored verbatim into `.agents/skills/`, which other agents load from; edit the
canonical copy and mirror it in the same change (`SkillMirrorTests` fails CI otherwise). `.agents/skills/` additionally holds the two
third-party skills, drawio-skill and ui-ux, which exist nowhere else.

**sloplint in this repo.** The skill is [sloplint](https://github.com/callmehai/sloplint) `v0.3.0`,
vendored unchanged (see its `VENDORED.md`). Run its CLI as `npx -y github:callmehai/sloplint#v0.3.0
<scan|diff|static>` from the repository root wherever the skill says `node <skill>/checker/cli.mjs`:
the repo has no root `node_modules`, and `npx` brings Playwright with it. `.slop.json` declares the
exceptions our ADRs mandate (sidebar gradient, uppercase column headings, sensitivity icons, the
call room's Meet pills); pass `--setup .slop.login.mjs` so each page signs in as a seeded user, and
add `#tab=<tab label>`, `#open=notifications` or `#click=<button name>` to a URL to measure a
detail tab, the notification panel or a dialog. A list that opens filtered to one status repeats it on every row, so scan it
with `?status=all`. Lucide icons, Be Vietnam Pro and the blue `--brand` are project choices, not
tells. The third-party [evondev ui-ux skill](.agents/skills/ui-ux/SKILL.md)
is available as additional guidance; project rules here and `frontend-ui-design` take precedence.
**Diagram and architecture work** uses [drawio-skill](.agents/skills/drawio-skill/SKILL.md)
for creating, editing, and validating `.drawio` models (such as the ERD), following
[DRAWIO_EDITING_RULES.md](DRAWIO_EDITING_RULES.md).

### Draw.io Editing Rules

When editing any `.drawio` or `.dio` file:

- Read and follow [DRAWIO_EDITING_RULES.md](DRAWIO_EDITING_RULES.md).
- Use the existing [drawio-skill](.agents/skills/drawio-skill/SKILL.md).
- Preserve graph semantics, IDs, source/target references, UML relationships, and file structure
  unless the user explicitly requests a semantic change.
- Make targeted edits; do not regenerate the full diagram unless explicitly requested.
- Render and inspect the result, fix visual defects, and validate the graph before reporting completion.

## Language rules

| Where | Language |
|---|---|
| Code comments, docstrings, identifiers | **English** |
| PR titles and PR descriptions | **English** |
| PR review comments / discussion | Vietnamese |
| Commit messages | Vietnamese (matches the existing history) |
| UI text, user-facing strings | Vietnamese — it is a Vietnamese product |
| `docs/`, `README.md` | Vietnamese |
| `docs/DEVELOPMENT_WORKFLOW.md` | **English** — exception: AI coding agents read it as instructions |
| `.claude/skills/`, `.agents/skills/` | **English** — same exception |

Keep English technical terms as-is inside Vietnamese text (*endpoint*, *migration*, *hub*,
*proxy*) rather than half-translating them.

## Layout

```
backend/TrustCabinet.Domain/         entities and enums — no EF Core dependency
backend/TrustCabinet.Infrastructure/ DbContext, entity configurations, migrations, seed
backend/TrustCabinet.Web/    ASP.NET Core 10 (Minimal API)
  Program.cs                 the whole pipeline and every endpoint currently lives here
  wwwroot/                   GENERATED by `npm run build` — gitignored, never edit by hand
frontend/                    React 19 + Vite + TypeScript, split by feature
  src/app/App.tsx            the route table
  src/lib/routes.ts          central route map — build every path from here
  src/features/<name>/       one folder per domain area: auth, meetings, access, calendar
  src/components/            shell and shared UI (AppShell, Select, Badge, Icon…)
  src/api/                   HTTP layer (client.ts) plus one file per endpoint group
  src/types/                 shared types, mirroring backend response shapes
  src/styles/global.css      design tokens + all styling
docs/                        capstone documentation — see docs/README.md
deploy/                      shared test server on AWS Lightsail (Docker Compose + Caddy), auto-deployed by .github/workflows/deploy.yml — see deploy/README.md
Dockerfile                   one image: frontend build + backend, used by deploy/
TrustCabinet.sln
global.json                  pins .NET SDK 10.0.401
```

Every signed-in screen sits in one shell with one vertical blue sidebar — no app bar, no workspace
switcher; the `/mgmt` and `/calendar` URL prefixes are kept so old links still work. See
[ADR-0012](docs/05-decisions/0012-giao-dien-hanh-chinh-sidebar-doc.md) for the layout and palette,
and [ADR-0002](docs/05-decisions/0002-khung-giao-dien-theo-ai-ecabinet.md) for what was taken from
the AI-eCabinet reference and why.

## Running in development

Requires **.NET SDK 10**, **Node.js**, and a **PostgreSQL** the backend can reach — `docker compose
up -d` brings one up. The connection string is not in the repo: set it with `dotnet user-secrets`,
per [docs/02-design/03-co-so-du-lieu.md](docs/02-design/03-co-so-du-lieu.md). Without it the
backend refuses to start, on purpose.

Two processes, two terminals:

```bash
dotnet run --project backend/TrustCabinet.Web   # :5225 — API
cd frontend && npm run dev                      # :5173 — UI with hot reload
```

Uploading meeting documents also needs the SeaweedFS container that `docker compose up -d` starts and
the `FileStorage:*` user-secrets ([03-co-so-du-lieu.md §5](docs/02-design/03-co-so-du-lieu.md#5-kho-tệp-tài-liệu));
without them the app still runs and uploads answer `503/MSG166`. The test server stores the bytes on
Cloudflare R2 ([ADR-0024](docs/05-decisions/0024-luu-file-tai-lieu-tren-cloudflare-r2.md)).

Open **http://localhost:5173** (not 5225). Vite proxies `/api` and `/hubs` to the backend — see
[frontend/vite.config.ts](frontend/vite.config.ts).

Checks:

```bash
dotnet build TrustCabinet.sln     # build the backend
cd frontend && npm run build      # tsc -b + vite build, emits into the backend wwwroot
cd frontend && npm run lint       # oxlint
dotnet test TrustCabinet.sln      # xunit, tests/TrustCabinet.Web.Tests
```

CI runs all four on every pull request, limited to what the PR touches — see
[.github/workflows/ci.yml](.github/workflows/ci.yml). A PR that only changes `frontend/` or `docs/`
runs just the backend tests that read those files; one that only changes README or `deploy/` skips
both jobs. A new file the backend build or a test reads from outside `backend/` and `tests/` needs
its path added to the `changes` job, or CI will skip the tests that should catch it.
Keep it green; fix the branch rather than disabling a step.

**CI builds the backend with `-warnaserror`**, so a compiler or analyzer warning fails the pull
request. The flag is only in the workflow, not in the `.csproj` files: a local `dotnet build` still
just warns, so half-written code stays runnable while you work. Run `dotnet build TrustCabinet.sln
-warnaserror` before pushing to get the answer CI will give. A warning that is genuinely intended
is declared by code in the project that emits it —
`<WarningsNotAsErrors>xUnit1012</WarningsNotAsErrors>`, with a comment saying why — rather than by
removing the flag.

Tests live under `tests/`, one project per backend project, registered in the `.sln`. Today there
is only `TrustCabinet.Web.Tests` with a `/api/health` smoke test — it exists to prove the app boots
in memory and CI can run tests at all. Replace it as real endpoints land; do not leave it as the
only test.

## Conventions

**Same origin, no CORS.** The frontend always calls relative paths (`/api/...`). In development
Vite proxies them; in production the backend serves the frontend. Therefore:

- do not add `UseCors` to the backend,
- do not introduce a `VITE_API_BASE_URL`-style environment variable,
- do not hardcode `http://localhost:5225` in frontend code.

**The database is code-first.** Never change a schema by hand: edit the entity or its
`IEntityTypeConfiguration`, then `dotnet dotnet-ef migrations add <Name> --project
backend/TrustCabinet.Infrastructure --startup-project backend/TrustCabinet.Web --output-dir
Migrations`. Commit the migration together with the updated `ModelSnapshot`. A migration that has
reached `main` is immutable — correct it with a new one, because teammates have already applied it.
In Development the app migrates and seeds itself at startup; nothing outside Development ever does.

Enums are stored as strings, entity keys are `Guid.CreateVersion7()`, user rows are
deactivated rather than deleted, and `MeetingStatusHistory` is append-only evidence — the
application never updates or deletes a row there. The reasoning for each is in
[docs/02-design/02-erd.md](docs/02-design/02-erd.md).

**Adding an endpoint** means touching all three places, following the `health` example:

1. backend: declare it in `Program.cs`
2. `frontend/src/types/<name>.ts`: the type matching the response
3. `frontend/src/api/<name>.ts`: the call, using `api.get/post/...` from `client.ts`
   — do not call `fetch` directly from a component

**The UI is never the security boundary.** `RequireCapability` and the sidebar filtering decide
what to *render*, nothing more. Every endpoint must re-authorize the request server-side, and list
endpoints must filter rows server-side — a record the caller may not see should never reach the
browser. A capability-gated route renders the same 404 as an unknown URL, so a restricted screen
does not reveal that it exists; keep it that way rather than switching to 403.

**403 or 404?** The UI rule above is about *routes*; the API answers by what would leak:

| Trường hợp | Mã | Lý do |
|---|---|---|
| Chưa đăng nhập | `401` | — |
| Đã đăng nhập, thiếu capability, endpoint cố định (`/api/users`, `/api/roles/capabilities`) | `403` | Đường dẫn nằm trong tài liệu API công khai, giấu đi không được gì; `404` chỉ khiến FE không phân biệt nổi "sai quyền" với "gọi sai URL" |
| Không được đọc **một tài nguyên cụ thể** (tài liệu, cuộc họp) | `404` | Ở đây `403` tự nó là thông tin: nó xác nhận tài nguyên đó có tồn tại — xem SCRUM-22 |
| Route UI thiếu quyền | `404` | Màn hình bị hạn chế không tự lộ ra là nó có tồn tại |

Tóm lại: giấu **sự tồn tại của một dòng dữ liệu**, không giấu **sự tồn tại của một endpoint**.

**Message codes** (`code` in an error or success body, and messages the FE shows on its own)
come from one project-wide catalogue:
[docs/03-api/00-danh-muc-ma-thong-bao.md](docs/03-api/00-danh-muc-ma-thong-bao.md). Read it before
adding or changing a message.

- Format `MSGnn` (`MSG07`, `MSG41`; three digits past `MSG99`). No string codes such as
  `MEETING_CREATED`.
- One global sequence. Never restart at `MSG01` for a new use case, even when a use-case spec or
  sequence diagram does — take the next free code from the catalogue instead and flag the
  mismatch in the PR.
- One code = one situation. Reuse a code only for the same situation on another endpoint; never
  give two different situations the same code.
- Never renumber or reuse a code once it has reached `main`: the FE and the tester match on it.
  A retired code stays in the catalogue marked as no longer used.
- Declare codes as named constants at the top of the endpoint file (`private const string
  Msg29Created = "MSG29";`), and match on the code in the FE, never on the message text.
- Adding a code means updating the catalogue — the table row and "Mã tiếp theo" — in the **same
  commit** as the code.
- `MessageCodeCatalogueTests` enforces this by reading the source: a code the FE matches with
  `isApiError` that the catalogue lacks (or lists under another status), a backend code missing
  from the catalogue or asserted by no test, or an inline `"MSGnn"` literal fails CI. It cannot
  tell whether a code is used for the *right situation* — that is still the reviewer's job.

**CSS**: use the variables declared on `:root` in `global.css` (`--brand`, `--gold`, `--ink`,
`--surface`, `--muted-ink`, `--chip-*`, …) rather than hardcoded colours. Blue `--brand` is the
primary colour; gold is only a thin accent edge; red (`--danger`) is only for "Mật", the current
time and warnings — plus the PDF file icon (`--file-pdf`), red because that is how readers know
PDF. The UI is **light only** — do not add a `prefers-color-scheme: dark` palette.
Sizes follow ADR-0012 (256px sidebar, 72px collapsed, 40px controls, 44px page-header action, 6px
radius, 14px body text, never below 12px; a listing is one `.list-panel` of toolbar, table and
paging); check against it before inventing new ones. A meeting's state uses `StatusBadge` and its
sensitivity `SensitivityBadge` — they differ by shape as well as colour. Icons come from `components/Icon.tsx`,
which maps a name to a Lucide glyph — never import `lucide-react` in a screen.

**Dropdowns** use `components/Select.tsx`, not a native `<select>`: the browser hands a native
select's popup to the operating system, which ignores the page stylesheet and looked wrong in dark
mode.

**`Program.cs` will outgrow itself.** As the domain grows, split endpoints into extension methods
(`MapMeetingEndpoints()`, …), and if needed split out `TrustCabinet.Domain` / `.Application` /
`.Infrastructure` as the README anticipates — remember to add new projects to the `.sln`.

## Documentation

### Load context before editing docs

`AGENTS.md` is loaded at the start of a task in this repository. When a request may create, edit, or
review files under `docs/`, load context in this order before deciding the scope:

1. Read [docs/README.md](docs/README.md).
2. For each target file, read `README.md` from `docs/` through each parent directory down to the
   directory that contains the file. If a `docs/` directory has no README, add a short one, scoped
   to that directory, in the same task.
3. Read any `AGENTS.md` that applies in the target directories, and the source or standard documents
   the related READMEs point to. When a task touches several areas, load each relevant branch. Skip
   READMEs for areas the task does not touch.
4. A directory README is a map of scope, the canonical source, and how source files relate to
   exported files. Do not copy the content of those standards into the README.
5. **Generated report / extract files are not editable.** If the path is a Report Markdown under
   `docs/08-reports/` (`Report-*.md`), or a Report-3 extract under `docs/01-requirements/`
   (`05-use-case-specifications.md`, `06-business-rules.md`), stop writing to that file. Read
   [docs/08-reports/README.md](docs/08-reports/README.md), edit the DOCX in
   `docs/08-reports/source/`, then regenerate with `scripts/sync_reports.py`. Do not open the
   snapshot and "fix the wording" there — the next sync overwrites it.

Every directory under `docs/` must have a `README.md`. This lets an agent start from the repository
context and follow the path to the files it will change.

When a change affects the API contract, a technical decision or the scope, update `docs/` in the
**same commit** as the code.

- Significant technical decision → write a new ADR in [docs/05-decisions/](docs/05-decisions/);
  never edit an accepted one.
- Scope change → update `docs/01-requirements/` **and**
  [frontend/src/pages/scopeData.ts](frontend/src/pages/scopeData.ts) (the landing page reads it).
- New or changed endpoint → update [docs/03-api/](docs/03-api/).

**Reports — hard rule (agents keep getting this wrong).** The Markdown under `docs/08-reports/`
(`Report-1_….md`, `Report-2_….md`, `Report-3_….md`) and the extracts
`docs/01-requirements/05-use-case-specifications.md` /
`docs/01-requirements/06-business-rules.md` are **generated snapshots**, not sources. Never edit
them by hand, even for a one-line fix.

1. Edit the DOCX in `docs/08-reports/source/` (that folder is the only local source).
2. Regenerate: `python scripts/sync_reports.py --source docs/08-reports/source/<file>.docx --report <n>`
   (Report 3 also refreshes the requirement extracts and `docs/uc-map.html` in the same run).
3. Push to Drive only when asked: `scripts/push_reports.py`. Drive is never the source — nothing
   reads reports back from it.

Full workflow, Drive links and DOCX preservation rules:
[docs/08-reports/README.md](docs/08-reports/README.md).

**BA settles the requirement docs last, not first.** BE writes the basic UC Specification and
Sequence Diagram, then implements; BE and FE change whatever the Use Case needs and note each change
as a comment in the Jira ticket; after FE integration the ticket moves to `BA Final Review` and BA
brings the UC Specification, diagrams and business rules in line with the code. So:

- when implementing, a deviation from the ticket or the docs is allowed — but tell the user and
  make sure it is noted in the ticket;
- when reviewing, never request changes, or ask BA to update docs, just because the UC
  Specification, diagrams or business rules lag behind the code. Do flag a requirement change that
  is **not** noted in the ticket, and docs the author wrote that contradict themselves.

Details in [docs/DEVELOPMENT_WORKFLOW.md §2.1](docs/DEVELOPMENT_WORKFLOW.md#21-changing-a-requirement-during-implementation).

## Watch out

- Some features are deliberately out of scope (task/action-item tracking, real HSM signing,
  air-gapped deployment, eOffice integration) — see the README. Voice ID came **into** scope on
  2026-10-23 at the review council's request ([ADR-0027](docs/05-decisions/0027-voice-id-co-dong-y.md)),
  under its own consent; speech models run in-house ([ADR-0026](docs/05-decisions/0026-tu-chay-nhan-dang-giong-noi.md)).
  Do not implement them unprompted. TrustCabinet stops at the approved minutes: it never tracks
  assignees, due dates or completion state.
- **One organisation, no org units.** There is no `organizations` table, no multi-tenancy, and no
  `org_units` — no department hierarchy, no unit on a user or a meeting, and no "invite the whole
  unit". Sign-in is email + password or Google; no separate IdP. Both are settled in
  [ADR-0004](docs/05-decisions/0004-mot-to-chuc-khong-co-don-vi.md), and a test in
  `DatabaseModelTests` fails if an `OrgUnit*` property comes back.
- Meeting recordings are personal data, and a voice used to recognise someone is **sensitive
  biometric data**, under Vietnam's Personal Data Protection Law 91/2025/QH15 (in force since
  2026-01-01, guided by Decree 356/2025/NĐ-CP; it superseded the Decree 13/2023/NĐ-CP regime). When
  touching this area, state the consent and deletion obligations explicitly rather than silently
  persisting the data. A voice embedding may live only encrypted in `voice_samples`, under a
  consent that can be withdrawn; `DatabaseModelTests` fails if one appears anywhere else.
- **The test server enforces a Content-Security-Policy** (`deploy/Caddyfile`); local development has
  none. Scripts, fonts, images, frames and connections may come only from our own origin, Google
  Fonts and Google Sign-In. A new outside source (a CDN, an analytics or AI service called from the
  browser) works locally and is blocked on the server until it is added there. Never add
  `'unsafe-inline'` or `'unsafe-eval'` to `script-src`: that is what keeps an XSS from running.
- `backend/TrustCabinet.Web/wwwroot/` is build output. Edits there are lost on the next build.
- **Never commit a connection string, API key or credential.** `appsettings.json` is committed, so
  secrets do not go there. Use `dotnet user-secrets set "ConnectionStrings:TrustCabinet" "..."` locally
  (the repo is public) and environment variables in deployment.
- Sign-in, sign-out and the admin screens (users, role matrix) call the real API. Tokens live in
  `features/auth/session.ts`; a user's capabilities come from the backend, never from a table in
  the frontend. The meeting list, create, edit and detail screens call the real API as well; on the
  detail screen every tab but "Thông tin chung" is still a placeholder until its endpoint exists.
- The notification bell (`features/notifications/`) calls the real API and listens on
  `/hubs/notifications`. Its provider lives in `RequireAuth`, so keep every signed-in route —
  redirects included — under that guard; a route outside it remounts the provider and reconnects.
- The AI-eCabinet repo (`~/Developer/ai-ecabinet`) is a private, unlicensed reference. Read it to
  understand the domain; do not copy source files into this repo. It is a graded capstone.
