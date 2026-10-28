# Bàn giao FE-11 biểu quyết — cho BE (SCRUM-96)

Ngày 28/10/2026. Ticket: **SCRUM-96** (biểu quyết + UC-74). Nhánh thiết kế:
`docs/voting-management-design` (PR #146).

## Đọc theo thứ tự

1. [Contract API](../03-api/15-bieu-quyet.md) — endpoint, quyền, body, MSG.
2. [ERD mục tiêu](../02-design/02-erd.md) — `VotingSession`, `VoteEligibleVoter`, `VoteDocument`,
   `VoteReceipt`/`VoteChoice`, `DocumentReadConfirmation`.
3. Sequence [voting-management](../06-sequencediagram/voting-management/README.md) (UC-52a…57, 76a/b)
   và [UC-74](../06-sequencediagram/document-management/UC74_ConfirmDocumentRead.txt).
4. Class diagram [FE-11](../07-classdiagram/feature-11-voting-management-class-diagram.drawio) +
   [README §6f](../07-classdiagram/README.md).
5. State machine [SM-08](../10-statemachine/SM-08_Voting.drawio),
   [SM-09](../10-statemachine/SM-09_VoteConclusion.drawio).
6. UC/BR/MSG: [NOTE tiếng Việt](NOTE-review-fe11-uc-br-msg.md), catalogue MSG205–236 / 252–255 /
   265–270, ADR-0014/0016/0017/0019/0020.

## Đã có trên nhánh thiết kế

| Hạng mục | Trạng thái |
|---|---|
| Sequence UC-52a…57, UC-76a/b (+ PNG, tab draw.io gộp) | Có |
| Class diagram FE-11 | Có (thiết kế; vài lớp nét đứt PROPOSED) |
| ERD Markdown + Chen/mục tiêu (Vote thuộc Meeting, eligible, documents) | Có |
| Entity + EF config + migration voting | Có — **tách PR code** (xem dưới) |
| `DatabaseModelTests` ràng buộc voting | Có |
| Contract API `15-bieu-quyet.md` | Có (28/10/2026) |
| Bảng / entity `DocumentReadConfirmation` | **Chưa code** — đã ghi ERD đích |
| Endpoint / hub / Postman | Chưa |

## Việc BE làm

1. **Tách code schema** khỏi PR docs (theo [note 6.3](ban-giao-ba-final-review/11-tai-lieu-fe-phong-hop-ghi-am.md)):
   nhánh code gắn SCRUM-96; xoá migration `20261025104005_…` rồi
   `dotnet dotnet-ef migrations add …` lại trên `main` mới nhất.
2. Thêm `DocumentReadConfirmation` (+ migration) cùng endpoint UC-74.
3. Implement endpoint theo [15-bieu-quyet.md](../03-api/15-bieu-quyet.md); khai báo hằng MSG;
   cập nhật catalogue sang “Đã triển khai” + câu tiếng Việt trong cùng PR.
4. Tái dùng `MeetingAccessPolicy`, roster/Guest session, `MeetingDocument` / kho cá nhân /
   `FileStorage` như FE-07; không invent capability mới.
5. Ghi lệch so với sequence/UC vào comment Jira (DEVELOPMENT_WORKFLOW §2.1).

## Chỗ còn chờ chốt (không chặn bắt đầu)

- Tally: tính từ `VoteChoice` khi đóng hay lưu cột — class diagram ghi NEEDS REVIEW; contract
  mặc định **tính**.
- `PersonalDocumentRepository` trên DCD = `DocumentRepository` / `PersonalLibraryService` as-built FE-07.
- Realtime `voteChanged`: khuyến nghị, sequence không bắt buộc.
- Đổi base PR #146 từ `docs/design-diagrams` → `main` sau khi nhánh docs đủ và tách code.
