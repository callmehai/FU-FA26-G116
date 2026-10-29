---
name: ship-pr
description: Get a TrustCabinet branch ready to merge — run the checks CI runs, confirm docs landed in the same commit, write the Vietnamese commit message and the English PR. Use only when the user asks to commit, push or open a PR.
disable-model-invocation: true
---

# Ship a PR

CI (`.github/workflows/ci.yml`) is strict and the conventions are easy to miss. Work through every
step and report what you actually ran. Never commit, push or open the PR without the user's go-ahead
for that step.

## 1. Branch
- Never commit to `main`. If on `main`, create a branch first, named after the ticket
  (`feat/scrum-107-upload-documents-r2`, `fix/...`, `docs/...`).
- `git status`: list untracked files and ask before staging anything you did not write
  (stray `README-*.txt`, `TestResults/`, local notes).

## 2. Checks — the same four CI runs, plus the warning gate
```bash
dotnet build TrustCabinet.sln -warnaserror
dotnet test TrustCabinet.sln
cd frontend && npm run lint && npm run build
```
- `-warnaserror` is only in CI; a plain local build passes with warnings that fail the PR.
- Backend tests need the PostgreSQL and Redis from `docker compose up -d`.
- A genuinely intended warning goes in that project's `.csproj` as
  `<WarningsNotAsErrors>CODE</WarningsNotAsErrors>` with a comment saying why — never remove the flag.
- If a check fails, fix the branch. Do not skip, disable or `--no-verify`.

## 3. Same-commit docs (from AGENTS.md)
Check the diff (`git diff origin/main...`) against this table:

| The diff touches | Must also change |
|---|---|
| a new or changed `/api/...` endpoint | `docs/03-api/` |
| a new `MSGnn` code | `docs/03-api/00-danh-muc-ma-thong-bao.md`: the table row **and** "Mã tiếp theo" |
| an entity or migration | `docs/02-design/02-erd.md` |
| scope | `docs/01-requirements/` **and** `frontend/src/pages/scopeData.ts` |
| a significant technical decision | a new ADR in `docs/05-decisions/` (never edit an accepted one) |
| a new folder under `docs/` | a `README.md` in it |

Consider running the `docs-sync-reviewer` agent, and `authz-reviewer` if an endpoint changed.

## 4. Commit message — Vietnamese
Format: `[Type]: <mô tả tiếng Việt> (SCRUM-nnn)`. Types used in the history: `[Feat]`, `[Fix]`,
`[Docs]`, `[FE]`, `[BE]`, `[Refactor]`, `[Chore]`. Say *what changed and why* in one line; keep
English technical terms as-is (*endpoint*, *migration*, *hub*). Example:

```
[BE]: tải tài liệu từ máy lên cuộc họp và lưu file trên Cloudflare R2 (SCRUM-107)
```

End with the attribution line from the session's system reminder, if there is one.

## 5. Pull request — English
- Title: `[Type] SCRUM-nnn: <English, imperative>`, e.g. `[BE] SCRUM-107: Upload meeting documents to R2`.
  Types: `[BE]`, `[FE]`, `[FE+BE]` (or `[Full]`), `[Docs]`, `[Chore]`; drop the ticket key when
  there is none (`[Chore] Shared Claude Code setup`). The team's Discord bot routes the review to
  the BE, FE or BA channel by this prefix, so never leave it out.
- Body: what changed, why, how it was verified (the checks from step 2 and their results, and the
  browser checks if the UI changed), and **every deviation from the Jira ticket or the UC spec**.
  Each deviation must also be a comment on the Jira ticket — use the `jira-deviation-note` skill.
- If a use-case spec numbered its messages from `MSG01`, say which catalogue codes you used instead.
- End with the attribution line from the session's system reminder, if there is one.
- `gh` may not be installed. If it is missing, give the user the compare URL
  (`https://github.com/callmehai/FU-FA26-G116/compare/main...<branch>`) and the title and body to paste.
