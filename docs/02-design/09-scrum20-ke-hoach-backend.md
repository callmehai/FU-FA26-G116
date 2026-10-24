# SCRUM-20 / SCRUM-97 — kế hoạch Backend theo PO 20/10/2026

Đây là baseline thay thế kế hoạch grants/recovery trước đó trong PR #92. Nguồn quyết định: Hải (PO), note ngày 20/10/2026. Chỉ Backend; Hải làm Frontend; BA đồng bộ UC/BR/sequence tại Final Review.

## Phạm vi triển khai

1. Domain: bỏ grants/recovery/response-summary operations; manager active Chair/Secretary; exactly-one Chair; giữ Secretary sau Published; Chair đọc trước Published; recipient/confirmation/ngoại lệ inactive Chair cho UC-73.
2. Tạo họp: input roster 1..100 unique active internal, đúng một Chair; khóa user FOR SHARE; save meeting/history/assignments nguyên tử; revision0, Pending, delivery key theo assignment; MSG277 validation. Update không nhận roster.
3. API: roster response/totals, context myRole + ba flags, options ba roles, detail chair/secretaries/myMeetingRole. JSON mutation strict và If-Match giữ nguyên; MSG85/256/257/259/275/276.
4. Persistence: xóa bảng grants, sửa migration chưa lên main và regenerate snapshot, giữ SQL preflight/delivery-key/Down guard, backfill creator Chair khi thiếu; seed mọi meeting có Chair.
5. Delivery/audit: dispatch duy nhất, Guest reschedule durable, retry dependency tăng dần không khóa meeting khi unavailable; audit chỉ changed rows.
6. Realtime: participantsChanged sau commit kể cả response, hợp readers trước/sau, room hook riêng; transport failure không rollback.
7. Tests: role/state matrix; create atomicity; rollback/concurrency; strict JSON; totals; reader union; migration backfill/downgrade; delivery dedup/retry. Release build -warnaserror, full suite, FE lint/build.
8. Bàn giao: API/handoff/Postman/catalogue/ERD/ADR/test report cùng thay đổi; change note Jira SCRUM-20/SCRUM-97 và PR description; Hải review trước merge.

## Dependency và giới hạn

SCRUM-59 sở hữu Guest OTP/profile/session/link production; default adapters deny/unavailable. SCRUM-41 sở hữu room disconnect. Không đổi approval workflow, attendance, delegation, scope FE hoặc requirements/diagrams. MSG258/260 giữ dành sẵn; MSG152/153 retired; nhánh SCRUM-26 remap MSG277/278 khi tích hợp.

Chi tiết triển khai và tiêu chí đối chiếu: [API contract](../03-api/09-thanh-phan-tham-du.md), [hướng dẫn FE/test](../03-api/09-thanh-phan-tham-du-ban-giao.md), [ADR-0021](../05-decisions/0021-vong-doi-thanh-phan-va-y-dinh-gui-thu.md).
