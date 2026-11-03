# SCRUM-130 — Docs từ đợt review repo: Report 1 lộ thông tin cá nhân, trạng thái README/AGENTS, UC-06c và Voice ID 1.2

**Người ghi:** KhanhPG (BA), 03/11/2026. **Nguồn:** tin TrustCabinet Bot 02/11/2026 22:57 (Hải giao), Jira **SCRUM-130**,
[PR #226](https://github.com/callmehai/FU-FA26-G116/pull/226) + [ADR-0048](../05-decisions/0048-thong-bao-voice-id-1-2-may-tu-dien-ten.md)
(Voice ID 1.2), [PR #227](https://github.com/callmehai/FU-FA26-G116/pull/227) + [ADR-0049](../05-decisions/0049-vo-hieu-hoa-tai-khoan-ket-thuc-voice-id.md)
(vô hiệu hoá tài khoản), [API 13](../../03-api/13-nhan-dien-giong-noi.md). Note này gom việc BA, không thay Jira.

> **Tiến độ 03/11/2026: CHƯA LÀM GÌ.** Mới ghi note. Việc Report 1 là **ưu tiên số 1** (repo công khai).

## 1. Report 1 lộ thông tin cá nhân — ưu tiên

- `docs/08-reports/source/Report_1_Project_Introduction.docx` có **6 email và 5 số điện thoại cá nhân** của nhóm (đếm trong
  `word/document.xml`; bảng tác giả/lịch liên hệ ở đầu báo cáo). **Không chép giá trị các thông tin này vào note, commit hay chat**
  — repo đang public.
- Cách sửa theo quy trình report: sửa **DOCX nguồn** → `python scripts/sync_reports.py --source
  docs/08-reports/source/Report_1_Project_Introduction.docx --report 1` → commit **cả DOCX và `Report-1_….md`** trong cùng commit.
- Thay bằng gì: email chung của nhóm/dự án nếu PO đồng ý, hoặc bỏ hẳn ô liên hệ; **không** để lại số điện thoại. Ghi lý do vào
  Change Log Report 1.
- Xoá khỏi **lịch sử git** (filter-repo/BFG) là việc riêng, **Hải quyết sau** theo tin nhắn — BA không tự rewrite lịch sử.
- [ ] Rà thêm Report 2–7 và `docs/notes/` xem còn cùng loại thông tin không (mới kiểm Report 1).

## 2. README.md và AGENTS.md đang ghi trạng thái cũ

- `README.md` dòng **24**: "Biểu quyết, biên bản và phê duyệt biên bản, tóm tắt bằng AI | **Chưa làm**" — biểu quyết (SCRUM-96/28),
  biên bản và tóm tắt đã có trên `main`.
- `AGENTS.md` dòng **12**: "**Status: v0.1.0 — skeleton plus the full data model.** … There is still no authentication and no
  business endpoint" — không còn đúng: đăng nhập, quản trị người dùng, cuộc họp, tài liệu, biểu quyết, ghi âm/gỡ băng, Voice ID đều đã có.
- **Tránh sửa** các dòng về **server test** (`deploy/`, ADR-0047, "test server runs as Staging…"): PR của Hải đang sửa cùng chỗ — chỉ
  ghi nhận, không đụng để khỏi conflict.
- [ ] Sửa README + AGENTS theo phạm vi đã làm; đối chiếu bảng scope với `frontend/src/pages/scopeData.ts` (AGENTS yêu cầu hai nơi khớp nhau).

## 3. UC-06c — vô hiệu hoá tài khoản kết thúc Voice ID (ADR-0049, PR #227)

Code: vô hiệu hoá tài khoản (`PUT /api/users/{id}/status`, `isActive: false`) **kết thúc Voice ID như khi tự rút đồng ý** — xoá mọi
giọng mẫu, gợi ý và **tên máy tự gán** cho người đó, đóng `voice_consents`, trong **cùng transaction** với việc đổi `is_active`; nhật ký
`voice.consent_withdrawn` với `reason: "accountDeactivated"`, người làm là **quản trị viên**. **Kích hoạt lại không khôi phục** gì:
người đó phải đồng ý lại và ghi giọng mẫu lại. Máy so giọng giữ khoá `FOR SHARE` trên dòng đồng ý nên rút đồng ý giữa lúc chuyển chữ
không để lại gợi ý cho người vừa rút.

| Chỗ cần sửa | Hiện tại | Phải thành |
|---|---|---|
| `UC-06c` Postconditions (dòng 334) | POST-1..3: đổi trạng thái, chặn truy cập, ghi audit | Thêm POST-4: vô hiệu hoá kết thúc Voice ID (xoá giọng mẫu, gợi ý, tên máy gán, đóng đồng ý) và **kích hoạt lại không khôi phục** (BR-365/BR-372) |
| `UC-06c` Normal Flow bước 7-8 | chỉ kết thúc phiên + audit | Thêm bước "nếu tài khoản có Voice ID: kết thúc trong cùng transaction, nhật ký ghi lý do `accountDeactivated`" |
| `BR-365` | chỉ nói rút đồng ý | Thêm vế vô hiệu hoá tài khoản; hoặc **BR-372** riêng cho ADR-0049 (đề xuất: BR riêng vì khác actor là quản trị viên) |
| `UC-81a`/`UC-81f` | rút đồng ý do chính người dùng | Thêm ghi chú: quản trị viên vô hiệu hoá tài khoản cũng kết thúc Voice ID, nhật ký người làm là quản trị viên |
| `02-erd.md` + ERD | `voice_consents`/`voice_samples` | Không đổi cấu trúc; chỉ câu "xoá theo tài khoản" nếu cần |

## 4. Voice ID — thông báo 1.2, máy chỉ tự điền cho người đã đồng ý 1.2, nghe lại trước khi lưu (ADR-0048, PR #226)

Code: thông báo Voice ID lên **bản 1.2** (mục đích "Nhận ra người phát biểu… (gợi ý tên, tự điền tên khi giọng khớp rõ)"; quyền của
bên: **rút lại thì giọng mẫu, mọi gợi ý và tên máy đã điền bị xoá ngay**). Máy **chỉ tự điền tên người đã đồng ý bản 1.2 trở lên**;
người còn ở bản 1.1/1.0 vẫn chỉ được gợi ý và thấy lời mời đồng ý điều khoản mới (ghi rõ điều gì đã đổi). Ghi giọng mẫu có bước
**nghe lại trước khi lưu**: bấm Dừng thì **chưa gửi**, nghe lại rồi **Lưu giọng mẫu** hoặc **Ghi lại**; trình phát hiện đủ độ dài.

| Chỗ cần sửa | Việc |
|---|---|
| `UC-81b` Normal Flow + Description | Thông báo 1.2: nêu máy gợi ý **và tự điền tên khi giọng khớp rõ**, Thư ký sửa được; mục đích đổi theo ADR-0048 |
| `BR-362` | Consent gắn bản thông báo; nêu bản hiện hành **1.2** và điều kiện máy tự điền (chỉ ≥ 1.2) |
| `BR-361`/`BR-366` | Chỉ người đồng ý 1.2 mới được máy tự điền (đặt cạnh ngưỡng 0,75) |
| `UC-81a` | Người đồng ý bản cũ thấy lời mời đồng ý điều khoản mới, liệt kê điều đã đổi; chưa đồng ý thì chỉ được gợi ý |
| `UC-81c` Normal Flow | Thêm bước nghe lại: sau khi bấm Dừng **chưa gửi**; nghe lại, chọn Lưu hoặc Ghi lại; chỉ lưu khi đủ 6–60 giây (BR-363) |
| Screens 8.x (Giọng nói của tôi) | Nếu đặc tả màn hình có hộp ghi mẫu: thêm nút nghe lại + Lưu/Ghi lại, và câu "Dừng chưa gửi" |
| `docs/03-api/13` | **Đã đúng** (bản 1.2, `playable`, `autoAssigned`, ngưỡng) — chỉ đối chiếu, không sửa |

## 5. Việc BA, theo thứ tự

- [ ] **Report 1: bỏ 6 email + 5 số điện thoại**, sync report 1, commit DOCX + Markdown (ưu tiên 1).
- [ ] README + AGENTS: trạng thái thật, scope khớp `scopeData.ts`, **không đụng dòng server test**.
- [ ] UC-06c + BR (đề xuất BR-372) + UC-81a/81f theo §3.
- [ ] UC-81b/81c, BR-361/362/366, screen spec 8.x theo §4.
- [ ] Note 28 (PR #225) làm cùng lượt: BR-365/366/367 và UC-46b liên quan chặt với §3–§4.
- [ ] Sync Report 3, Change Log; ghi kết quả vào SCRUM-130.

## 6. Hỏi PO / Hải

- Report 1 thay bằng email nào (email nhóm/dự án) hay bỏ hẳn ô liên hệ? Có cần xoá cả tên trường/đơn vị không?
- Thông báo 1.2: PO duyệt câu chữ tiếng Việt chưa, hay BA viết lại theo ADR-0048 rồi PO xem?
- Vô hiệu hoá tài khoản: đặt **BR riêng (BR-372)** hay gộp vào BR-365? (đề xuất BR riêng vì actor khác).
