# SCRUM-28, 96 biểu quyết — BA Final Review; đóng SCRUM-41, 44, 108

**Người ghi:** KhánhPG/BA, 08/11/2026. **Nguồn:** comment dev trên Jira (Huy BE 30/10, Huy FE 31/10, review của Lộc 30/10),
đối chiếu với `main` `098d9bf5` ngày 08/11/2026. Note này là sổ việc, không thay Jira hay đặc tả
([DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update)).

## 0. Ticket và thứ tự làm

Năm ticket đang ở `BA Final Review` (Sprint 3, đọc ngày 08/11/2026):

| Ticket | Nội dung | Docs trong repo | Việc còn lại | Làm |
|---|---|---|---|---|
| SCRUM-41 | Phòng họp trực tuyến (UC-37–42) | PR #147 đã vào `main` (BA 29/10) | Chỉ đóng ticket (§1) | 1 |
| SCRUM-44 | Ghi cuộc gọi cho ASR | UC-44a/44b theo ADR-0040; ADR-0030 đã bị thay | Chỉ đóng ticket (§1) | 1 |
| SCRUM-108 | Cửa sổ nổi (UC-78) | UC-78 và sequence `UC78_FloatingWindow` đã có | Đối chiếu một lần với `pipWindow.ts`, rồi đóng (§1) | 1 |
| SCRUM-28 | Biểu quyết trực tiếp (UC-52a–57) | Chưa có commit BA nào sau 30/10 | §2, §3, §4 | 2 |
| SCRUM-96 | Biểu quyết: nháp Secretary, tài liệu, xác nhận đọc (UC-74, UC-76) | Như trên | §2, §3, §4 | 2 |

**Thứ tự:** (1) đóng 41, 44, 108 trước vì nhỏ, chỉ comment Jira; (2) SCRUM-28 và SCRUM-96 làm chung một PR docs vì cùng sửa
UC-52–57, BR-190, sequence UC-54/55 và cùng ERD; (3) Report 3 sửa trong DOCX rồi sync (§5).

