# SCRUM-130 — Docs từ đợt review repo: Report 1 lộ thông tin cá nhân, trạng thái README/AGENTS, UC-06c và Voice ID 1.2

**Người ghi:** KhanhPG (BA), 03/11/2026. **Nguồn:** tin TrustCabinet Bot 02/11/2026 22:57 (Hải giao), Jira **SCRUM-130**,
[PR #226](https://github.com/callmehai/FU-FA26-G116/pull/226) + [ADR-0048](../05-decisions/0048-thong-bao-voice-id-1-2-may-tu-dien-ten.md)
(Voice ID 1.2), [PR #227](https://github.com/callmehai/FU-FA26-G116/pull/227) + [ADR-0049](../05-decisions/0049-vo-hieu-hoa-tai-khoan-ket-thuc-voice-id.md)
(vô hiệu hoá tài khoản), [API 13](../../03-api/13-nhan-dien-giong-noi.md). Note này gom việc BA, không thay Jira.

> **Tiến độ 03/11/2026: ĐÃ LÀM XONG** cả bốn phần — Report 1 hết thông tin cá nhân, README/AGENTS cập nhật trạng thái,
> UC-06c + BR-372, Voice ID 1.2 + nghe lại trước khi lưu. Nhánh `docs/ba-final-review-scrum-130-voiceid`, cùng lượt với
> [note 28](28-pr225-may-tu-gan-ten-nguoi-noi.md). Chi tiết ở §5.

## 1. Report 1 lộ thông tin cá nhân — ưu tiên

- `docs/08-reports/source/Report_1_Project_Introduction.docx` có **6 email và 5 số điện thoại cá nhân** của nhóm (đếm trong
  `word/document.xml`; bảng tác giả/lịch liên hệ ở đầu báo cáo). **Không chép giá trị các thông tin này vào note, commit hay chat**
  — repo đang public.
- Cách sửa theo quy trình report: sửa **DOCX nguồn** → `python scripts/sync_reports.py --source
  docs/08-reports/source/Report_1_Project_Introduction.docx --report 1` → commit **cả DOCX và `Report-1_….md`** trong cùng commit.
- Thay bằng gì: email chung của nhóm/dự án nếu PO đồng ý, hoặc bỏ hẳn ô liên hệ; **không** để lại số điện thoại. Ghi lý do vào
  Change Log Report 1.
- Xoá khỏi **lịch sử git** (filter-repo/BFG) là việc riêng, **Hải quyết sau** theo tin nhắn — BA không tự rewrite lịch sử.
  **Chốt 03/11/2026: để nguyên lịch sử.** Đây là email/số điện thoại của chính sáu thành viên, đã nằm trong repo public từ
  đầu dự án; bản hiện tại đã sạch, còn viết lại lịch sử thì phải force-push main cùng hơn 200 nhánh, mọi người clone lại,
  các PR đang mở làm lại, và GitHub vẫn có thể giữ object cũ (phải gửi yêu cầu lên Support) — không đáng so với lợi ích.
- [x] Rà thêm Report 2–4 (DOCX) và toàn bộ `docs/` (trừ ảnh/PDF): không còn email hay số điện thoại thật; các địa chỉ
      còn lại là dữ liệu giả (`*@trustcabinet.local`, `*@trustsoft.vn`, `*@example.*`, `*@x.vn`). Hai địa chỉ gmail trong
      `docs/08-reports/test-reports/inventory.json` là dữ liệu mẫu chép từ mã test đăng nhập Google, không phải người thật.

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

## 5. Đã làm (03/11/2026)

- **Report 1:** bỏ hẳn **hai cột Email và Mobile** của bảng Project Stakeholders (giữ Full Name + Role, chia lại độ rộng cột
  cho kín trang), thêm dòng Change Log 03/11/2026 (M, D, KhanhPG) ghi lý do; `scripts/sync_reports.py --report 1` sinh lại
  Markdown. Kiểm lại: DOCX và Markdown **0 email, 0 số điện thoại**; `check_docx.py` OK.
- **README.md:** tách dòng 24 thành "Biểu quyết … Đã có" và "Biên bản, phê duyệt biên bản và tóm tắt bằng AI | Chưa làm";
  dòng Voice ID ghi thêm "tự điền tên khi giọng khớp rõ".
- **AGENTS.md:** bỏ câu "v0.1.0 … no authentication"; nêu trạng thái thật (vòng họp chạy được đầu-cuối, biểu quyết xong,
  biên bản/biên bản ký/AI summary còn placeholder UC-47–UC-51), bỏ số bảng cũ "27-table". **Không đụng các dòng server test**
  (`deploy/`, ADR-0047) như đã dặn.
- **UC-06c:** Description nêu việc kết thúc Voice ID; thêm **POST-4**; thêm bước **8** (kết thúc Voice ID trong cùng
  transaction, nhật ký ghi quản trị viên) và đánh số lại 9, 10; BR-365 thêm "và mọi tên do máy điền"; **BR-372 mới** cho
  ADR-0049 (UC-06c, domain Voice ID); UC-81a/UC-81f thêm câu quản trị viên vô hiệu hoá cũng kết thúc Voice ID.
- **Voice ID 1.2:** UC-81b Description + NF bước 1 (bản 1.2, mục đích mới, rút là xoá cả tên máy điền) + AF-1 (đồng ý bản mới
  thì máy được tự điền lại); BR-362 nêu bản 1.2 và điều kiện tự điền; UC-81a AF-1 nói người ở bản cũ chỉ được gợi ý tới khi
  đồng ý bản mới; UC-81c thêm **nghe lại trước khi lưu** (Dừng chưa gửi, nghe rồi Lưu hoặc Ghi lại) + Description.
  Không phải sửa screen spec: Report 3 **không có** màn "My voice" (màn hình dừng ở FE-08/FE-10).
- **Report 3 Change Log:** dòng 03/11/2026 (M, A) ghi cả SCRUM-130 và PR #225; `sync_reports.py --report 3` audit khớp 100%.
- **`scripts/check_docx.py` kiểm tra rộng hơn** (cùng lượt, theo yêu cầu): ngoài lỗi làm Word hỏi "recover", giờ bắt
  style không có trong `styles.xml`, heading rỗng/nhảy cấp, bảng lệch số cột hoặc header trống, chữ nằm ngoài `w:r`,
  và **hình dạng đặc tả của Report 3** — `UC-xx` duy nhất và đúng dạng, UC cha/con, đủ 8 dòng chuẩn, `PRE/POST/AF/EX`
  và các bước Normal Flow đánh số 1..n, mỗi `BR-n` một dòng tăng dần, mỗi cặp (mã MSG, ngữ cảnh) một dòng và một mã
  một nội dung, mọi `BR/MSG/UC` được UC nhắc tới phải tồn tại. Cảnh báo: `TODO/TBD`, dấu cách thừa, ảnh thiếu alt text.
  Nhờ đó tìm và sửa thêm hai lỗi cũ: **UC-05** có khối `AF-2` lặp và 4 bước 5–8 thừa do một lần merge, và yêu cầu bảo
  mật trỏ tới **UC-92** (không tồn tại) → **UC-13 View Audit Log**. README `08-reports` đã mô tả 5 nhóm kiểm tra.

## 6. Việc BA còn lại

- [x] Report 1: bỏ 6 email + 5 số điện thoại, sync report 1, DOCX + Markdown trong cùng commit.
- [x] README + AGENTS: trạng thái thật, **không đụng dòng server test**.
- [x] UC-06c + **BR-372** + UC-81a/81f theo §3.
- [x] UC-81b/81c, BR-361/362/366, screen spec 8.x theo §4 (screen spec: không có màn tương ứng).
- [x] Note 28 (PR #225) làm cùng lượt: BR-365/366/367, UC-45a/45c/46b, sequence.
- [x] Ghi kết quả vào SCRUM-130 trên Jira (đã xong 03/11: `Done` + comment 10425, xem README dòng 54).
- [ ] Còn lệch nhỏ chưa sửa (ngoài phạm vi note): AGENTS.md còn câu "chỉ có TrustCabinet.Web.Tests với smoke test
      `/api/health`" và "trên màn chi tiết mọi tab trừ Thông tin chung còn placeholder" — cập nhật khi rảnh.

## 7. Hỏi PO / Hải (đã chốt trong lúc làm)

- **Report 1:** đã bỏ hẳn **hai cột Email và Mobile** (không đưa email nhóm/dự án) — nếu PO muốn có một email chung thì
  thêm lại một cột, khi đó dùng email của nhóm chứ không dùng email cá nhân. Tên và đơn vị giữ nguyên.
- **Thông báo 1.2:** BA viết câu chữ theo ADR-0048 trong UC-81b; PO xem lại lời thông báo tiếng Việt trên app là chỗ duy nhất
  cần duyệt (Report 3 chỉ mô tả, không chép nguyên văn).
- **Vô hiệu hoá tài khoản:** đã đặt **BR-372 riêng** (actor là quản trị viên, khác BR-365 của người tự rút đồng ý).
