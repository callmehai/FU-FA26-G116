---
name: jira-deviation-note
description: Note in the Jira ticket (project SCRUM) where an implementation deviates from the ticket, the UC Specification, the sequence diagram or the business rules, so BA Final Review can bring the docs in line. Use when implementation changes a requirement, when the user says "ghi chú lên Jira", "note vào ticket", or before opening a PR whose work differs from the ticket.
---

# Note a deviation in Jira

DEVELOPMENT_WORKFLOW.md §2.1: BE and FE may change what a Use Case needs, but every change must be
a comment on the Jira ticket. BA then updates the UC Specification, diagrams and business rules at
`BA Final Review`. A requirement change that is not in the ticket is what review flags.

## Steps
1. **Find the ticket.** Take the key from the user, the branch name (`scrum-107` → `SCRUM-107`) or
   the commit messages. If none, ask. Read it with the Atlassian tools (`getJiraIssue`; reuse the
   `cloudId` from `getAccessibleAtlassianResources`) together with its existing comments, so you
   do not repeat a note that is already there.
2. **Find the deviations.** Compare `git diff origin/main...` with the ticket's description and
   acceptance criteria, and with the UC spec / sequence diagram it links to in `docs/`. A deviation
   is anything the ticket or spec says differently: a field, a status code, a message code, a
   business rule, a screen flow, a removed or added step. Code style is not a deviation.
3. **Draft the comment in Vietnamese** (UI-facing team language), one bullet per deviation:
   ```
   [BE|FE] Thay đổi so với ticket/UC:
   - <Spec nói gì> → <Code làm gì>. Lý do: <vì sao>. Ảnh hưởng: <màn hình / endpoint / tài liệu cần BA cập nhật>.
   ```
   Keep English technical terms as-is. Name the doc BA must update (`docs/03-api/11-tai-lieu-cuoc-hop.md`, UC-xx).
4. **Show the draft to the user and wait for a clear yes.** Posting a comment is visible to the whole
   team. Never post on your own.
5. Post with the Atlassian comment tool, then give the user the ticket link.

Do not transition the ticket's status unless the user asks for that too.
