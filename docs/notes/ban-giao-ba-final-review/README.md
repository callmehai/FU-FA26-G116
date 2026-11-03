# Bàn giao BA Final Review — 24/10/2026

Sáu note ban đầu của leader, note 10 (review PR #136), note 11 (quy tắc thông báo, PO chốt 25/10), note 12 (tài liệu FE, PO FE-08, ghi âm)
và note 13 (đối chiếu SCRUM-107 / SCRUM-21 với code, bước 6 của FE-07).
Mỗi file giữ nguyên phạm vi cần sửa. Khi ticket sang **BA Final Review**, xử lý lần lượt theo số thứ tự.

| # | File | Ticket / PR | Việc |
|---|---|---|---|
| 1 | [SCRUM-18 / SCRUM-20](01-scrum-18-20-loi-moi-va-danh-sach.md) | SCRUM-20 (PR #111), một phần SCRUM-18 | UC-14, UC-17, UC-28, UC-29, UC-30, BR-30, sequence UC-29/30/17 |
| 2 | [SCRUM-97](02-scrum-97-giao-chu-toa.md) | SCRUM-97 (PR #123), PO chốt 23/10/2026 | UC-73, sequence, MSG258, BR-126 |
| 3 | [Voice ID](03-voice-id-scrum-31-35.md) | SCRUM-102, SCRUM-103; mô tả SCRUM-31, SCRUM-35 | UC-43–51, ERD mục 7, căn cứ Luật 91/2025 |
| 4 | [SCRUM-26](04-scrum-26-diem-danh.md) | SCRUM-26 | UC-31, sequence, Report 3, ADR-0025, MSG150 |
| 5 | [SCRUM-107](07-scrum-107-tai-len-tai-lieu.md) | SCRUM-107 (PR #128) | UC-33a AF-1: 10 điểm lệch docs trong mô tả PR |
| 6 | [Chương trình nghị sự](08-chuong-trinh-nghi-su.md) | SCRUM-98, UC-23, UC-77 | Bỏ thời lượng, người trình bày, tài liệu nhiều mục, kết luận item |
| 10 | [Review PR #136 & Bổ sung sau merge](10-review-pr136-va-bo-sung-sau-merge.md) | PR #136, #133, #134, #135, #137, #141 | Fix CI .mmd, conflict main, MSG174, BR-115, UC-28b, UC-73, quy tắc không tự nhận thông báo, PO Agenda 25/10, rủi ro BR-188 |
| 11 | [Quy tắc thông báo & PO Agenda 25/10](11-quy-tac-thong-bao-agenda-po-25-10.md) | PR #133, #137, #134, #135, #141 | BR chung không tự nhận thông báo (BR-76/92/164/307), BR-72, rủi ro BR-188, lệch màn 5.1–5.3 |
| 12 | [Tài liệu FE, PO FE-08, ghi âm](11-tai-lieu-fe-phong-hop-ghi-am.md) | SCRUM-21, 107, 27, 40, 44, 109 (PR #134, #144, #154, #161) | Lệch BE/FE tài liệu, Đăng ký phát biểu, bỏ gọi 1–1, chế độ thu ghi âm, BR-328, mã MSG từ MSG328 |
| 13 | [Đối chiếu SCRUM-107 / SCRUM-21](13-doi-chieu-scrum-107-21.md) | SCRUM-107 (PR #128), SCRUM-21 (PR #134, #144) | Bước 6 FE-07: bảng đối chiếu UC-32a/32c/33a AF-1, BR, MSG, sequence, class diagram, Report 3 §III; **đã sửa tài liệu theo code** ngày 27/10/2026 (§10) |
| 14 | [Ghi chú 27–28/10, phân loại theo FE](14-ghi-chu-nhan-27-28-10-2026-phan-loai-theo-fe.md) | SCRUM-15, 20, 21, 22, 23, 25, 26, 41, 58, 69, 97, 98, 99, 107, 110–113 (PR #165–#178) | Trạng thái Jira, việc BA còn lại theo FE-04/05/06/07/08/13, ERD drawio, danh sách review (mục 5) |
| 15 | [Plan FE-07 BA Final Review](15-plan-fe-07-ba-final-review.md) | SCRUM-21, 22, 23, 58, 107, 110–113 | Plan đối chiếu FE-07 |
| 16 | [Ghi âm và Voice ID 28–29/10](16-ghi-am-voice-id-28-29-10-2026.md) | SCRUM-29, 31, 35, 109 (PR #184 → #193) | Cách ghi theo hình thức họp, mic phòng từng lượt, nghe lại câu và giọng mẫu, MSG351–MSG367; **thay §3–§5 của note 11 và phần lưu trữ của note 03** |
| 17 | [Plan sửa phần còn lại sau note 14](17-plan-sua-con-lai-sau-note-14.md) | SCRUM-99, 69, 15, 25, 98, 41 | 6 lượt: FE-04 huỷ cuộc họp, ERD drawio, UC-21/22, email, nghị sự, phòng họp; nguyên tắc Report 3 không kỹ thuật |
| 18 | [Sửa wireframe và việc ngoài phạm vi](18-sua-wireframe-va-viec-ngoai-pham-vi.md) | SCRUM-99, 25, 98 (+ bảng ticket đã chuyển 29/10) | Màn hình agenda bỏ thời lượng, màn huỷ cuộc họp, ERD muc-tieu/hien-tai, Permission Matrix, câu hỏi PO UC-26 |
| 19 | [Plan FE-09 / FE-10 ghi âm, gỡ băng, Voice ID](19-plan-fe-09-fe-10-ghi-am-go-bang-voice-id.md) | SCRUM-29, 31, 35, 109, 102, 103 | Tổ chức lại UC-43–46 (thêm 46d), thêm UC-80 (Ghi âm của tôi), UC-81 (Voice ID), BR-340+, MSG Report 3, 24 sequence, 2 class diagram as-built; tách tóm tắt thành FE-11 hoãn |
| 20 | [Việc Jira sau khi merge PR ghi âm / Voice ID](20-viec-jira-sau-khi-merge-pr-ghi-am-voice-id.md) | SCRUM-29, 31, 35, 109, 102, 103 | Ticket nào comment, chuyển kiểm thử hay Done sau khi PR merge; việc còn dở |
| 21 | [SCRUM-116 truy cập Khách UC-04](21-scrum-116-truy-cap-khach-uc04.md) | SCRUM-116–123 (PR #203, #208) | 6 điểm khác tài liệu: giới hạn OTP và phiên 12 giờ, Khách mất quyền khi Ended (UC-26, BR-181, ADR-0016), khai tên bắt buộc, MSG371–379; câu hỏi PO |
| 22 | [PR #201 gắn tài liệu từ dòng nghị sự](22-pr201-gan-tai-lieu-tu-dong-nghi-su.md) | không có ticket (PR #201) | 8 điểm khác tài liệu: form mục không còn chọn tài liệu (UC-23a/b), kẹp giấy là điểm vào UC-33b và dùng chung với UC-76a (SCRUM-96), `documentIds` thiếu thì giữ, `canLinkDocuments`, đăng nhập vào lịch (UC-01/02), BR-280 và tài liệu gắn biểu quyết; **03/11/2026: #235 và #236 đã merge; PO chốt phương án B và #249 đã lên `main`, nên BR-280 + BR-275 + UC-32a AF-1 + màn 7.3 còn phải sửa** |
| 23 | [PR #212 màn Khách và tài liệu Khách](23-pr212-scrum-121-122-123-man-khach.md) | SCRUM-121, 122, 123 (PR #212) | Màn 1.3, hồ sơ tự khai, trang cuộc họp Khách: 6.4 gộp vào trang, thiếu `sensitivity`, màn kết thúc, nút Thoát, `GET /meetings/{id}` 403 thay 404; **03/11/2026 đã sửa UC-04/29/32c + Change Log**, còn đặc tả màn hình và kỳ vọng 403 ở ticket |
| 24 | [PR #213, #215 Khách trong phòng họp](24-pr213-215-scrum-59-127-khach-phong-hop.md) | SCRUM-59, 127 (PR #213, #215) | Phòng chỉ mở khi InProgress, xác nhận thông báo ghi âm MSG380, lượt vào/ra theo phiên Khách (ERD), 180 ngày (đã sửa ở #222), I2/I3; ghi âm Khách chờ SCRUM-129; **03/11/2026 đã sửa UC-28c/37/38/41 + §V.2 MSG380 và ERD mục tiêu (muc-tieu + chen có `GuestSession`)** |
| 25 | [PR #218 gán người nói là Khách](25-pr218-gan-nguoi-noi-la-khach.md) | PR #218 (SCRUM-35) | UC-46b chọn Khách còn lời mời, MSG317 thêm 2 trường hợp, Khách không được gợi ý Voice ID; ADR-0044 đã hoàn tác; **03/11/2026 đã sửa UC-45b/46b/81a + ngữ cảnh MSG317, thêm `TranscriptSpeaker` vào ERD mục tiêu** |
| 26 | [Giao việc BE: che email roster, khoá tên](26-giao-viec-be-scrum-116-che-email-khoa-ten.md) | SCRUM-116 (PO 01/11/2026) | Đã làm ở PR #222 (`main`): Khách không thấy email người khác trong roster (vẫn thấy email của mình, tìm không theo email), khoá tên và chức vụ sau lần khai đầu (MSG381); Report 3 đã sửa |
| 27 | [PR #224 tài liệu sau test E2E](27-pr224-tai-lieu-sau-e2e.md) | SCRUM-21, 22, 23, 58, 107 (PR #224) | Nhóm Biểu quyết, Khách góp ý, server kiểm và đếm trang PDF (ADR-0044), MSG382/383; Report 3, sequence, API **đã sửa theo code** (PR #224, #235); chỉ để BA biết, không có việc cần sửa |
| 28 | [PR #225 máy tự gán tên người nói](28-pr225-may-tu-gan-ten-nguoi-noi.md) | SCRUM-35 (PR #225, ADR-0045) | Tự gán khi khớp ≥ 0,75, giọng thứ hai ≥ 0,7, rút đồng ý xoá tên máy gán; **03/11/2026 đã sửa BR-362/365/366/367, UC-45a/45c/46b, screen 10.1, 02-erd §7 và sequence UC-45c/UC-46b** |
| 29 | [SCRUM-130 docs review repo](29-scrum-130-docs-review-repo-uc06c-voiceid-1-2.md) | SCRUM-130, SCRUM-35 (PR #226, #227; ADR-0048, ADR-0049) | Report 1 lộ 6 email + 5 số điện thoại (**03/11/2026 đã bỏ hai cột liên hệ**), README/AGENTS trạng thái cũ (**đã sửa**), UC-06c vô hiệu hoá kết thúc Voice ID (+ BR-372), thông báo 1.2 + nghe lại trước khi lưu (**đã sửa**) |
| 30 | [PR #249 "Tài liệu chung" phương án B](30-pr249-phuong-an-b-tai-lieu-chung.md) | SCRUM-21 (PR #249) | BR-280 định nghĩa lại nhóm Tài liệu chung (không gắn mục **và** không gắn biểu quyết), BR-275 trừ tài liệu chỉ gắn biểu quyết nháp (Thành viên/Khách 404 MSG156); màn 7.3, 8.6, UC-32a/UC-32c và Change Log — **03/11/2026 đã sửa** |
| 31 | [SCRUM-129 Khách ghi âm theo micro, ERD thiếu `speech_clips`](31-scrum-129-khach-ghi-am-erd.md) | SCRUM-129 (PR #245), SCRUM-109, ADR-0051 | 7 điểm lệch ở comment Jira `10418`; **04/11/2026 đã sửa**: `02-erd` thêm `SpeechClip`/`VoiceConsent`/`VoiceSample` (32 → 35 thực thể), vẽ lại ERD mục tiêu + Chen (38 bảng / 59 quan hệ), Report 3 sửa UC-80/BR-355/BR-370/UC-39a AF-2 và thêm 5 entity vào §I.3.2 |
Plan gom cả sáu note. **Thứ tự làm và cách giao cho agent: [plan 05 mục 8](05-plan-sua-uc-br-msg-diagram.md#8-thứ-tự-và-cách-chạy-agent-đọc-mục-này-trước)** —
làm lần lượt từng lượt, mỗi lượt một session mới.

## Trạng thái Jira — đọc ngày 03/11/2026

Đối chiếu note với Jira (`sep490g116.atlassian.net`, project `SCRUM`) và trạng thái PR trên GitHub. "Docs" là phần trong repo đã sửa xong; "Jira" là đã comment kết quả BA Final Review hay chưa.

| Note | Ticket | Docs trong repo | Trạng thái Jira (03/11/2026) | Việc còn lại |
|---|---|---|---|---|
| [20](20-viec-jira-sau-khi-merge-pr-ghi-am-voice-id.md) | 29, 31, 35, 109, 102, 103, 125, 126 | PR #205 đã merge | **Xong 02/11**: đã comment cả 8; 29/31/35/109 → `In Review`, 102/103/125/126 → `Done` | — |
| [21](21-scrum-116-truy-cap-khach-uc04.md), [26](26-giao-viec-be-scrum-116-che-email-khoa-ten.md) | 116 (+117–123) | UC-04/BR/MSG + A/B đã merge (#203, #208, #222); #237 merge 03/11 | **116 → `In Review`** (comment 10420); 117, 123 `Done` | Mục D (ghi âm Khách) nay làm được vì SCRUM-129 xong (#245); đặc tả màn 1.3 + trang Khách |
| [22](22-pr201-gan-tai-lieu-tu-dong-nghi-su.md) | không có ticket (PR #201) | #235, #236 đã merge; **#249 (phương án B) đã lên `main`** | Không có ticket | Đã sửa BR-280 + BR-275, màn 7.3 và 8.6 theo phương án B — xem [note 30](30-pr249-phuong-an-b-tai-lieu-chung.md) |
| [23](23-pr212-scrum-121-122-123-man-khach.md) | 121, 122, 123 | PR #237 đã merge | **121, 122 → `In Review`** (10422, 10423); 123 `Done` + comment 10429 | Sửa mục 8 của SCRUM-122 (403 thay 404) và báo Tester |
| [24](24-pr213-215-scrum-59-127-khach-phong-hop.md) | 59, 127 | PR #237 đã merge | **59 → `In Review`** (10421), **127 → `In Review`** (10424) | I2/I3 trong mô tả SCRUM-127; đặc tả màn chờ Khách |
| [25](25-pr218-gan-nguoi-noi-la-khach.md) | 35 (qua PR #218) | PR #237 đã merge | 35 `In Review` | Gộp vào comment SCRUM-35 (10426) của note 28 |
| [27](27-pr224-tai-lieu-sau-e2e.md) | 21, 22, 23, 58, 107 | #224 + #235 đã merge | Cả năm `In Review`; đã comment 21 (10427), 107 (10428), 23 (10429) | 21 chờ BR-280 phương án B; 22/58 không cần sửa |
| [28](28-pr225-may-tu-gan-ten-nguoi-noi.md) | 35 | PR #238 → vào `main` bằng **PR #251** (merge `5b75cedb`) | `In Review`, đã trả lời comment 10406 (10426) | Tester kiểm lại lỗi "máy chỉ nhận 1 người nói" |
| [29](29-scrum-130-docs-review-repo-uc06c-voiceid-1-2.md) | 130 | Như trên (PR #238 → #251) | **`Done`** (comment 10425) | — |
| [30](30-pr249-phuong-an-b-tai-lieu-chung.md) | 21 (và 22) | Sửa Report 3 trong PR #252; UC-32a/UC-32c ở nhánh `docs/scrum-21-uc32-bieu-quyet-nhap` | `In Review` (comment 10427) | Comment kết quả sau khi PR tài liệu merge; ảnh wireframe 7.3/7.4/8.6 |
| [31](31-scrum-129-khach-ghi-am-erd.md) | 129 (và 109) | Đã sửa 04/11/2026 (`02-erd`, 2 hình ERD, Report 3) | Chưa đọc — comment `10418` ghi 7 điểm lệch | Comment Jira sau khi PR vào `main`; đọc comment 10418 để bổ sung 4 điểm còn lại |

**#238 merge sai đích — đã cứu xong.** #238 được merge vào nhánh stacked
`docs/ba-final-review-khach-pr212-213-215-218` **sau khi** nhánh đó đã merge vào `main` (là #237), nên nội dung
của nó (Report 1 bỏ email/số điện thoại, UC-06c + BR-372, Voice ID 1.2, `check_docx.py`, AGENTS/README) không có
trên `main`. **PR #251** (`docs/ba-final-review-scrum-130-voiceid` → `main`) đã merge ngày 03/11/2026 (`5b75cedb`);
đã kiểm lại trên `main`: `BR-372` có, Report 1 không còn email/số điện thoại, `AGENTS.md` đúng trạng thái. Hai nhánh
stacked đã xoá. Quy tắc để không lặp lại: [DEVELOPMENT_WORKFLOW §2.2](../../DEVELOPMENT_WORKFLOW.md#22-closing-a-review-which-tickets-to-update).

**SCRUM-21 phương án B — đã sửa 03/11/2026.** PO chốt và code lên `main` (`45ae5e36`, PR #249): "Tài liệu chung"
chỉ gồm tài liệu **không gắn mục nghị sự và không gắn biểu quyết**; tài liệu chỉ gắn biểu quyết nháp thì
Thành viên/Khách không thấy (404 MSG156), Chủ toạ/Thư ký vẫn thấy. Report 3 sửa **BR-280**, **BR-275**, đoạn mở đầu
màn 7.3, bảng 8.6 và Change Log — chi tiết ở [note 30](30-pr249-phuong-an-b-tai-lieu-chung.md).

- [05 — UC, BR, message, diagram](05-plan-sua-uc-br-msg-diagram.md)
- [06 — chụp lại và đặc tả màn hình Report 3](06-plan-dac-ta-man-hinh.md)

Nguồn chuẩn khi sửa nằm trong từng note (docs API, comment Jira, PR). Note không thay thế Jira hay đặc tả.

Việc chạy tiếp ở nhà (xuất PNG, draw.io) nằm ở [note-may-nha](note-may-nha/README.md).