Mọi sửa Report 3 đi vào DOCX trong `docs/08-reports/source/`, rồi `python scripts/sync_reports.py --source docs/08-reports/source/<file>.docx --report 3`
([AGENTS.md](../../../AGENTS.md#documentation)). Không sửa tay `05-use-case-specifications.md`, `06-business-rules.md`.

## 1. Đóng SCRUM-41, 44, 108 (chỉ Jira)

| Ticket | Comment cần ghi | Chuyển sang |
|---|---|---|
| SCRUM-41 | PR #147 đã vào `main` (merge `86f0c483`); UC-37–42, BR-323–BR-332, MSG328–MSG343/350 khớp code (comment 10326 ngày 29/10). Còn mở: BR-336 sức chứa 50 người chưa có mã cho UC-37 EX-7; Guest vào phòng đã làm ở PR #213/#215 (note 24) — kiểm lại UC-37 AF-2 không còn ghi "chưa cài" | `In Review` |
| SCRUM-44 | Hướng "Thư ký trộn cả cuộc gọi" (ADR-0030) đã **bị bỏ**, thay bằng ADR-0040; PO chốt 26/10 và 29/10. Đặc tả hiện hành là UC-44a/44b, ghi theo từng micro. Không còn việc BA riêng cho ticket này | `Done`, ghi rõ "thay bằng SCRUM-109 / ADR-0040" |
| SCRUM-108 | UC-78 và sequence đã có. Mục "Còn thiếu" của mô tả: sequence đã xong; đối chiếu `pipWindow.ts` xem bên dưới | `In Review` sau khi đối chiếu |

Đối chiếu SCRUM-108 (một lần): đọc `frontend/src/features/calls/room/pipWindow.ts`, so với UC-78 (AF-1 tự mở khi sang tab khác,
AF-2 rời phòng từ cửa sổ nổi không hỏi lại, EX-1 trình duyệt từ chối thì không báo gì) và BR-332. Lệch chỗ nào thì ghi ở đây,
không sửa tay Markdown.

## 2. SCRUM-28 — việc BA

Nguồn: comment Jira 10339, 10340 (Huy BE), 10354 (Huy FE), 10347 (Lộc). Contract as-built: `docs/03-api/15-bieu-quyet.md` §7.

| # | Chỗ sửa | Hiện trạng ở repo (08/11) | Sửa thành |
|---|---|---|---|
| 28-1 | UC-52a, UC-52b — Primary Actors | Có "System Admin" | Bỏ System Admin, hoặc ghi "chỉ khi có quan hệ" (BR-29) |
| 28-2 | UC-56 POST-1 | "closing time and tally are recorded" | "the tally is available from the recorded ballots" (tally không lưu thành cột) |
| 28-3 | UC-54 bước 5 (Khách) | "Guest must have verified the invited email" | Khách không cần xác minh lúc mở phiếu; được bỏ phiếu bất cứ lúc nào phiếu còn Open, miễn lời mời còn hiệu lực và lúc bỏ phiếu có phiên Khách xác minh (PO 30/10) |
| 28-4 | BR-190 | "fixed when the Vote opens" | Thêm ngoại lệ: Khách được mời **sau** lúc mở phiếu vẫn vào danh sách, `eligibleCount` tăng |
| 28-5 | UC mời Khách (UC-04 / UC-28a) và sequence | Chưa có nhánh | Thêm nhánh "lời mời Khách được tạo khi có phiếu Open chọn vai trò Khách" |
| 28-6 | UC-53 AF-3, UC-54 EX-2 | Chưa nói rõ | Thành viên gửi `openImmediately` nhận MSG213, Thư ký nhận MSG218 (EX-2 đi trước AF-3) |
| 28-7 | Sequence UC-55, bước "Save VoteChoice … Secret Vote" | Chưa phản ánh | Phiếu kín: **không ghi `VoteChoice`** (đã đổi theo review Lộc, ERD dòng 58); Id ngẫu nhiên, không `CastAt` |
| 28-8 | ERD `02-erd.md` | Dòng 58 nói "phiếu kín không ghi dòng nào", nhưng dòng 361 còn cột `CastAt`, dòng 390 còn mô tả ghép bằng `CastAt` | Làm ba chỗ khớp nhau; xác nhận bằng migration `VoteChoicesWithoutCastTime` |
| 28-9 | Sequence UC-55 / UC-56 | Chưa kiểm | Response bỏ phiếu chỉ trả MSG227; turnout lấy từ `voteChanged` — chỉ để viết as-built |
| 28-10 | FE: thông báo nổi trong phòng họp | Ticket: "Chủ toạ mở biểu quyết … Bỏ phiếu" | Hiện cho mọi người chưa bỏ phiếu; người không có trong danh sách thấy nút "Xem"; ảnh hưởng màn 8.5 / 14.2 |
| 28-11 | FE: kết luận UC-57 | Wireframe 11.4 chọn sẵn "Thông qua" | Không chọn sẵn (BR-263); sửa wireframe 11.4 |
| 28-12 | FE: hộp thoại khi mở và đóng phiếu, phiếu công khai đã đóng | Chưa ghi | Màn 11.1 / 11.2; mục thu gọn "Phiếu từng người (x/y)" (BR-273) |

**Chờ PO, chưa làm:**

- Người nội bộ được thêm **sau** lúc mở phiếu (comment 10350): giữ như cũ hay đối xử giống Khách. UC-55 EX-3 đang ghi "không được thêm". Không sửa UC-55 EX-3 và BR-190 cho vế này cho đến khi PO trả lời.
- Nháp còn lại khi cuộc họp `Ended` (Lộc mục 4): cho xoá nháp hay ẩn nháp. BR-189 cần chốt.
- Khách chưa từng xác minh có tính vào `eligibleCount` không (Lộc mục 2). Comment 10339 của Huy đã trả lời theo hướng "tính", ghi lại cho khớp 28-3.

**Giữ lại cho ticket biên bản:** "Kết quả kiểm phiếu và kết luận đưa vào biên bản" chưa làm ở SCRUM-28 (Lộc mục 5). Ghi vào ticket biên bản để không mất khi SCRUM-28 `Done`.

## 3. SCRUM-96 — việc BA

Nguồn: comment Jira 10341, 10351 (Huy BE), 10355 (Huy FE), 10348 (Lộc).

| # | Chỗ sửa | Hiện trạng ở repo (08/11) | Sửa thành |
|---|---|---|---|
| 96-1 | UC-74 PRE-2, EX-2, sequence UC-74 | "tài liệu gắn với một Vote" | Người xác nhận là người bỏ phiếu của một biểu quyết **Open** gắn tài liệu đó; thêm hai nhánh lỗi MSG224 (không có trong danh sách) và MSG225 (không còn Open / cuộc họp không InProgress); tài liệu chỉ gắn nháp → 404/MSG156 |
| 96-2 | UC-74 AF-1, catalogue MSG253 | Chưa có | Đã xác nhận rồi → 200/MSG253 kèm `alreadyConfirmed`, kể cả khi `lastPageReached: false` |
| 96-3 | UC-74 bước 3 và 5, UC-32c POST-3 và Normal Flow, sequence UC-32c | "system records that the actor reached the last page", checkbox "I have read this document" | Server **không lưu** bước tới trang cuối; viewer gửi `lastPageReached: true` trong lệnh xác nhận, khác `true` thì 400/MSG254 |
| 96-4 | BR-290 | "scrolled to the last page … then ticks" | "Server chỉ ghi nhận lời xác nhận của người dùng, không kiểm việc đọc; không phải chốt bảo mật" (PO chốt 30/10) |
| 96-5 | UC-74 Normal Flow, UC-32c, màn trình xem 7.5 | Checkbox "Tôi đã đọc" | Nút "Đọc tới trang cuối" (khoá) đổi thành "Xác nhận đã đọc" khi cuộn tới cuối (FE 31/10) |
| 96-6 | UC-76a EX-4 | Chỉ MSG267 | MSG267 khi gắn tài liệu cuộc họp; MSG166 khi gắn từ kho / tải từ máy (khớp sequence) |
| 96-7 | UC-76a AF-1 bước 4, sequence nhánh "Selection invalid" | Chỉ MSG164 / MSG166 | Thêm 400/MSG288 (quá 10 tệp hoặc 200 MB) và 409/MSG346 (hết dung lượng kho) |
| 96-8 | UC-76a EX-2 / AF-2 | Chưa có nhánh | Không chọn tài liệu: 404/MSG170 (nguồn cuộc họp), 400/MSG249 (nguồn kho) |
| 96-9 | UC-76b EX-2 (MSG269) | Không ghi HTTP | Ghi 404 trong catalogue |
| 96-10 | Sequence UC-54 | Có bước "lưu danh sách tài liệu chốt khi mở" (dòng 66) | Bỏ bước đó; danh sách cố định vì chỉ gắn / gỡ được khi Draft (BR-288) |
| 96-11 | BR-292 | "while the Vote is Open" | Mở ra mọi trạng thái: gỡ tài liệu khỏi cuộc họp thì tài liệu ẩn khỏi biểu quyết và không còn tính vào điều kiện đọc |
| 96-12 | UC-55 bước 2 | "Submit ballot chỉ khả dụng khi mọi tài liệu đã xác nhận" | Không đổi đặc tả; `actions.canCast` không xét đọc, FE tự khoá nút; ghi cho Tester |
| 96-13 | FE: gắn / gỡ tài liệu | Ghi ở tab Tài liệu, nhóm "Biểu quyết" | Chỉ có ở trang chi tiết cuộc họp, không trong phòng họp (BR-316); ảnh hưởng UC-76a/b, màn 8.5 |
| 96-14 | UC-32a AF-1 (nhóm "Biểu quyết" ở tab Tài liệu) | Ghi "sẽ có khi làm SCRUM-96" | Đã có trên `main` (note 22, 27: tab Tài liệu trả `votes`). Kiểm lại câu cho khớp as-built; điểm "còn hở #10" của Huy có thể đã lỗi thời |
| 96-15 | Khách mở tài liệu để xác nhận đọc | SCRUM-96 ghi "còn hở" | Đã chuyển SCRUM-123 (đã `Done`, note 23); bỏ chữ "chưa" nếu còn |

## 4. Việc chung cho SCRUM-28 và SCRUM-96

- **Catalogue** [00-danh-muc-ma-thong-bao.md](../../03-api/00-danh-muc-ma-thong-bao.md): đã có MSG224, MSG253 "Đã triển khai". Rà thêm MSG217, MSG218, MSG221, MSG252–255, MSG265–270 đúng HTTP và tình huống.
- **Class diagram FE-11** `feature-11-voting-management-class-diagram.drawio`: thêm `VoteDocument`, `DocumentReadConfirmation`, `VoteEligibleVoter`; `VoteChoice` bỏ `CastAt`; làm theo [DRAWIO_EDITING_RULES.md](../../../DRAWIO_EDITING_RULES.md), render lại PNG.
- **Sequence** (`.txt` + `.png` + tab trong drawio gộp): UC-32c, UC-54, UC-55, UC-74, UC-76a; UC mời Khách (28-5). Xuất ảnh theo README `drawio-png`.
- **Wireframe**: 7.5 (trình xem), 8.5, 11.1–11.4, 14.2.

## 5. Report 3 và Jira

1. Sửa DOCX theo §2–§4, thêm một dòng Change Log 08/11/2026, chạy `sync_reports.py --report 3`, kiểm `check_docx.py`.
2. Sau khi PR docs **đã vào `main`** (kiểm `git log origin/main`), comment từng ticket: UC / BR / MSG đã đổi, PR và commit, việc còn lại. Chuyển `BA Final Review` → `In Review`.
3. SCRUM-28 và SCRUM-96 chưa chuyển sang `Done` khi còn mục "chờ PO" ở §2. Ghi rõ trong comment.

## 6. Checklist

- [ ] §1: comment và chuyển trạng thái SCRUM-41, 44, 108.
- [ ] §1: đối chiếu `pipWindow.ts` với UC-78.
- [ ] §2: 28-1 → 28-12.
- [ ] §3: 96-1 → 96-15.
- [ ] §4: catalogue, class diagram, sequence, wireframe.
- [ ] §5: sync Report 3, PR docs, comment Jira.
- [ ] Hỏi PO: người nội bộ thêm sau khi mở phiếu; nháp khi `Ended`.
