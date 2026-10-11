# Việc sửa docs ngày 10/10/2026 — bỏ Meeting Session, biểu quyết chỉ trong họp

> **Trạng thái 10/10/2026: ĐÃ THỰC HIỆN — chỉ còn giá trị lịch sử.** Đợt đồng bộ docs ngày 10/10/2026 đã áp dụng
> D1–D3 theo Report 1/2/3 bản DOCX mới. Khác với file này: danh sách UC **đã được gom và đánh số lại** (70 UC,
> FE-01 → FE-15, không còn "đóng băng UC-01 → UC-54"); số UC, BR, dòng nêu bên dưới là số cũ. Tra số mới ở
> [Master Use Case Inventory §5](01-requirements/00-master-use-case-inventory.md#5-bảng-đổi-số-uc-cũ--mới).
> Phần còn mở: mục `[B]` (API docs + code/migration) và cập nhật `03-api/00-danh-muc-ma-thong-bao.md`.


> **Đọc phần 0 → 5 trước khi sửa bất cứ file nào.** Phần A, B, C bên dưới là danh sách sửa
> chi tiết theo từng file, từng dòng, viết để một AI có giới hạn hoặc một thành viên mới làm
> theo **không cần tự quyết định**. Số dòng là số dòng tại commit `6e08966` của branch
> `docs/bf-meeting-no-session`. Tìm theo đoạn trích "Văn bản hiện tại" nếu số dòng đã lệch.

Nguồn sự thật cho đợt sửa này (đã push trên branch `docs/bf-meeting-no-session`):

| Nguồn | File |
|---|---|
| Business Flow đã PO duyệt | `docs/09-bussinessflow/BF-02…`, `BF-03…`, `BF-04….drawio.png` + `docs/09-bussinessflow/README.md` |
| ERD mục tiêu đã vẽ lại | `docs/assets/erd-muc-tieu/trustcabinet-erd-muc-tieu.drawio.png` |
| Wireframe 17 màn theo BF | `docs/assets/wireframe/luong-cuoc-hop.html` (mở bằng trình duyệt) |
| Tham chiếu nghiệp vụ | AI-eCabinet: không có cấp nào trên cuộc họp; "phiên họp" trên UI của họ là **agenda item** |

---

## 0. Ba quyết định của PO (09/10/2026)

**D1. Bỏ Meeting cha, `MeetingSession` đổi thành `Meeting`.** Một cuộc họp = một buổi họp. Meeting
tự mang lịch (`PlannedStartTime`, `PlannedEndTime`, `ActualStartedAt`, `ActualEndedAt`), hình thức,
địa điểm, `OnlineLink`, vòng đời `Draft → WaitingApproval → Approved | Rejected → Published →
InProgress → Ended`, agenda, thành phần, khách, tài liệu, biểu quyết, audio/transcript/tóm tắt,
biên bản. `AgendaItem.MeetingId` trỏ thẳng vào Meeting.

- FE-05 *Meeting Session Management* **không còn dùng**. Giữ số FE-05, ghi "Removed"; **không**
  đánh lại số FE-06 → FE-16.
- UC-26 → gộp vào UC-15 (lịch nhập khi tạo). UC-27 → gộp vào UC-16 (sau `Published` chỉ dời được
  giờ, địa điểm, link — *Reschedule* — và báo lại người được mời). UC-28 → bỏ. UC-29/UC-30 → trùng
  UC-43/UC-44. UC-53 → bỏ. **UC-01 → UC-54 đóng băng số**: các UC bỏ ghi "không còn dùng", giữ số,
  không tái cấp.
- UC-54 *View Meeting Agenda* giữ, đổi quan hệ thành `<<extend>>` UC-17. UC-31 → UC-36 giữ.
- Publish không còn đòi "có ít nhất một session Planned hợp lệ". End Meeting không còn kiểm tra
  "session chưa xong".

**D2. Bỏ biểu quyết đặc biệt.** Không có vote time-boxed / trước họp / sau họp, không quorum, không
có đối tượng Resolution bất biến. Vote chỉ tạo và mở khi Meeting `InProgress`: Chair tạo và mở →
người đủ điều kiện bỏ phiếu → Chair đóng → hệ thống đếm và hiện kết quả → Chair ghi kết luận
`Passed | NotPassed | Deferred` (Thông qua / Không thông qua / Hoãn). UC-72 đổi tên **Open Vote**.
FE-12 đổi tên **Voting Management**. Biên bản (FE-13) lấy "kết quả và kết luận biểu quyết" thay cho
"Resolution". Tên entity `VotingSession` **giữ nguyên**.

**D3. Secretary tự upload audio (UC-62).** Không có vai trò Audio Uploader.

**Không được đổi** (khác hẳn Meeting Session): `VotingSession`, `CallSession`, guest session
(UC-87), phiên đăng nhập / refresh token, "phiên bản" tài liệu.

---

## 1. Câu hỏi PO — đã điền sẵn mặc định để làm không bị kẹt

PO xem lại bảng này sáng 10/10. **Dòng nào PO chưa sửa thì dùng cột "Mặc định"**. Chỗ nào trong
phần A/B/C viết "PO-Qx" hoặc "chờ PO" thì thay bằng mặc định ở đây, và ghi vào ticket Jira.

| # | Câu hỏi | Mặc định dùng ngay | Lý do |
|---|---|---|---|
| Q1 | Có thêm UC "Cancel Meeting" không? | **Không thêm.** UC-28 ngừng dùng, không có UC thay | AI-eCabinet không có huỷ cuộc họp |
| Q2 | UC-76 đổi thành gì? | **Phương án A: đổi tên thành "Record Vote Conclusion"**, actor Chair | BF-03 đã có bước Chair ghi kết luận; danh mục đang thiếu UC cho bước này. Baseline = **88 UC đang dùng** |
| Q3 | Ai được dời lịch, ở trạng thái nào? | **Chỉ Meeting Organizer, chỉ khi `Published`.** Trước Published thì sửa bằng UC-16 bình thường | Khớp BF-02 |
| Q4 | Có lưu `OnlineLink` không? | **Có, tùy chọn, tối đa 1.000 ký tự.** Để trống thì dùng phòng họp trực tuyến của TrustCabinet | Cho phép link ngoài khi cần |
| Q5 | Agenda sửa được ở trạng thái nào? | **`Draft` và `Rejected`.** Khoá từ `WaitingApproval` | Giống AI-eCabinet; nội dung đã duyệt thì không đổi lén |
| Q6 | `PlannedEndTime` có bắt buộc không? | **Có**, và phải sau `PlannedStartTime` | |
| Q7 | Khi tạo, giờ bắt đầu có phải ở tương lai không? | **Có** | |
| Q8 | Kết thúc họp khi còn phiếu đang mở hoặc agenda đang chạy? | **Chặn kết thúc khi còn phiếu đang mở**, báo dùng mã mới `MSG139`. Agenda đang chạy thì cho kết thúc, hệ thống ghi mục đó là xong | Phiếu đang mở mà kết thúc thì mất kết quả |
| Q9 | Ai đủ điều kiện bỏ phiếu? | **Chair, Secretary, Member đang được phân công.** Guest không bỏ phiếu | |
| Q10 | `MinutesIssued` / `Archived` có trong vòng đời không? | **Không.** Xoá khỏi spec và lifecycle; trạng thái biên bản nằm ở `MeetingMinutes.Status` | Vòng đời PO chốt kết thúc ở `Ended` |
| Q11 | UC-43 Start Meeting có báo cho người tham dự không? | **Có**, thông báo trong ứng dụng | Khớp BF-03 |
| Q12 | Ai đứng tên dòng Change Log trong Report? | **HaiTV** (PO) | |

Mã thông báo ngừng dùng: **15 mã** `MSG48`–`MSG59`, `MSG77`, `MSG113`, `MSG127` (Phần A và B chỉ
liệt kê 14, thiếu `MSG77` của UC-28 — dùng danh sách 15 mã này).

Mã thông báo mới (lấy theo [Mã tiếp theo] của catalogue): **`MSG138`** — dời lịch thành công (UC-16
Reschedule); **`MSG139`** — không kết thúc được vì còn phiếu đang mở (UC-44, theo Q8). Mã tiếp theo
sau đợt này: **`MSG140`**.

---

## 2. Thứ tự làm (mỗi bước một commit, commit message tiếng Việt)

1. **Requirements** — Phần A: `00-master-use-case-inventory.md`, `05-use-case-specifications.md`,
   `06-business-rules.md`, `README.md`. **Cùng commit** sửa `docs/03-api/00-danh-muc-ma-thong-bao.md`
   (mục mã thông báo ở Phần C), vì test đọc catalogue cùng lúc với spec.
2. **Thiết kế** — Phần C mục `docs/02-design/`: vòng đời, ERD Markdown (khớp ERD đã vẽ, xem mục 3),
   `04-luong-nghiep-vu-chinh.md` (viết lại theo BF mới, văn bản đầy đủ có trong Phần C).
3. **ADR** — viết mới ADR-0013 *Bỏ Meeting Session* và ADR-0014 *Biểu quyết chỉ trong cuộc họp*
   (dàn ý ở Phần C). **Không sửa nội dung ADR đã chấp nhận**, chỉ đổi dòng trạng thái theo chỉ dẫn.
4. **Sơ đồ** — sequence diagram (xoá cả thư mục `meeting-session-management/`, sửa `meeting-management/`,
   `agenda-management/`), class diagram (xoá FE-05, sửa FE-04, FE-06), use case diagram (6 file `.drawio`).
   Sửa xong phải xuất lại PNG.
5. **Reports** — Phần B. **Nguồn thật là DOCX trên Google Drive**: sửa DOCX, tải về
   `docs/08-reports/source/`, chạy `python3 scripts/sync_reports.py`. **Không sửa tay** 3 file `.md`
   trong `docs/08-reports/` (lần sync sau sẽ ghi đè). Kiểm tra bằng mắt hình trong DOCX (context
   diagram, ERD, UC diagram) xem còn Meeting Session / Resolution không.
6. **API docs `[B]`** — các mục gắn `[B]` trong Phần C mô tả code đang chạy: **chỉ sửa cùng commit
   với migration code**, không sửa trước. Mục `[A]` (banner cảnh báo) thì sửa ngay.
7. **Code** — để sau, qua migration mới (migration đã lên `main` là bất biến). Danh sách file ở cuối
   Phần C, mục "Code — để sau".

Sau mỗi bước, ghi một comment vào ticket Jira liên quan: đổi gì, theo quyết định nào (D1/D2/D3, Qx).

---

## 3. ERD Markdown phải khớp ERD đã vẽ — bổ sung cho Phần C

Phần C hướng dẫn sửa `docs/02-design/02-erd.md`. ERD vẽ ở
`docs/assets/erd-muc-tieu/` có thêm các điểm dưới đây; **thêm cả những điểm này vào `02-erd.md`**
để hai bản khớp nhau (29 thực thể):

| Thực thể | Bổ sung so với Phần C |
|---|---|
| `VotingSession` | Thêm `OpenedAt timestamptz?`, `ClosedAt timestamptz?`, `ConclusionNote string?`, `ConcludedById FK uuid?` (người ghi kết luận). Bỏ `Kind`, `OpensAt`, `ClosesAt` như Phần C |
| `Recording` | Thêm `UploadedById FK uuid` (Secretary upload); `Source` chỉ còn `Upload` (hệ thống không tự ghi âm) |
| `MeetingSummary` | Thêm `Status "Draft|Approved|Published"`, `ApprovedById FK uuid?`, `ApprovedAt`, `PublishedAt` (UC-68 duyệt và UC-69 công bố là hai bước) |
| `MeetingMinutes` | `Status` đổi thành `"Draft|PendingSignature|RevisionRequested|Sealed|Superseded"`; bỏ `ApprovedById`, `RejectionReason`, `IssuedAt`; thêm `RevisionNote string?`, `SealedById FK uuid?`, `SealedAt` (đặt rồi là khoá) — khớp BF-04 ký / yêu cầu sửa / niêm phong |
| `MinutesSignature` | Thêm `InvalidatedAt timestamptz?` (biên bản bị sửa sau khi ký thì chữ ký cũ bị huỷ, UC-93) |
| Quan hệ thêm | `User 1 → 0..* VoteReceipt` (bỏ phiếu), `User 1 → 0..* MinutesSignature` (ký) |

Còn một điểm chưa xử lý trong ERD: `CallSession.RecordingId` (phòng họp ghi âm) mâu thuẫn với
"hệ thống không tự ghi âm". **Giữ nguyên lần này**, ghi vào mục "Khác biệt cần xử lý" của `02-erd.md`.

---

## 4. Quy tắc cho người / AI thực hiện

- Ngôn ngữ: `docs/` tiếng Việt (giữ thuật ngữ tiếng Anh như *endpoint*, *migration*); UC
  Specification và Reports viết tiếng Anh như phần còn lại của file; commit message tiếng Việt.
- **Không đánh lại số** UC, BR, MSG, FE. Thứ bỏ thì ghi "không còn dùng" và lý do, giữ dòng.
- Mỗi mã MSG mới phải thêm vào catalogue (dòng bảng + "Mã tiếp theo") **trong cùng commit** với chỗ
  dùng nó. Khi sửa catalogue **giữ nguyên cột UC-xx** vì `MessageCodeCatalogueTests` đọc cột đó.
- Hai phần A/B/C mâu thuẫn nhau thì theo thứ tự: **phần 0–3 của file này > Phần A > Phần C > Phần B**.
- Gặp chỗ không có trong danh sách mà vẫn nhắc session/quorum/resolution: sửa theo D1/D2 và ghi
  thêm một dòng vào ticket; không tự sáng tác tính năng mới.
- Không đụng `VotingSession`, `CallSession`, guest session, phiên đăng nhập.

---

## 5. Kiểm tra trước khi push

```bash
# 1. Không còn Meeting Session / quorum / resolution / time-boxed ngoài chỗ ghi "không còn dùng"
grep -rniE "meeting ?session|meetingsession|phiên họp|quorum|resolution|nghị quyết|time-boxed|audio uploader" \
  docs AGENTS.md README.md --include='*.md' | grep -viE "không còn dùng|retired|removed|ngừng dùng|ADR-0013|ADR-0014|NOTE-sua-docs"

# 2. UC bỏ vẫn còn số trong danh mục, ghi "không còn dùng"
grep -nE "UC-(26|27|28|29|30|53)" docs/01-requirements/00-master-use-case-inventory.md

# 3. Catalogue mã thông báo và test
dotnet test TrustCabinet.sln --filter MessageCodeCatalogueTests
```

Lệnh 1 phải chỉ còn các dòng giải thích lý do bỏ. Lệnh 3 phải xanh.

**Lệch có sẵn phát hiện khi rà, sửa luôn trong đợt này:**

- UC Specification dòng ~2641–2653: một mảng bảng BR-93 → BR-103 dán hỏng, trùng số nhưng khác nội
  dung danh mục chuẩn → xoá mảng đó.
- UC-21 dẫn chiếu BR-89 (uỷ quyền) cho điều kiện Chair/Secretary → đổi thành BR-124/BR-125.
- BR-58 trong Report 3 bản Markdown có ô Rule Statement trống (BR-58 sẽ ngừng dùng, ghi lý do).
- ADR-0010 và kế hoạch phòng họp dùng `MSG65` cho "phòng chưa mở", trong khi catalogue cấp `MSG65`
  cho UC-36 và `MSG117` cho phòng không khả dụng → sửa tài liệu về `MSG117`.
- Index ADR ghi 0010/0011 là "Đề xuất" nhưng file ghi "Đã chấp nhận" → index theo file.
- `AGENTS.md` nhắc `frontend/src/pages/scopeData.ts` nhưng file không còn tồn tại → bỏ câu đó.
- README gốc ghi 28 bảng, `AGENTS.md` ghi 27 → sửa sau migration theo số thật.
- `03-co-so-du-lieu.md` có câu SQL mẫu dùng cột `starts_at` đã bị drop → sửa cùng migration `[B]`.

---

# PHẦN A — `docs/01-requirements/` (Inventory, UC Specification, Business Rules)

## Audit `docs/01-requirements/` theo quyết định PO 09/10/2026 (D1, D2, D3)

Branch `docs/bf-meeting-no-session`. Số dòng là **số dòng hiện tại** (trước khi sửa). Áp dụng
**từ dưới lên trong mỗi file** (dòng lớn trước) để số dòng phía trên không bị lệch; hoặc tìm theo
"Văn bản hiện tại" (trích ngắn, duy nhất trong file).

Quy ước dùng trong danh sách:

- **THAY** = thay toàn bộ dòng/khối được trích bằng văn bản mới (giữ nguyên markdown xung quanh).
- **XOÁ** = xoá dòng/khối.
- **THÊM** = chèn văn bản mới vào vị trí ghi rõ.
- **RETIRE** = đánh dấu "không còn dùng", giữ mã, không đánh lại số, không tái cấp.
- **GIỮ** = đã rà, không sửa (ghi để người làm không phải tự quyết).
- Văn bản mới trong UC Specification giữ **tiếng Anh** như phần còn lại của file đó; văn bản mới
  trong Inventory/README/ghi chú giữ **tiếng Việt** như file đó.

Các khái niệm **không được đổi** (khác Meeting Session): `guest session` / "Meeting-scoped Guest
Session" (UC-87, UC-41, UC-45), "phiên đăng nhập" / `authenticated sessions` / `server-side session`
(UC-01..08, BR-18, BR-20), "phiên bản" (version tài liệu), `VotingSession`, `CallSession`. Các dòng
chứa chúng được liệt kê ở mục cuối của từng file là **GIỮ**.

---

## A. `docs/01-requirements/00-master-use-case-inventory.md` (1472 dòng)

### A-01 · Dòng 3 · header Baseline
- Hiện tại: `> **Baseline:** 05/10/2026 — Bản đã review sau khi đối soát Report 1, Report 2, Report 3 và các review vòng 1–2  `
- **THAY** bằng:
  `> **Baseline:** 09/10/2026 — Áp dụng quyết định PO 09/10/2026 (bỏ Meeting Session, bỏ biểu quyết đặc biệt) lên bản review 05/10/2026 đã đối soát Report 1, Report 2, Report 3 và các review vòng 1–2  `

### A-02 · Sau dòng 19 (cuối mục "Các quyết định PO mới nhất đã áp dụng") · THÊM khối mới
- Hiện tại dòng 19: `- Thêm UC-94 **Upload Document to Personal Library**.`
- **THÊM** ngay sau dòng 19 (để một dòng trống trước):

```markdown
### Quyết định PO 09/10/2026 (xem [Business Flow README](../09-bussinessflow/README.md))

- **Bỏ Meeting cha, `MeetingSession` đổi thành `Meeting`.** Một cuộc họp = một buổi họp. Meeting tự
  mang lịch (Planned Start/End Time, Meeting Mode, Location / Online Link), vòng đời
  (Draft → WaitingApproval → Approved | Rejected → Published → InProgress → Ended), agenda,
  thành phần tham dự, Guest, tài liệu, biểu quyết, audio/transcript/summary và biên bản.
- **FE-05 Meeting Session Management không còn dùng.** UC-26, UC-27, UC-28, UC-29, UC-30, UC-53
  đánh dấu *không còn dùng* — giữ mã, không đánh lại số, không tái cấp.
- UC-26 gộp vào **UC-15** (lịch nhập khi tạo cuộc họp). UC-27 gộp vào **UC-16** (sau Published chỉ
  được dời giờ / địa điểm / link — *Reschedule* — do Meeting Organizer, hệ thống báo lại cho người
  được mời). UC-29/UC-30 trùng **UC-43/UC-44**. UC-28 bỏ (PO chưa chốt có thêm Cancel Meeting
  hay không). UC-53 bỏ.
- **UC-54** giữ, đổi quan hệ thành `<<extend>>` UC-17. **UC-31 → UC-36** giữ; Agenda Item thuộc
  Meeting.
- **Bỏ biểu quyết đặc biệt:** không có vote time-boxed / trước họp / sau họp, không quorum, không có
  đối tượng Resolution bất biến. Biểu quyết chỉ diễn ra khi cuộc họp `InProgress`: Chair tạo và mở,
  participant đủ điều kiện bỏ phiếu, Chair đóng, hệ thống đếm phiếu và hiển thị kết quả, Chair ghi
  kết luận **Thông qua / Không thông qua / Hoãn**.
- UC-72 đổi tên thành **Open Vote**. UC-76 *View Resolution*: **PO cần chốt** (xem FE-12).
- Biên bản (FE-13) lấy **kết quả và kết luận biểu quyết** thay cho "Resolution".
```

### A-03 · Dòng 21–27 · "Quy ước trạng thái" · THÊM bullet
- Hiện tại dòng 27: `> - Các UC mới tiếp tục từ UC-55 trở đi; không renumber chỉ để gom UC của cùng Feature cho liền số.`
- **THÊM** sau dòng 27:
  `> - **KHÔNG CÒN DÙNG**: UC/Feature đã bị PO loại khỏi scope. Giữ mã để truy vết, không đếm vào baseline, không đánh lại số, không tái cấp cho nghĩa khác.`

### A-04 · Mục 0 "Các quyết định review đã chốt" · bảng dòng 33–51 · THÊM 4 dòng
- **THÊM** 4 dòng vào cuối bảng (sau dòng 51 `| Priority | FE-01 → FE-14 = ...`):

```markdown
| Meeting Session (FE-05) | **PO chốt 09/10/2026:** bỏ. Một cuộc họp = một buổi họp; lịch, vòng đời, agenda thuộc Meeting. UC-26 → UC-30, UC-53 không còn dùng. |
| Dời lịch sau Published | Gộp vào UC-16 (AF Reschedule): Meeting Organizer chỉ đổi giờ, địa điểm, online link; hệ thống báo lại cho người được mời. **PO cần chốt** Chair có được dời lịch không. |
| Hủy cuộc họp | UC-28 bỏ. **PO cần chốt** có thêm UC Cancel Meeting (mã mới từ UC-95) hay không; AI-eCabinet không có hủy. |
| Biểu quyết đặc biệt | **PO chốt 09/10/2026:** bỏ vote time-boxed / trước họp / sau họp, quorum, Resolution bất biến. Vote chỉ khi meeting `InProgress`; Chair ghi kết luận Thông qua / Không thông qua / Hoãn. |
```

### A-05 · Dòng 53 · tổng baseline
- Hiện tại: `**Baseline kế hoạch hiện tại: 94 UC.**`
- **THAY** bằng (phương án mặc định = UC-76 phương án A "Record Vote Conclusion"):
  `**Baseline kế hoạch hiện tại: 88 UC đang dùng** (mã UC-01 → UC-94; 6 mã không còn dùng: UC-26, UC-27, UC-28, UC-29, UC-30, UC-53).`
- Nếu PO chọn **phương án B** (bỏ UC-76): ghi `87 UC đang dùng` và `7 mã không còn dùng: UC-26, UC-27, UC-28, UC-29, UC-30, UC-53, UC-76`.
- Nếu PO thêm Cancel Meeting: cộng thêm 1 (UC-95) và sửa câu tương ứng.

### A-06 · Dòng 120–126 · mục 1.3 "Lifecycle không phải include/extend"
- Dòng 122 hiện tại: `Tạo Session → Start Session → End Session` → **XOÁ** dòng.
- Dòng 121 hiện tại: `Tạo Meeting → Gửi duyệt → Duyệt → Publish → Start → End` → **GIỮ**.
- Dòng 124 hiện tại: `Mở Vote → Cast Vote → Close Vote → Resolution`
  → **THAY** bằng: `Tạo Vote → Mở Vote → Cast Vote → Close Vote → Kết luận (Thông qua / Không thông qua / Hoãn)`

### A-07 · Dòng 263–301 · FE-04 Meeting Management
- Dòng 270 (UC-15) `| UC-15 | Tạo cuộc họp *(Create Meeting)* | ĐÃ ĐẶC TẢ | Meeting Organizer | ...` → **GIỮ** (actor/quan hệ không đổi).
- Dòng 271 hiện tại: `| UC-16 | Cập nhật cuộc họp *(Update Meeting)* | ĐÃ ĐẶC TẢ | Meeting Organizer | `<<extend>>` UC-17 |`
  → **THAY** bằng:
  `| UC-16 | Cập nhật cuộc họp *(Update Meeting)* — gồm dời lịch sau Published *(Reschedule)* | ĐÃ ĐẶC TẢ — cần cập nhật AF Reschedule | Meeting Organizer | `<<extend>>` UC-17 |`
- **THÊM** sau dòng 289 (sau khối ```` ``` ```` của sơ đồ FE-04), trước `### Không tạo UC mới cho`:

```markdown
### Lịch và dời lịch (PO chốt 09/10/2026)

- Lịch họp (Planned Start Time, Planned End Time, Meeting Mode, Location / Online Link) nhập ngay
  trong UC-15; không còn Meeting Session.
- Ở `Draft` / `Rejected`, UC-16 sửa mọi thông tin. Ở `Published`, UC-16 chỉ cho **Reschedule**:
  đổi giờ, địa điểm, online link; Meeting Organizer thực hiện; hệ thống báo lại cho mọi người được mời.
- UC-43 Start / UC-44 End không còn kiểm tra session; UC-21 Publish không còn yêu cầu session.
```

### A-08 · Dòng 305–327 · FE-05 · RETIRE toàn bộ section
- Hiện tại dòng 305–306: `## FE-05 — Quản lý phiên họp  ` / `**Meeting Session Management**`; dòng 308 `**Priority:** Must-have`; bảng 310–317; khối sơ đồ 319–327.
- **THAY** toàn bộ dòng 305–327 bằng:

```markdown
## FE-05 — Quản lý phiên họp — KHÔNG CÒN DÙNG  
**Meeting Session Management (không còn dùng từ 09/10/2026)**

> PO chốt 09/10/2026: bỏ cấp Meeting Session — một cuộc họp = một buổi họp. Lịch, vòng đời và
> agenda thuộc Meeting (FE-04, FE-06). Mã FE-05 và các mã UC dưới đây giữ nguyên để truy vết,
> không đánh lại số, không tái cấp.

| UC | Use Case | Trạng thái | Thay bằng |
|---|---|---|---|
| UC-26 | Tạo phiên họp *(Create Meeting Session)* | KHÔNG CÒN DÙNG | Lịch nhập trong UC-15 Create Meeting |
| UC-27 | Cập nhật phiên họp *(Update Meeting Session)* | KHÔNG CÒN DÙNG | UC-16 Update Meeting (AF Reschedule sau Published) |
| UC-28 | Hủy phiên họp *(Cancel Meeting Session)* | KHÔNG CÒN DÙNG | Không có UC thay thế — PO chưa chốt Cancel Meeting |
| UC-29 | Bắt đầu phiên họp *(Start Meeting Session)* | KHÔNG CÒN DÙNG | UC-43 Start Meeting |
| UC-30 | Kết thúc phiên họp *(End Meeting Session)* | KHÔNG CÒN DÙNG | UC-44 End Meeting |
| UC-53 | Xem các phiên họp *(View Meeting Sessions)* | KHÔNG CÒN DÙNG | Lịch hiển thị trong UC-17 View Meeting Details |
```

### A-09 · Dòng 331–355 · FE-06 Agenda
- **THÊM** sau dòng 334 (`**Priority:** Must-have`), trước bảng:
  `Agenda Item thuộc trực tiếp **Meeting** (không còn Meeting Session). Secretary soạn agenda khi chuẩn bị cuộc họp; Chair điều hành agenda khi cuộc họp ` + "`InProgress`" + `.`
- Dòng 344 hiện tại: `| UC-54 | Xem Agenda của phiên họp *(View Meeting Agenda)* | ĐÃ ĐẶC TẢ | Actor có quyền | `<<extend>>` UC-53 |`
  → **THAY** bằng:
  `| UC-54 | Xem chương trình nghị sự của cuộc họp *(View Meeting Agenda)* | ĐÃ ĐẶC TẢ | Actor có quyền xem meeting | `<<extend>>` UC-17 |`
- Dòng 347 hiện tại: `UC-54 --<<extend>>--> UC-53` → **THAY** bằng: `UC-54 --<<extend>>--> UC-17`
- Dòng 338–343 (UC-31 → UC-36): **GIỮ** (actor và quan hệ `<<extend>>` UC-54 không đổi).

### A-10 · Dòng 523–558 · FE-10 / UC-62 (D3)
- Dòng 531, 540–549: đã đúng D3 (Secretary upload, không Audio Uploader) → **GIỮ**. Không còn tài liệu nào trong `docs/01-requirements/` nói ngược lại.

### A-11 · Dòng 619–638 · FE-12 Voting · THAY toàn bộ section
- Hiện tại: dòng 619 `## FE-12 — Biểu quyết & Nghị quyết  `, 620 `**Voting & Resolution Management**`, bảng 624–632, đoạn 634–638 (`**Resolution là business outcome chính thức**…`, `> Kết quả được ghi thành Resolution gắn vào meeting và bất biến.`).
- **THAY** toàn bộ dòng 619–638 bằng (phương án A mặc định; phương án B ghi ngay dưới):

```markdown
## FE-12 — Biểu quyết trong cuộc họp  
**Voting Management**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-70 | Xem các biểu quyết của meeting *(View Meeting Votes)* | ĐÃ LÊN KẾ HOẠCH | Actor có quyền | `<<extend>>` UC-17 |
| UC-71 | Tạo / cấu hình biểu quyết *(Create / Configure Vote)* | ĐÃ LÊN KẾ HOẠCH | Chair | `<<extend>>` UC-70 |
| UC-72 | Mở biểu quyết *(Open Vote)* | ĐÃ LÊN KẾ HOẠCH | Chair | `<<extend>>` UC-70 |
| UC-73 | Bỏ phiếu *(Cast Vote)* | ĐÃ LÊN KẾ HOẠCH | Participant đủ điều kiện | `<<extend>>` UC-70 |
| UC-74 | Xem trạng thái và kết quả biểu quyết *(View Vote Status)* | ĐÃ LÊN KẾ HOẠCH | Actor theo rule visibility | `<<extend>>` UC-70 |
| UC-75 | Đóng biểu quyết *(Close Vote)* | ĐÃ LÊN KẾ HOẠCH | Chair | `<<extend>>` UC-70 |
| UC-76 | Ghi kết luận biểu quyết *(Record Vote Conclusion)* — **PO cần chốt** | ĐÃ LÊN KẾ HOẠCH | Chair | `<<extend>>` UC-70 |

### Quy tắc đã chốt (PO 09/10/2026)

- Biểu quyết chỉ diễn ra khi cuộc họp `InProgress`, thường gắn với một agenda item. Không có
  biểu quyết trước họp, sau họp hay có hạn giờ (time-boxed).
- UC-71 không cấu hình quorum, không cấu hình deadline.
- UC-72 chỉ mở được khi cuộc họp `InProgress`.
- Sau UC-75, hệ thống đếm phiếu và hiển thị kết quả (xem bằng UC-74). Chair ghi kết luận
  **Thông qua / Không thông qua / Hoãn**.
- Không có đối tượng "Resolution" bất biến riêng. Biên bản (FE-13) lấy kết quả và kết luận biểu quyết.

### UC-76 — PO cần chốt một trong hai phương án

- **Phương án A (mặc định trong bảng trên):** đổi UC-76 *View Resolution* thành **Record Vote
  Conclusion** — Chair ghi kết luận Thông qua / Không thông qua / Hoãn sau khi đóng vote. Đây là bước
  có trong AI-eCabinet mà danh mục đang thiếu. FE-12 giữ 7 UC.
- **Phương án B:** bỏ UC-76, đánh dấu *không còn dùng* (vì xem kết quả đã nằm trong UC-74). Khi đó
  thay dòng UC-76 trong bảng bằng
  `| UC-76 | Xem nghị quyết *(View Resolution)* | KHÔNG CÒN DÙNG | — | — |`, FE-12 còn 6 UC, và
  bước "Chair ghi kết luận" phải được đưa vào UC-75 Close Vote.
```

### A-12 · Dòng 642–669 · FE-13 Minutes · THÊM nguồn biên bản
- Bảng 647–656 (UC-77 → UC-83, UC-93): **GIỮ**.
- **THÊM** sau dòng 656 (dòng UC-93), trước `### Rule sửa minutes sau khi đã bước vào signing`:

```markdown
### Nội dung draft minutes (UC-78)

Draft minutes tổng hợp: thông tin cuộc họp (lịch, thành phần, điểm danh), agenda, summary đã duyệt,
và **kết quả + kết luận của từng biểu quyết** (Thông qua / Không thông qua / Hoãn). Không có đối
tượng "Resolution" riêng.
```

### A-13 · Dòng 684–692 · FE-14 Notification scope
- Dòng 688 hiện tại: `- session/agenda change nếu áp dụng;`
  → **THAY** bằng: `- dời lịch cuộc họp sau Published (Reschedule) — gửi cho mọi người được mời; thay đổi agenda nếu áp dụng;`
- Dòng 691 `- voting call;` → **GIỮ** (vote mở trong cuộc họp vẫn có thể báo).

### A-14 · Dòng 707–713 · FE-15 Dashboard
- Dòng 712 `- open votes;` → **GIỮ** (vote chỉ mở khi meeting `InProgress`, card vẫn hợp lệ).

### A-15 · Dòng 729–735 · FE-16 Reporting
- Dòng 733 hiện tại: `- decisions;` và dòng 734: `- resolutions;`
  → **THAY** hai dòng bằng một dòng: `- kết quả và kết luận biểu quyết (Thông qua / Không thông qua / Hoãn);`

### A-16 · Dòng 741–845 · Mục 5 "Ma trận quan hệ `<<extend>>`"
- Dòng 767–773 hiện tại (khối `# FE-05` gồm `UC-53 --<<extend>>--> UC-17` và UC-26..UC-30 `--<<extend>>--> UC-53`)
  → **THAY** bằng hai dòng:
  ```text
  # FE-05 — không còn dùng (UC-26 → UC-30, UC-53 không vẽ)
  ```
  (một dòng chú thích + một dòng trống).
- Dòng 776 hiện tại: `UC-54  --<<extend>>--> UC-53` → **THAY** bằng: `UC-54  --<<extend>>--> UC-17`
- Dòng 820–826 (FE-12): **GIỮ** nếu phương án A. Nếu phương án B: **XOÁ** dòng 826 `UC-76  --<<extend>>--> UC-70`.

### A-17 · Dòng 898–912 · Mục 6 Lifecycle "Meeting"
- **THAY** khối text 900–912 bằng:

```text
UC-15 Create (gồm lịch) → Draft
        ↓ UC-19 Submit
WaitingApproval
        ↓ UC-20 Approve / Reject
Approved / Rejected            (Rejected → UC-16 sửa → UC-19 gửi lại)
        ↓ UC-21 Publish (nếu Approved)
Published ──┐
        │   └─ UC-16 AF Reschedule (chỉ giờ / địa điểm / link) → vẫn Published, báo người được mời
        ↓ UC-43 Start
InProgress
        ↓ UC-44 End
Ended
```

### A-18 · Dòng 914–922 · Mục 6 Lifecycle "Meeting Session"
- Hiện tại: `## Meeting Session` + khối `UC-26 Create → Planned … └─ UC-30 End → Completed`.
- **XOÁ** toàn bộ dòng 914–923 (heading, khối code và dòng trống sau nó).

### A-19 · Dòng 924–933 · Lifecycle "Agenda Item"
- **THAY** dòng 927 `UC-31 Create → Pending` bằng `UC-31 Create → Pending        (khi meeting Draft / Rejected / Approved / Published)`
- **THAY** dòng 931 `                └─ UC-35 Set Current → InProgress` bằng `                └─ UC-35 Set Current → InProgress   (khi meeting InProgress)`
- Các dòng khác **GIỮ**. (Khoảng thời gian soạn agenda: xem câu hỏi mở Q3.)

### A-20 · Dòng 935–946 · Lifecycle "Guest"
- Dòng 942 `Meeting-scoped Guest Session` → **GIỮ** (guest session, khác Meeting Session).

### A-21 · Dòng 992–1006 · Lifecycle "Voting" · THAY khối
- **THAY** khối text 994–1006 bằng (phương án A):

```text
Meeting InProgress (agenda item hiện tại)
   ↓
UC-71 Create / Configure Vote (không quorum, không deadline)
   ↓
UC-72 Open Vote
   ├─ UC-73 Cast Vote
   ├─ UC-74 View Status
   ↓
UC-75 Close
   ↓
Hệ thống đếm phiếu, hiển thị kết quả (UC-74)
   ↓
UC-76 Record Vote Conclusion: Thông qua / Không thông qua / Hoãn
   ↓
Kết quả + kết luận được đưa vào draft minutes (UC-78)
```
- Phương án B: thay dòng `UC-76 Record Vote Conclusion: …` bằng `Chair ghi kết luận trong UC-75: Thông qua / Không thông qua / Hoãn`.

### A-22 · Dòng 1008–1030 · Lifecycle "Minutes"
- Dòng 1011 hiện tại: `UC-78 Prepare Draft` → **THAY** bằng: `UC-78 Prepare Draft (thông tin họp + summary + kết quả/kết luận biểu quyết)`

### A-23 · Dòng 1034–1054 · Mục 7 "Thống kê số lượng UC"
- Dòng 1042 hiện tại: `| FE-05 | Meeting Session Management | 6 |`
  → **THAY** bằng: `| FE-05 | ~~Meeting Session Management~~ — không còn dùng (UC-26 → UC-30, UC-53) | 0 |`
- Dòng 1049 hiện tại: `| FE-12 | Voting & Resolution Management | 7 |`
  → **THAY** bằng: `| FE-12 | Voting Management | 7 |` (phương án B: `6`)
- Dòng 1054 hiện tại: `| **TỔNG** |  | **94** |`
  → **THAY** bằng: `| **TỔNG** |  | **88** |` (phương án B: `**87**`; nếu thêm Cancel Meeting: +1 và FE-04 thành 10)
- **THÊM** sau dòng 1054: `Mã không còn dùng (không đếm): UC-26, UC-27, UC-28, UC-29, UC-30, UC-53` (phương án B thêm `, UC-76`).

### A-24 · Dòng 1141–1151 · Mục 9.5 "Notification BR"
- Dòng 1147 hiện tại: `- agenda/session changes;` → **THAY** bằng: `- agenda changes và dời lịch cuộc họp (Reschedule);`

### A-25 · Mục 11 "Kế hoạch đồng bộ Report 1 / 2 / 3" · THÊM mục 11.5
- **THÊM** sau dòng 1381 (`- Không tạo actor Audio Uploader.`), trước `---` dòng 1383:

```markdown

## 11.5 Đồng bộ quyết định PO 09/10/2026

### Report 1

- FE-05: đánh dấu *không còn dùng*; FE-04 mô tả lịch nằm trên Meeting và Reschedule sau Published.
- FE-06: Agenda Item thuộc Meeting, không thuộc Session.
- FE-12: bỏ "time-boxed vote… before or after", "quorum rule", "cannot be altered"; đổi tên feature
  theo mục FE-12 ở trên.
- GAP-05: bỏ "immutable record of resolutions". FE-16: bỏ "resolutions".

### Report 2

- Iteration voting: bỏ "both voting modes" và "resolution".

### Report 3

- Đánh dấu UC-26 → UC-30, UC-53 *không còn dùng*; cập nhật UC-15, UC-16 (AF Reschedule), UC-17,
  UC-18, UC-19, UC-21, UC-31 → UC-36, UC-43, UC-44, UC-45 → UC-52, UC-54.
- BR: xem [06-business-rules.md](06-business-rules.md) — các BR Meeting Session *không còn dùng*.
- §12 Voting: theo FE-12 ở trên.
```

### A-26 · Dòng 1374 · Mục 11.4 Business Flow (D3)
- Dòng 1374 `- BF-02: bỏ bước assign designated audio uploader.` → **THAY** bằng:
  `- BF-02: bỏ bước assign designated audio uploader. *(Đã làm khi vẽ lại BF 09/10/2026.)*`
- Dòng 1375 `- BF-04: lane Secretary thực hiện Upload Meeting Audio.` → **THAY** bằng:
  `- BF-04: lane Secretary thực hiện Upload Meeting Audio. *(Đã làm 09/10/2026.)*`

### A-27 · Dòng 1401–1445 · Mục 13 "Cách chia Use Case Diagram"
- Dòng 1403 `Không vẽ 94 UC vào một hình.` → **THAY** bằng `Không vẽ 88 UC vào một hình.` (phương án B: 87)
- Dòng 1409 `   - 16 FE package / representative UC` → **THAY** bằng `   - 15 FE package (FE-05 không còn dùng) / representative UC`
- Dòng 1417–1418 (`4. **FE-04 Meeting**` / `   - UC-15 → UC-21, UC-43, UC-44`) → **GIỮ**.
- Dòng 1420 `5. **FE-05 + FE-06 Session & Agenda**` → **THAY** bằng `5. **FE-06 Agenda**`
- Dòng 1421 `   - UC-26 → UC-36, UC-53, UC-54` → **THAY** bằng `   - UC-31 → UC-36, UC-54`
- Dòng 1435 `10. **FE-12 Voting & Resolution**` → **THAY** bằng `10. **FE-12 Voting**`
- Dòng 1436 `    - UC-70 → UC-76` → **GIỮ** (phương án B: `    - UC-70 → UC-75`)

### A-28 · Dòng 1449–1472 · Mục 14 "Quy tắc baseline cuối cùng"
- Dòng 1451 `1. UC-01 → UC-54 giữ nguyên ID.` → **THAY** bằng:
  `1. UC-01 → UC-54 giữ nguyên ID. UC-26 → UC-30 và UC-53 không còn dùng (09/10/2026) — giữ mã, không tái cấp.`
- Dòng 1452 `2. UC-55 → UC-94 được reserve trong master này.` → **THAY** bằng:
  `2. UC-55 → UC-94 được reserve trong master này. UC mới (ví dụ Cancel Meeting nếu PO chốt) bắt đầu từ UC-95.`
- Dòng 1458 `8. Lifecycle Meeting/Session/Agenda/Vote/Minutes không phải `include`.` → **THAY** bằng:
  `8. Lifecycle Meeting/Agenda/Vote/Minutes không phải `include`.`
- Dòng 1469 `19. Resolution là business outcome bất biến của closed vote.` → **THAY** bằng:
  `19. Biểu quyết chỉ diễn ra khi meeting `InProgress`; kết quả là số phiếu đã đếm và kết luận Chair ghi (Thông qua / Không thông qua / Hoãn). Không có vote time-boxed / trước họp / sau họp, không quorum, không Resolution bất biến riêng.`
- **THÊM** sau dòng 1472 (cuối file):
  `23. Một cuộc họp = một buổi họp. Không có Meeting Session; lịch, vòng đời và agenda thuộc Meeting.`

### A-29 · Actor mapping (mục 2, dòng 132–163)
- Không có actor nào bị bỏ. **GIỮ**. Chỉ thay đổi gán actor theo UC: UC-26..UC-28 (Secretary) và
  UC-29/UC-30 (Chair) không còn; Reschedule thuộc **Meeting Organizer** (UC-16); UC-76 phương án A
  thuộc **Chair** (đã ghi trong A-11). Diagram theo actor ở `../assets/use-case-diagrams/` phải vẽ
  lại theo — **ngoài phạm vi file này**.

### A-30 · Các dòng đã rà và GIỮ (khái niệm khác)
- Dòng 107, 148, 198, 942: *guest session* — GIỮ.
- Dòng 407–408, 437: "phiên bản" (document version) — GIỮ.
- Dòng 12, 18, 41, 544–545, 1078, 1172, 1192, 1335–1336, 1355, 1367, 1381: "Audio Uploader" đều là
  câu *bỏ* Audio Uploader → khớp D3, GIỮ.
- Dòng 37 (UC-25 mở Vote/Minutes sau): GIỮ.

---

## B. `docs/01-requirements/05-use-case-specifications.md` (2662 dòng)

> File này chỉ đặc tả UC-01 → UC-54. **UC-62, UC-70 → UC-76, UC-78 không có trong file** → không có
> thay đổi D2/D3 ở đây; các thay đổi đó nằm ở Inventory (mục A).

### B-01 · Dòng 48–54 · Index "1.6 Quản lý phiên họp"
- Dòng 48 `### 1.6 Quản lý phiên họp (Meeting Session Management)` → **THAY** bằng:
  `### 1.6 Quản lý phiên họp (Meeting Session Management) — không còn dùng`
- Dòng 49–54: thêm hậu tố ` — *không còn dùng*` vào cuối **mỗi** dòng:
  - `- **UC-26**: Tạo phiên họp (Create Meeting Session) — *không còn dùng*`
  - `- **UC-27**: Cập nhật phiên họp (Update Meeting Session) — *không còn dùng*`
  - `- **UC-28**: Hủy phiên họp (Cancel Meeting Session) — *không còn dùng*`
  - `- **UC-29**: Bắt đầu phiên họp (Start Meeting Session) — *không còn dùng*`
  - `- **UC-30**: Kết thúc phiên họp (End Meeting Session) — *không còn dùng*`
  - `- **UC-53**: Xem danh sách phiên họp (View Meeting Sessions) — *không còn dùng*`
- Dòng 63 (UC-54) **GIỮ**.

### B-02 · Dòng 861–909 · UC-15 Create Meeting
- Dòng 867 hiện tại: `... Meeting Sessions are planned separately and are not required when the meeting is first created.`
  → **THAY** cả dòng bằng:
  `- **Mô tả (Description)**: Allows the Meeting Organizer to create a new meeting, including its planned schedule. One meeting is one sitting; there are no separate Meeting Sessions.`
- Dòng 875 hiện tại: `  - POST-4: Meeting Sessions may be planned separately when their schedule becomes known.`
  → **THAY** bằng: `  - POST-4: The meeting's Planned Start Time and Planned End Time are recorded on the meeting. (BR-25)`
- Dòng 883–887 (danh sách trường) → **THAY** dòng 883–887 bằng:
  ```
     - Meeting Name
     - Planned Start Time
     - Planned End Time
     - Meeting Mode
     - Location / Online Link
     - Confidentiality Level
     - Description
  ```
- Dòng 889 hiện tại: `5. The system validates the entered meeting-level information (BR-24).`
  → **THAY** bằng: `5. The system validates the entered meeting information, including the planned schedule (BR-24, BR-25, BR-62).`
- Dòng 901 hiện tại: `  3. The system displays MSG31 – "Invalid meeting information." and the corresponding validation messages.`
  → **THAY** bằng:
  ```
    3. The system displays MSG31 – "Invalid meeting information." and the corresponding validation messages, for example:
       - "Planned start time is required."
       - "Planned end time must be later than planned start time."
  ```

### B-03 · Dòng 913–961 · UC-16 Update Meeting (gộp UC-27 → Reschedule)
- Dòng 919 hiện tại: `... while the meeting is still editable. Meeting Session planning is handled separately.`
  → **THAY** cả dòng bằng:
  `- **Mô tả (Description)**: Allows the Meeting Organizer to modify the information of an existing meeting while it is in Draft or Rejected status, and to reschedule a Published meeting by changing only its time, location, or online link.`
- Dòng 924 hiện tại: `  - PRE-4: At the time the edit action is initiated, the meeting is in Draft or Rejected status.`
  → **THAY** bằng: `  - PRE-4: At the time the edit action is initiated, the meeting is in Draft or Rejected status (full edit), or in Published status (reschedule only, AF-1).`
- **THÊM** sau dòng 928 (POST-3):
  `  - POST-4: If a Published meeting was rescheduled, all current invitees are notified of the new schedule. (BR-76)`
- Dòng 936 hiện tại: `4. The system validates the submitted meeting-level information (BR-27).`
  → **THAY** bằng: `4. The system validates the submitted meeting information, including the planned schedule (BR-25, BR-27, BR-62).`
- Dòng 940–941 hiện tại: `#### Luồng sự kiện thay thế (Alternative Flows):` / `- N/A`
  → **THAY** dòng 941 `- N/A` bằng:
  ```
  - **AF-1: Reschedule a Published Meeting**
    1. At Step 1 of the Main Flow, the Meeting Organizer selects a Published meeting and chooses Reschedule.
    2. The system displays the current Planned Start Time, Planned End Time, Location, and Online Link. No other meeting information can be changed. (BR-27)
    3. The Meeting Organizer enters the new time, location, and/or online link and submits the changes.
    4. The system validates that only the time, location, or online link changed, that the Planned End Time is later than the Planned Start Time, and that the Planned Start Time is later than the current time. (BR-27, BR-62, BR-75)
    5. The system verifies that the meeting is still Published.
    6. The system saves the new schedule. The meeting remains Published. (BR-28)
    7. The system notifies all current internal participants and external Guests of the new schedule. A delivery failure does not undo the reschedule. (BR-76)
    8. The system displays MSG138 – "Meeting rescheduled successfully."
    9. The use case ends successfully.
  ```
- Dòng 946 hiện tại: `  1. At Step 4, the submitted information is invalid.`
  → **THAY** bằng: `  1. At Step 4 of the Main Flow or Step 4 of AF-1, the submitted information is invalid.`
- Dòng 949 hiện tại: `  4. The flow resumes at Step 4 of the Main Flow.`
  → **THAY** bằng: `  4. The flow resumes at the step where the error was detected (Step 4 of the Main Flow or Step 4 of AF-1).`
- Dòng 952 hiện tại: `  1. At Step 4, the system determines that the meeting status has changed and the meeting is no longer Draft or Rejected.`
  → **THAY** bằng: `  1. At Step 4 of the Main Flow, the system determines that the meeting is no longer Draft or Rejected; or at Step 5 of AF-1, the meeting is no longer Published.`
- Dòng 958 hiện tại: `  1. At Step 5, the system cannot complete the update.`
  → **THAY** bằng: `  1. At Step 5 of the Main Flow or Step 6 of AF-1, the system cannot complete the update.`
- Mã MSG138: xem mục E (mã mới, phải thêm vào catalogue cùng commit).

### B-04 · Dòng 965–1001 · UC-17 View Meeting Details
- Dòng 971 hiện tại: `... Meeting Sessions and agenda are handled by UC-53 and UC-54.`
  → **THAY** cả dòng bằng: `- **Mô tả (Description)**: Allows an authorized user to view meeting details, including the planned schedule. The agenda is handled by UC-54.`
- Dòng 976 hiện tại: `  - POST-1: Only meeting-level details are displayed.` → **THAY** bằng: `  - POST-1: Meeting details, including the planned schedule, are displayed.`
- Dòng 977 hiện tại: `  - POST-2: No Meeting, Meeting Session, or Agenda Item is modified.` → **THAY** bằng: `  - POST-2: No Meeting or Agenda Item is modified.`
- Dòng 984 hiện tại: `3. The system retrieves only the meeting-level information.` → **THAY** bằng: `3. The system retrieves the meeting information.`
- Dòng 985 hiện tại: `4. The system displays Meeting Name, Meeting Mode, Location, Confidentiality Level, Description, and Meeting Status. Meeting Sessions and Agenda Items are not loaded by this use case.`
  → **THAY** bằng: `4. The system displays Meeting Name, Planned Start Time, Planned End Time, Meeting Mode, Location / Online Link, Confidentiality Level, Description, and Meeting Status. Agenda Items are not loaded by this use case. (BR-25)`

### B-05 · Dòng 1005–1058 · UC-18 View Meeting List
- Dòng 1037 hiện tại: `   For Time Range, a meeting matches only when at least one non-cancelled Meeting Session has a planned schedule overlapping the selected range; meetings without a scheduled Meeting Session do not match. (BR-30)`
  → **THAY** bằng: `   For Time Range, a meeting matches when its planned schedule (Planned Start Time to Planned End Time) overlaps the selected range. (BR-30)`

### B-06 · Dòng 1062–1125 · UC-19 Submit Meeting for Approval
- Dòng 1085 hiện tại: `2. The system verifies that all mandatory meeting-level information is complete. Meeting Sessions are not required for approval submission. (BR-33, BR-55)`
  → **THAY** bằng: `2. The system verifies that all mandatory meeting information, including the planned schedule, is complete. (BR-33)`

### B-07 · Dòng 1215–1263 · UC-21 Publish Meeting
- Dòng 1221 hiện tại: `... initiate invitation delivery to current internal participants and external Guests. Meeting Sessions are optional.`
  → **THAY** cả dòng bằng: `- **Mô tả (Description)**: Allows the Meeting Organizer to publish an Approved meeting and initiate invitation delivery to current internal participants and external Guests.`
- Dòng 1227 `  - PRE-5: Meeting Sessions are optional and are not a publication prerequisite. (BR-56)` → **XOÁ**.
- Dòng 1228 hiện tại: `  - PRE-6: The meeting has at least one current Chair and one current Secretary with active accounts. Invitation Response is not a publication prerequisite. (BR-89, F08)`
  → **THAY** bằng (đổi số PRE-6 → PRE-5; sửa luôn BR dẫn chiếu sai — BR-89 là quy tắc ủy quyền, quy tắc Chair/Secretary khi công bố là BR-124/BR-125/BR-129):
  `  - PRE-5: The meeting has exactly one current Chair and at least one current Secretary with active accounts. Invitation Response is not a publication prerequisite. (BR-124, BR-125, BR-129)`
- Dòng 1233 `  - POST-4: Publishing does not create or modify Meeting Sessions; a meeting may be published with no sessions. (BR-56)` → **XOÁ**.
- Dòng 1240 hiện tại: `3. The system verifies ... have active accounts. Meeting Sessions are not required. (BR-37, BR-56, BR-89)`
  → **THAY** bằng: `3. The system verifies that the Meeting Organizer remains authorized for the selected meeting, that the meeting is still Approved, and that exactly one current Chair and at least one current Secretary have active accounts. (BR-37, BR-124, BR-125)`
- Dòng 1252 hiện tại: `... or no current Chair or Secretary has an active account. (BR-37, BR-89)`
  → **THAY** đoạn `(BR-37, BR-89)` bằng `(BR-37, BR-124, BR-125)`. Phần chữ giữ nguyên.
- *Ghi chú:* hai sửa BR-89 → BR-124/125 là lỗi có sẵn, không do D1; sửa luôn vì đang chạm đúng dòng đó. Nếu reviewer muốn tách, bỏ phần đổi BR, chỉ xoá chữ "Meeting Sessions…" và `BR-56`.

### B-08 · Dòng 1405–1660 · FEATURE 6 + UC-53, UC-26 → UC-30 · RETIRE
- **THAY** toàn bộ dòng 1405–1660 (từ `### FEATURE 6: QUẢN LÝ PHIÊN HỌP (MEETING SESSION MANAGEMENT)` đến hết EX-1 của UC-30, ngay trước `---` dòng 1661) bằng:

```markdown
### FEATURE 6: QUẢN LÝ PHIÊN HỌP (MEETING SESSION MANAGEMENT) — KHÔNG CÒN DÙNG

> **Không còn dùng từ 09/10/2026.** PO bỏ cấp Meeting Session: một cuộc họp = một buổi họp; lịch,
> vòng đời và agenda thuộc Meeting. Các mã UC dưới đây nằm trong dải đóng băng UC-01 → UC-54:
> giữ mã, không đánh lại số, không tái cấp. Đặc tả cũ xem lịch sử git.

### UC-53: View Meeting Sessions (Xem danh sách phiên họp) — KHÔNG CÒN DÙNG

> Bỏ. Lịch họp hiển thị trong UC-17 View Meeting Details. Mã thông báo `MSG127` ngừng dùng.

### UC-26: Create Meeting Session (Tạo phiên họp) — KHÔNG CÒN DÙNG

> Gộp vào UC-15 Create Meeting: lịch họp nhập ngay khi tạo cuộc họp. Mã thông báo `MSG48`, `MSG53`, `MSG54` ngừng dùng.

### UC-27: Update Meeting Session (Cập nhật phiên họp) — KHÔNG CÒN DÙNG

> Gộp vào UC-16 Update Meeting. Sau Published chỉ được dời giờ / địa điểm / online link (UC-16 AF-1 Reschedule). Mã thông báo `MSG49`, `MSG55` ngừng dùng.

### UC-28: Cancel Meeting Session (Hủy phiên họp) — KHÔNG CÒN DÙNG

> Bỏ, không có UC thay thế. PO chưa chốt có thêm UC Cancel Meeting hay không (nếu có sẽ lấy mã mới từ UC-95). Mã thông báo `MSG50`, `MSG56` ngừng dùng.

### UC-29: Start Meeting Session (Bắt đầu phiên họp) — KHÔNG CÒN DÙNG

> Trùng UC-43 Start Meeting. Mã thông báo `MSG51`, `MSG57`, `MSG58` ngừng dùng.

### UC-30: End Meeting Session (Kết thúc phiên họp) — KHÔNG CÒN DÙNG

> Trùng UC-44 End Meeting. Mã thông báo `MSG52`, `MSG59` ngừng dùng.
```

### B-09 · Dòng 1667–1681 · UC-54 View Meeting Agenda · THAY khối
- **THAY** dòng 1667–1681 bằng (đổi heading `####` → `###` cho đồng nhất với các UC khác):

```markdown
### UC-54: View Meeting Agenda (Xem chương trình nghị sự cuộc họp)

- **Use Case ID**: UC-54
- **Use Case Name**: View Meeting Agenda
- **Primary Actor**: Secretary, Chair, Meeting Organizer, Meeting Approver, authorized Meeting Participant
- **Description**: Allows an authorized user to view the Agenda Items of a meeting, including agenda order and execution status where available.
- **Related Diagram source**: [UC54_ViewMeetingAgenda.txt](../06-sequencediagram/agenda-management/UC54_ViewMeetingAgenda.txt)
- **Preconditions**: The user is authenticated and currently authorized to view the selected meeting. (BR-29)
- **Postconditions**: The authorized agenda is displayed; no agenda or lifecycle data is changed.
- **Main Flow**:
  1. The user opens an authorized meeting and selects its Agenda.
  2. The system verifies access to the meeting and retrieves its Agenda Items in their defined order. (BR-29, BR-64)
  3. The system displays the agenda and current execution status where available.
- **Alternative Flow — No Agenda Items**: If the meeting has no Agenda Items, display an empty agenda. The use case ends successfully.
- **Exception Flows**: If the meeting is not accessible, return the same not-found result as for an unknown meeting and disclose no agenda data. (BR-29) If the agenda cannot be loaded, display MSG128.
```

### B-10 · Dòng 1683–1741 · UC-31 Create Agenda Item
- 1689: `... to add a new Agenda Item to a planned Meeting Session, specifying ...` → **THAY** cụm `to a planned Meeting Session` bằng `to a meeting whose agenda is still open for planning`.
- 1692: `  - PRE-2: Selected Meeting Session exists and belongs to the selected meeting.` → **THAY** bằng `  - PRE-2: The selected meeting is in Draft, Rejected, Approved, or Published status, so its agenda is open for planning. (BR-74)`
- 1693: `  - PRE-3: Selected Meeting Session is Planned.` → **XOÁ**.
- 1695: `  - POST-1: The new Agenda Item belongs to the selected Meeting Session.` → **THAY** bằng `  - POST-1: The new Agenda Item belongs to the selected meeting.`
- 1697: `  - POST-3: The new Agenda Item appears at the end of that Meeting Session agenda.` → **THAY** bằng `  - POST-3: The new Agenda Item appears at the end of the meeting agenda.`
- 1703: `1. The Secretary opens the agenda of a selected Meeting Session.` → **THAY** bằng `1. The Secretary opens the agenda of the selected meeting.`
- 1714: `7. The system creates the Agenda Item in Pending status, associates it with the selected Meeting Session, and places it at the end of the session agenda. (BR-64, BR-67)` → **THAY** bằng `7. The system creates the Agenda Item in Pending status, associates it with the selected meeting, and places it at the end of the meeting agenda. (BR-64, BR-67)`
- 1732: `  1. At Step 6, the Meeting Session is no longer Planned or the parent meeting no longer permits agenda planning. (BR-65, BR-68, BR-74)` → **THAY** bằng `  1. At Step 6, the meeting no longer permits agenda planning (it is WaitingApproval, InProgress, or Ended). (BR-65, BR-68, BR-74)`
- 1734: `MSG67 – "The agenda for this meeting session can no longer be modified."` → **THAY** chuỗi trong ngoặc kép bằng `"The agenda for this meeting can no longer be modified."`

### B-11 · Dòng 1745–1795 · UC-32 Update Agenda Item
- 1751: `... of an existing Agenda Item in a planned Meeting Session.` → **THAY** cụm `in a planned Meeting Session` bằng `of a meeting whose agenda is still open for planning`.
- 1754: `  - PRE-2: Selected Meeting Session belongs to the selected meeting and is Planned.` → **THAY** bằng `  - PRE-2: The selected meeting is in Draft, Rejected, Approved, or Published status. (BR-74)`
- 1755: `  - PRE-3: Selected Agenda Item exists, belongs to the Meeting Session, and is Pending.` → **THAY** bằng `  - PRE-3: Selected Agenda Item exists, belongs to the selected meeting, and is Pending.`
- 1762: `1. The Secretary opens the agenda of a selected Meeting Session.` → **THAY** bằng `1. The Secretary opens the agenda of the selected meeting.`
- 1786: `  1. At Step 6, the Meeting Session is no longer Planned or the parent meeting no longer permits agenda planning. (BR-65, BR-68, BR-74)` → **THAY** bằng `  1. At Step 6, the meeting no longer permits agenda planning (it is WaitingApproval, InProgress, or Ended). (BR-65, BR-68, BR-74)`
- 1788: MSG67 text → như B-10 (`"The agenda for this meeting can no longer be modified."`).

### B-12 · Dòng 1799–1840 · UC-33 Delete Agenda Item
- 1805: `... to remove an Agenda Item from a planned Meeting Session.` → **THAY** cụm `from a planned Meeting Session` bằng `from the agenda of a meeting whose agenda is still open for planning`.
- 1808: `  - PRE-2: Selected Meeting Session belongs to the selected meeting and is Planned.` → **THAY** bằng `  - PRE-2: The selected meeting is in Draft, Rejected, Approved, or Published status. (BR-74)`
- 1809: `  - PRE-3: Selected Agenda Item exists, belongs to the Meeting Session, and is Pending.` → **THAY** bằng `  - PRE-3: Selected Agenda Item exists, belongs to the selected meeting, and is Pending.`
- 1811: `  - POST-1: The Agenda Item is removed from the Meeting Session agenda.` → **THAY** bằng `  - POST-1: The Agenda Item is removed from the meeting agenda.`
- 1812: `  - POST-2: Remaining Agenda Items in the session retain contiguous explicit ordering.` → **THAY** bằng `  - POST-2: Remaining Agenda Items of the meeting retain contiguous explicit ordering.`
- 1817: `1. The Secretary opens the agenda of a selected Meeting Session.` → **THAY** bằng `1. The Secretary opens the agenda of the selected meeting.`
- 1821: `5. The system verifies that the Secretary is assigned to the meeting, the Meeting Session is Planned, and the Agenda Item is Pending. (BR-65, BR-68, BR-74)` → **THAY** bằng `5. The system verifies that the Secretary is assigned to the meeting, the meeting agenda is still open for planning, and the Agenda Item is Pending. (BR-65, BR-68, BR-74)`
- 1831: `  1. At Step 5, the Meeting Session is no longer Planned or the parent meeting no longer permits agenda modification. (BR-65, BR-68, BR-74)` → **THAY** bằng `  1. At Step 5, the meeting no longer permits agenda modification (it is WaitingApproval, InProgress, or Ended). (BR-65, BR-68, BR-74)`
- 1833: MSG67 text → như B-10.

### B-13 · Dòng 1844–1883 · UC-34 Reorder Agenda Items
- 1850: `... to reorder Agenda Items within a planned Meeting Session.` → **THAY** cụm `within a planned Meeting Session` bằng `within a meeting whose agenda is still open for planning`.
- 1853: `  - PRE-2: Selected Meeting Session belongs to the selected meeting and is Planned.` → **THAY** bằng `  - PRE-2: The selected meeting is in Draft, Rejected, Approved, or Published status. (BR-74)`
- 1854: `  - PRE-3: Selected Meeting Session contains two or more Agenda Items.` → **THAY** bằng `  - PRE-3: The selected meeting contains two or more Agenda Items.`
- 1856: `  - POST-1: The new explicit order of Agenda Items is saved within the Meeting Session.` → **THAY** bằng `  - POST-1: The new explicit order of Agenda Items is saved within the meeting.`
- 1861: `1. The Secretary opens the agenda of a selected Meeting Session.` → **THAY** bằng `1. The Secretary opens the agenda of the selected meeting.`
- 1864: `4. The system validates that all reordered items belong to the same Meeting Session, the Secretary is assigned to the meeting, and the Meeting Session is Planned. (BR-64, BR-65, BR-67, BR-74)` → **THAY** bằng `4. The system validates that all reordered items belong to the selected meeting, the Secretary is assigned to the meeting, and the meeting agenda is still open for planning. (BR-64, BR-65, BR-67, BR-74)`
- 1874: `  1. At Step 4, the Meeting Session is no longer Planned or the parent meeting no longer permits agenda modification. (BR-65, BR-68, BR-74)` → **THAY** bằng `  1. At Step 4, the meeting no longer permits agenda modification (it is WaitingApproval, InProgress, or Ended). (BR-65, BR-68, BR-74)`
- 1876: MSG67 text → như B-10.

### B-14 · Dòng 1887–1944 · UC-35 Set Current Agenda Item
- 1893: `... as the active/current item of an in-progress Meeting Session, recording its actual start time.` → **THAY** cụm `of an in-progress Meeting Session` bằng `of an InProgress meeting`.
- 1896: `  - PRE-2: Selected Meeting Session is InProgress.` → **THAY** bằng `  - PRE-2: The selected meeting is InProgress.`
- 1897: `  - PRE-3: Selected Agenda Item belongs to the Meeting Session and is Pending.` → **THAY** bằng `  - PRE-3: Selected Agenda Item belongs to the selected meeting and is Pending.`
- 1899: `  - POST-1: The selected Agenda Item is InProgress and becomes the current Agenda Item of the Meeting Session.` → **THAY** bằng `  - POST-1: The selected Agenda Item is InProgress and becomes the current Agenda Item of the meeting.`
- 1905: `1. The Chair opens the in-progress Meeting Session agenda.` → **THAY** bằng `1. The Chair opens the agenda of the InProgress meeting.`
- 1909: `   - the Meeting Session is InProgress;` → **THAY** bằng `   - the meeting is InProgress;`
- 1910: `   - the selected Agenda Item belongs to that Meeting Session;` → **THAY** bằng `   - the selected Agenda Item belongs to that meeting;`
- 1912: `   - no other Agenda Item of the same Meeting Session is InProgress.` → **THAY** bằng `   - no other Agenda Item of the same meeting is InProgress.`
- 1915: `... to authorized users viewing the same Meeting Session. (BR-73)` → **THAY** cụm `viewing the same Meeting Session` bằng `viewing the same meeting`.
- 1924: `  1. At Step 3, another Agenda Item of the same Meeting Session is already InProgress. (BR-71)` → **THAY** bằng `  1. At Step 3, another Agenda Item of the same meeting is already InProgress. (BR-71)`
- 1931: `     - the selected Agenda Item does not belong to the Meeting Session; or` → **THAY** bằng `     - the selected Agenda Item does not belong to the meeting; or`
- 1932: `     - the Meeting Session is no longer InProgress; or` → **THAY** bằng `     - the meeting is no longer InProgress; or`

### B-15 · Dòng 1948–1999 · UC-36 Complete Agenda Item
- 1957: `  - PRE-2: Selected Meeting Session is InProgress.` → **THAY** bằng `  - PRE-2: The selected meeting is InProgress.`
- 1958: `  - PRE-3: Selected Agenda Item belongs to the Meeting Session and is InProgress.` → **THAY** bằng `  - PRE-3: Selected Agenda Item belongs to the selected meeting and is InProgress.`
- 1962: `  - POST-3: The Meeting Session has no current Agenda Item until the Chair starts another Agenda Item.` → **THAY** bằng `  - POST-3: The meeting has no current Agenda Item until the Chair starts another Agenda Item.`
- 1967: `1. The Chair views the current in-progress Agenda Item in the active Meeting Session.` → **THAY** bằng `1. The Chair views the current InProgress Agenda Item of the InProgress meeting.`
- 1971: `   - the Meeting Session is InProgress; and` → **THAY** bằng `   - the meeting is InProgress; and`
- 1972: `   - the selected Agenda Item belongs to that Meeting Session and is currently InProgress.` → **THAY** bằng `   - the selected Agenda Item belongs to that meeting and is currently InProgress.`
- 1976: `... to authorized users viewing the same Meeting Session. (BR-73)` → **THAY** cụm `viewing the same Meeting Session` bằng `viewing the same meeting`.
- 1986: `     - the selected Agenda Item does not belong to the Meeting Session; or` → **THAY** bằng `     - the selected Agenda Item does not belong to the meeting; or`
- 1987: `     - the Meeting Session is no longer InProgress; or` → **THAY** bằng `     - the meeting is no longer InProgress; or`

### B-16 · Dòng 2008–2036 · "Vòng đời quản lý thành phần tham dự đã chốt" (FE-07)
- 2018: `| `Approved` | Secretary hoàn thiện danh sách; Organizer công bố theo UC-21 khi đủ điều kiện. Meeting Sessions là tùy chọn. | ...` → **XOÁ** câu ` Meeting Sessions là tùy chọn.` (phần còn lại của dòng giữ nguyên).
- 2030–2031: `- Chỉ Organizer có quyền công bố được chuyển meeting **`Approved → Published`**. Meeting Session` / `  không bắt buộc. Tại thời điểm công bố, ...` → **XOÁ** cụm ` Meeting Session không bắt buộc.` (nối 2 dòng: `...**`Approved → Published`**. Tại thời điểm công bố, meeting phải có ...`).
- 2021: `| `Ended`, `MinutesIssued`, `Archived` | ...` → **GIỮ** nhưng xem câu hỏi mở Q7 (vòng đời PO chốt kết thúc ở `Ended`).

### B-17 · Dòng 2442–2450 · UC-43 Start Meeting · THAY khối
- **THAY** dòng 2445–2450 bằng:

```markdown
- **Description**: Allows an active Chair assigned to a Published meeting to start the meeting.
- **Preconditions**: The Chair is authenticated and currently assigned to the meeting; the meeting is Published; exactly one current Chair and at least one current Secretary have active accounts. (BR-87, BR-127, BR-129, BR-130, BR-131)
- **Postconditions**: The meeting changes to InProgress and its lifecycle history is recorded.
- **Related Diagram source**: [UC43_StartMeeting.txt](../06-sequencediagram/meeting-management/UC43_StartMeeting.txt)
- **Main Flow**: The Chair selects Start Meeting; the system revalidates Chair assignment, active Chair and Secretary, and Published status; it changes the meeting to InProgress, records the transition, and displays MSG110. (BR-38, BR-127, BR-130, BR-131)
- **Exception Flows**: If the meeting is not Published or role readiness fails, display MSG112. If starting fails, display MSG115.
```
  (Đã bỏ: "Meeting Sessions are optional and keep their own lifecycle", "Meeting Sessions are optional", "Meeting Session statuses are unchanged", BR-56, BR-133, BR-135.)

### B-18 · Dòng 2452–2460 · UC-44 End Meeting · THAY khối
- **THAY** dòng 2455–2460 bằng:

```markdown
- **Description**: Allows an active Chair assigned to an InProgress meeting to end it.
- **Preconditions**: The Chair is authenticated and currently assigned to the meeting; the meeting is InProgress.
- **Postconditions**: The meeting changes to Ended and its lifecycle history is recorded; any associated room closes and connected users are disconnected.
- **Related Diagram source**: [UC44_EndMeeting.txt](../06-sequencediagram/meeting-management/UC44_EndMeeting.txt)
- **Main Flow**: The Chair confirms End Meeting; the system revalidates Chair assignment, active Chair, and InProgress status; it changes the meeting to Ended, records the transition, closes the room, and displays MSG111. (BR-38, BR-130, BR-132, BR-143)
- **Exception Flows**: If meeting/role state is no longer eligible, display MSG114. If ending fails, display MSG116.
```
  (Đã bỏ: "after all planned sessions have finished or been cancelled", kiểm tra "no Meeting Session remains Planned or InProgress", MSG113, BR-61, BR-134, BR-135.)

### B-19 · Dòng 2462–2486 · UC-45 Join Meeting Room
- 2466: `... The room belongs to the Meeting, not to a Meeting Session, so a Meeting Session is not required for room access.` → **THAY** cả dòng bằng `- **Description**: Allows an authorized user to enter the online room associated with a meeting. The room belongs to the Meeting. (BR-137)`
- 2471: `  - PRE-4: No Meeting Session is required. (BR-137, BR-138)` → **XOÁ**.
- 2475: `  - POST-3: The Meeting and all Meeting Session lifecycle statuses remain unchanged.` → **THAY** bằng `  - POST-3: The Meeting lifecycle status remains unchanged.`
- 2478: `... (BR-137, BR-138, BR-139, BR-140)` → **THAY** cụm BR thành `(BR-137, BR-139, BR-140)`.
- 2481: `  5. The Meeting and Meeting Session lifecycles remain unchanged. (BR-141)` → **THAY** bằng `  5. The Meeting lifecycle remains unchanged. (BR-141)`
- 2486: `... leave Meeting and Meeting Session lifecycles unchanged, ...` → **THAY** cụm `leave Meeting and Meeting Session lifecycles unchanged` bằng `leave the Meeting lifecycle unchanged`.
- 2468, 2477, 2485: *guest session* → **GIỮ**.

### B-20 · Dòng 2488–2621 · UC-46 → UC-52 (cùng một mẫu sửa)
Thay chính xác các cụm sau (phần còn lại của dòng giữ nguyên):

| Dòng | Hiện tại (trích) | Thay bằng |
|---|---|---|
| 2492 | `without ending the Meeting or any Meeting Session.` | `without ending the Meeting.` |
| 2497 | `POST-3: The Meeting and Meeting Session lifecycle statuses remain unchanged.` | `POST-3: The Meeting lifecycle status remains unchanged.` |
| 2502 | `4. The Meeting and Meeting Session lifecycles remain unchanged. (BR-141)` | `4. The Meeting lifecycle remains unchanged. (BR-141)` |
| 2514 | `POST-2: No Meeting or Meeting Session lifecycle state is changed.` | `POST-2: No Meeting lifecycle state is changed.` |
| 2531 | `POST-2: No Meeting or Meeting Session lifecycle state is changed.` | `POST-2: No Meeting lifecycle state is changed.` |
| 2549 | `POST-3: No Meeting or Meeting Session lifecycle state is changed.` | `POST-3: No Meeting lifecycle state is changed.` |
| 2557 | `7. Meeting and Meeting Session lifecycles remain unchanged. (BR-141)` | `7. The Meeting lifecycle remains unchanged. (BR-141)` |
| 2571 | `POST-2: The Meeting participant roster and Meeting Session data are not modified.` | `POST-2: The Meeting participant roster is not modified.` |
| 2593 | `POST-3: The Meeting and Meeting Session lifecycles remain unchanged.` | `POST-3: The Meeting lifecycle remains unchanged.` |
| 2600 | `6. The Meeting and Meeting Session lifecycles remain unchanged. (BR-141)` | `6. The Meeting lifecycle remains unchanged. (BR-141)` |
| 2615 | `POST-2: The Meeting and Meeting Session lifecycles remain unchanged.` | `POST-2: The Meeting lifecycle remains unchanged.` |
| 2621 | `5. The Meeting and Meeting Session lifecycles remain unchanged. (BR-141)` | `5. The Meeting lifecycle remains unchanged. (BR-141)` |

### B-21 · Dòng 2628–2662 · Mục "3. Danh mục Business Rules dẫn chiếu"
- 2639 hiện tại: `6. **Phòng họp trực tuyến**: Online Meeting Room thuộc về Meeting, không thuộc Meeting Session (BR-137); không cần session để vào room (BR-138). Room chỉ khả dụng ...`
  → **THAY** đoạn đầu `Online Meeting Room thuộc về Meeting, không thuộc Meeting Session (BR-137); không cần session để vào room (BR-138).` bằng `Online Meeting Room thuộc về Meeting (BR-137).` Phần sau giữ nguyên.
- **2641–2653: XOÁ toàn bộ** (từ dòng `C-43, UC-44 |` đến dòng `| `BR-103` | Revoking a Guest invitation ...`). Đây là mảnh bảng BR cũ bị dán hỏng: bắt đầu giữa dòng, không có header, và dùng lại mã `BR-93` → `BR-103` với nội dung **khác** danh mục chuẩn [06-business-rules.md](06-business-rules.md) (ví dụ `BR-95` ở đây nói "same room… across multiple sessions"). Lỗi có sẵn, nhưng 2642, 2644, 2645 nhắc Meeting Session nên phải xử lý.
- 2661 hiện tại: `6. **Phòng họp trực tuyến**: room thuộc Meeting, không thuộc Meeting Session; không cần session để mở hoặc vào. Room chỉ available ...`
  → **THAY** đoạn `room thuộc Meeting, không thuộc Meeting Session; không cần session để mở hoặc vào.` bằng `room thuộc Meeting.` Phần sau giữ nguyên.

### B-22 · Các dòng đã rà và GIỮ (khái niệm khác)
- 128 `PHIÊN ĐĂNG NHẬP`, 181 `server-side session`, 415, 452–453, 479, 486–489, 536, 746, 813: phiên đăng nhập — GIỮ.
- 2357, 2389, 2468, 2477, 2485: *guest session* — GIỮ.
- UC-37 → UC-42 (2040–2438): không nhắc Meeting Session — GIỮ.

---

## C. `docs/01-requirements/06-business-rules.md` (254 dòng)

Cách đánh dấu dùng chung (mechanical):

- **RETIRE** BR: cột *Rule Statement* đổi thành
  `**Không còn dùng (09/10/2026).** <lý do>. Nội dung cũ: ~~<nguyên văn cũ>~~`;
  cột *Applies To* đổi thành `—`; cột *Rationale* đổi thành `PO bỏ Meeting Session (09/10/2026)`.
  Giữ nguyên dòng và mã, không tái cấp.
- **REWRITE** BR: cột *Rule Statement* đổi thành `**[Sửa 09/10/2026]** <văn bản mới>`;
  cột *Rationale* đổi thành `PO bỏ Meeting Session (09/10/2026)`; *Applies To* theo chỉ dẫn.
- **APPLIES** = chỉ sửa cột *Applies To*.

### C-01 · Dòng 3 · lời dẫn đầu file · THÊM câu
- **THÊM** vào cuối đoạn dòng 3:
  ` Từ 09/10/2026 một số BR về Meeting Session được đánh dấu **Không còn dùng** hoặc **[Sửa 09/10/2026]** theo quyết định PO bỏ Meeting Session; các BR này không còn là nguyên văn Report 3 cho tới khi Report 3 được cập nhật.`

### C-02 · Section 1 (BR-01 → BR-23)
- **BR-18** (dòng 36) "all active authenticated sessions" — phiên đăng nhập → **GIỮ**.
- Ghi chú UC-08 dòng 53 "mọi phiên đăng nhập" → **GIỮ**.

### C-03 · Section 2 (BR-24 → BR-44), dòng 61–92

| BR | Dòng | Hành động | Văn bản mới / Applies To mới |
|---|---|---|---|
| BR-24 | 65 | GIỮ | — |
| BR-25 | 66 | **REWRITE** | `Planned Start Time and Planned End Time are meeting-level schedule fields. They are required when a meeting is created and are displayed with the meeting.` · Applies To: `UC-15, UC-16, UC-17, UC-18` |
| BR-26 | 67 | GIỮ | — |
| BR-27 | 68 | **REWRITE** | `Meeting information may be edited only while the meeting is in Draft or Rejected status. While the meeting is Published, the Meeting Organizer may only reschedule it by changing Planned Start Time, Planned End Time, Location, or Online Link; no other meeting information may change.` · Applies To: `UC-16` |
| BR-28 | 69 | GIỮ | — |
| BR-29 | 70 | **APPLIES** | Applies To: `UC-17, UC-25, UC-54` (bỏ `UC-53`) |
| BR-30 | 71 | **REWRITE** | `The meeting list may be searched by Meeting Name and filtered by Meeting Status, Confidentiality Level, and Time Range. A meeting matches a Time Range when its planned schedule (Planned Start Time to Planned End Time) overlaps the selected range.` · Applies To: `UC-18` |
| BR-31, BR-32 | 72–73 | GIỮ | — |
| BR-33 | 74 | **REWRITE** | `A meeting may be submitted or resubmitted for approval only when all mandatory meeting information, including the planned schedule, is complete and the meeting is in Draft or Rejected status.` · Applies To: `UC-19` |
| BR-34 → BR-43 | 75–84 | GIỮ | — |
| BR-44 | 85 | GIỮ (xem câu hỏi Q5: có áp dụng cho Reschedule UC-16 không) | — |

- Ghi chú "Áp dụng trong TrustCabinet" dòng 87–92 → **GIỮ**.

### C-04 · Section 3 (BR-45 → BR-53)
- **BR-45** (dòng 100) "approaching its start time" → **GIỮ** (start time nay nằm trên Meeting, đúng hơn trước).
- Các BR khác → GIỮ.

### C-05 · Dòng 112 · heading Section 4
- Hiện tại: `## 4. BR-54–BR-63 — Phiên họp và vòng đời cuộc họp`
- **THAY** bằng: `## 4. BR-54–BR-63 — Phiên họp (đa số không còn dùng từ 09/10/2026) và lịch cuộc họp`

### C-06 · Section 4 (BR-54 → BR-63), dòng 116–125

| BR | Dòng | Hành động | Lý do (điền vào `<lý do>`) / văn bản mới |
|---|---|---|---|
| BR-54 | 116 | **RETIRE** | `Không còn Meeting Session; lịch nhập trong UC-15 và sửa/dời trong UC-16 (BR-27)` |
| BR-55 | 117 | **RETIRE** | `Không còn Meeting Session; điều kiện gửi duyệt xem BR-33` |
| BR-56 | 118 | **RETIRE** | `Không còn Meeting Session; điều kiện công bố xem BR-37, BR-124, BR-125` |
| BR-57 | 119 | **RETIRE** | `Không còn Meeting Session` |
| BR-58 | 120 | **RETIRE** | `Không còn vòng đời Meeting Session; vòng đời cuộc họp xem BR-130 → BR-132` |
| BR-59 | 121 | **RETIRE** | `Không còn Meeting Session; một cuộc họp là một buổi họp` |
| BR-60 | 122 | **RETIRE** | `Không còn Meeting Session` |
| BR-61 | 123 | **RETIRE** | `Không còn Meeting Session; kết thúc cuộc họp là UC-44, xem BR-130, BR-132` |
| BR-62 | 124 | **REWRITE** | `A meeting's Planned End Time must be later than its Planned Start Time.` · Applies To: `UC-15, UC-16` |
| BR-63 | 125 | **RETIRE** | `Không còn Meeting Session; Chair bắt đầu/kết thúc cuộc họp theo BR-130` |

### C-07 · Section 5 (BR-64 → BR-77), dòng 129–146
- Heading dòng 129 `## 5. BR-64–BR-77 — Chương trình nghị sự` → **GIỮ**.

| BR | Dòng | Hành động | Văn bản mới / Applies To mới |
|---|---|---|---|
| BR-64 | 133 | **REWRITE** | `Each Agenda Item belongs to exactly one Meeting. Reordering changes the order only within that Meeting.` · Applies To: GIỮ `UC-31, UC-34, UC-35, UC-36, UC-54` |
| BR-65 | 134 | **REWRITE** | `Only the Secretary assigned to the meeting may create, update, delete, or reorder Agenda Items, and only while agenda planning is open for that meeting (BR-74).` · Applies To: GIỮ |
| BR-66 | 135 | GIỮ | ("parent meeting" vẫn đúng nghĩa) |
| BR-67 | 136 | **REWRITE** | `A newly created Agenda Item starts in Pending status and is assigned an explicit order within its Meeting. The Secretary may change that order while agenda planning is open (BR-74).` · Applies To: GIỮ |
| BR-68 | 137 | **REWRITE** | `After the Meeting becomes InProgress, its agenda structure cannot be created, updated, deleted, or reordered. Only agenda execution state may change while the Meeting is InProgress.` · Applies To: GIỮ |
| BR-69 | 138 | **REWRITE** | `Only the Chair assigned to the meeting may set or complete the current Agenda Item, and only while the Meeting is InProgress.` · Applies To: GIỮ |
| BR-70 | 139 | GIỮ | — |
| BR-71 | 140 | **REWRITE** | `Only one Agenda Item of the same Meeting may be InProgress at a time. The current Agenda Item must be completed before another Agenda Item can be set as current.` · Applies To: GIỮ |
| BR-72 | 141 | **REWRITE** | `Setting an Agenda Item as current records its actual start time. Completing it records its actual end time and actual time used. Completing an Agenda Item does not automatically end the Meeting.` · Applies To: GIỮ |
| BR-73 | 142 | **REWRITE** | `Changes to the current or completed Agenda Item must be reflected in real time for authorized users viewing the same Meeting.` · Applies To: GIỮ |
| BR-74 | 143 | **REWRITE** | `Agenda planning is open while the Meeting is Draft, Rejected, Approved, or Published. It is closed while the Meeting is WaitingApproval and ends when the Meeting becomes InProgress.` · Applies To: GIỮ (xem câu hỏi Q3) |
| BR-75 | 144 | **REWRITE** | `When a Published meeting is rescheduled, the new Planned Start Time must be later than the current time. This prevents retroactive rescheduling after publication.` · Applies To: `UC-16` |
| BR-76 | 145 | **REWRITE** | `Rescheduling a Published meeting (time, location, or online link) must notify all current invitees, internal participants and external Guests, of the new schedule. A notification delivery failure does not undo the reschedule.` · Applies To: `UC-16` |
| BR-77 | 146 | **RETIRE** | `Không còn Meeting Session; huỷ cuộc họp chưa được PO chốt` |

### C-08 · Section 6 (BR-78 → BR-129), dòng 150–205

| BR | Dòng | Hành động | Ghi chú |
|---|---|---|---|
| BR-87 | 163 | **APPLIES** | Applies To: `UC-21, UC-43` (bỏ `UC-29`) |
| BR-127 | 203 | GIỮ | — |
| BR-128 | 204 | **RETIRE** | Lý do: `Không còn Meeting Session; điều kiện bắt đầu cuộc họp xem BR-127` |
| BR-129 | 205 | **APPLIES** | Applies To: `UC-21, UC-43` (bỏ `UC-29`) |
| Các BR khác | — | GIỮ | Không nhắc Session |

### C-09 · Section 7 (BR-130 → BR-136), dòng 209–219

| BR | Dòng | Hành động | Lý do |
|---|---|---|---|
| BR-130, BR-131, BR-132 | 213–215 | GIỮ | — |
| BR-133 | 216 | **RETIRE** | `Không còn Meeting Session` |
| BR-134 | 217 | **RETIRE** | `Không còn Meeting Session; UC-44 không còn kiểm tra session chưa kết thúc` |
| BR-135 | 218 | **RETIRE** | `Không còn Meeting Session` |
| BR-136 | 219 | **RETIRE** | `Không còn Meeting Session` |

### C-10 · Section 8 (BR-137 → BR-147), dòng 223–237

| BR | Dòng | Hành động | Văn bản mới / lý do |
|---|---|---|---|
| BR-137 | 227 | **REWRITE** | `The Online Meeting Room belongs to the Meeting.` · Applies To: GIỮ |
| BR-138 | 228 | **RETIRE** | `Không còn Meeting Session` |
| BR-139, BR-140 | 229–230 | GIỮ | — |
| BR-141 | 231 | **REWRITE** | `Joining, leaving, microphone/camera control, screen sharing, room moderation, and in-room chat do not change the Meeting lifecycle.` · Applies To: GIỮ |
| BR-142 | 232 | **RETIRE** | `Không còn Meeting Session; một cuộc họp có một buổi họp` |
| BR-143 → BR-147 | 233–237 | GIỮ | — |

### C-11 · Section 9 (BR-148 → BR-157)
- Không nhắc Session/Vote → **GIỮ** toàn bộ.

### C-12 · Quorum / Resolution / time-boxed / deadline vote
- **Không có BR nào** trong file nhắc quorum, resolution, time-boxed hay deadline vote → không có gì để xoá cho D2.
- *Tùy chọn (không bắt buộc, chỉ khi đặc tả FE-12):* BR mới cho biểu quyết phải lấy mã tiếp theo
  **từ BR-158**, ví dụ: (a) vote chỉ được tạo/mở/bỏ phiếu/đóng khi Meeting `InProgress`;
  (b) không có quorum, không có deadline; (c) kết luận chỉ là một trong Passed / Not Passed / Deferred,
  do Chair ghi sau khi vote đóng. Cần PO/BA duyệt trước khi thêm.

---

## D. `docs/01-requirements/README.md` (135 dòng)

### D-01 · Dòng 13
- Hiện tại: `... Master Use Case Inventory (FE-01 → FE-16, UC-01 → UC-94) & Use Case Diagram Baseline ...`
- **THAY** cụm `(FE-01 → FE-16, UC-01 → UC-94)` bằng `(FE-01 → FE-16, UC-01 → UC-94; FE-05 và UC-26 → UC-30, UC-53 không còn dùng từ 09/10/2026)`.

### D-02 · Dòng 14
- Hiện tại: `... Đặc tả Use Case chi tiết UC-01 → UC-54; dẫn chiếu ...`
- **THAY** cụm `UC-01 → UC-54;` bằng `UC-01 → UC-54 (UC-26 → UC-30 và UC-53 không còn dùng);`.

### D-03 · Dòng 15
- Hiện tại: `- [06-business-rules.md](06-business-rules.md) — Danh mục tập trung BR-01 → BR-157; UC Specification ...`
- **THAY** cụm `BR-01 → BR-157;` bằng `BR-01 → BR-157 (một số BR về Meeting Session không còn dùng từ 09/10/2026);`.

### D-04 · Dòng 69 · ví dụ Exception Flow
- Hiện tại: `(ví dụ: Cuộc họp đã bị hủy, không có quyền thực hiện, phiên họp đã kết thúc)`
- **THAY** bằng: `(ví dụ: Cuộc họp đã kết thúc, không có quyền thực hiện, biểu quyết đã đóng)`
  ("Cuộc họp đã bị hủy" bỏ vì chưa có UC hủy cuộc họp — xem Q1.)

### D-05 · Dòng 29 "không chép lại một phiên bản BR riêng" → **GIỮ** ("phiên bản" = version).

---

## E. Mã thông báo (MSGnn) bị ảnh hưởng

Không sửa `docs/03-api/00-danh-muc-ma-thong-bao.md` trong lượt này (ngoài phạm vi), nhưng commit sửa
UC Specification phải cập nhật catalogue **cùng commit** (quy tắc AGENTS.md). Tất cả mã dưới đây đang
ở trạng thái *Dành sẵn* (chưa có code BE/FE dùng — đã grep `backend/`, `frontend/src`).

| Mã | UC cũ | Tình huống | Hành động |
|---|---|---|---|
| `MSG48` | UC-26 | Meeting session created successfully. | **Ngừng dùng** |
| `MSG49` | UC-27 | Meeting session updated successfully. | **Ngừng dùng** |
| `MSG50` | UC-28 | Meeting session cancelled successfully. | **Ngừng dùng** |
| `MSG51` | UC-29 | Meeting session started successfully. | **Ngừng dùng** |
| `MSG52` | UC-30 | Meeting session ended successfully. | **Ngừng dùng** |
| `MSG53` | UC-26/27 | Invalid meeting session information. | **Ngừng dùng** (lỗi lịch nay dùng `MSG31` + field message) |
| `MSG54` | UC-26 | Meeting sessions can no longer be added to this meeting. | **Ngừng dùng** |
| `MSG55` | UC-27 | This meeting session can no longer be edited. | **Ngừng dùng** (dùng `MSG33`) |
| `MSG56` | UC-28 | This meeting session can no longer be cancelled. | **Ngừng dùng** |
| `MSG57` | UC-29 | Another meeting session is already in progress… | **Ngừng dùng** |
| `MSG58` | UC-29 | This meeting session cannot be started. | **Ngừng dùng** |
| `MSG59` | UC-30 | This meeting session cannot be ended. | **Ngừng dùng** |
| `MSG113` | UC-44 | Complete or cancel all unfinished meeting sessions before ending the meeting. | **Ngừng dùng** |
| `MSG127` | UC-53 | Unable to load meeting sessions. Please try again. | **Ngừng dùng** |
| `MSG67` | UC-31..34 | The agenda for this meeting **session** can no longer be modified. | **Giữ mã, sửa câu chữ** → *The agenda for this meeting can no longer be modified.* (catalogue cho phép đổi câu chữ) |
| `MSG128` | UC-54 | Unable to load the meeting agenda. | **Giữ** (đổi mô tả use case không cần) |
| `MSG31` | UC-15/16 | Invalid meeting information. | **Giữ**; thêm field message "Planned start time is required.", "Planned end time must be later than planned start time.", "Planned start time must be in the future." (Reschedule) |
| `MSG33` | UC-16 | This meeting can no longer be edited. | **Giữ**; nay cũng dùng cho AF-1 Reschedule khi meeting không còn Published |
| `MSG138` (mới) | UC-16 AF-1 | Meeting rescheduled successfully. | **Cấp mới** — `MSG138` là "Mã tiếp theo" hiện tại; kiểm tra lại lúc áp dụng, thêm dòng catalogue và đổi "Mã tiếp theo" thành `MSG139` cùng commit |

D2 (biểu quyết) và D3 (audio): **không có mã MSG nào** đang gán cho UC-62, UC-70 → UC-76, UC-78 →
không có mã ngừng dùng.

---

## F. Ngoài phạm vi nhưng phát hiện khi rà (để người điều phối chuyển tiếp)

- Sequence diagram `docs/06-sequencediagram/meeting-session-management/` (UC26–UC30, UC53) — retire;
  `UC16`, `UC43`, `UC44`, `UC54`, `UC31`–`UC36` phải vẽ lại (bỏ session, thêm AF Reschedule).
- `docs/03-api/00-danh-muc-ma-thong-bao.md` — mục E; dòng 30 câu "`MSG48`–`MSG77` … đã dành cho Meeting Session" cần ghi rõ phần nào ngừng dùng.
- Use Case Diagram theo actor `docs/assets/use-case-diagrams/` — bỏ UC-26..30, UC-53; UC-54 extend UC-17; UC-72/76 đổi tên.
- `frontend/src/pages/scopeData.ts` — README dòng 7 yêu cầu đồng bộ với tài liệu này.

---

## G. Câu hỏi mở cho PO / BA

- **Q1.** Có thêm UC **Cancel Meeting** (mã mới UC-95) hay không? Ảnh hưởng: tổng UC, FE-04, ví dụ README dòng 69.
- **Q2.** UC-76: phương án **A** (Record Vote Conclusion, Chair) hay **B** (bỏ, kết luận ghi trong UC-75)? Mặc định trong danh sách này là A.
- **Q3.** Thời điểm soạn agenda: danh sách này đặt BR-74 = mở ở Draft / Rejected / Approved / Published, khoá ở WaitingApproval, đóng từ InProgress (tương đương "session còn Planned" cũ + khóa như roster khi chờ duyệt). PO xác nhận? Đặc biệt: Secretary có được sửa agenda **sau Published** không, và có được thêm agenda item khi đang họp không?
- **Q4.** Ai được Reschedule: chỉ Meeting Organizer (đã viết) hay cả Chair? Reschedule có được ở `Approved` (trước công bố) không, hay ở `Approved` không sửa gì như hiện tại?
- **Q5.** Reschedule có ghi một bản ghi lịch sử (BR-44 "distinct lifecycle record") không? Hiện chưa thêm BR-44 vào Applies To của UC-16.
- **Q6.** "Online Link" là trường nhập tay (link ngoài) hay chính là phòng họp trực tuyến tích hợp (FE-09)? Đã viết `Location / Online Link` theo D1; nếu là phòng tích hợp thì bỏ "Online Link" khỏi UC-15/16/17 và BR-27/BR-76.
- **Q7.** Vòng đời PO chốt kết thúc ở `Ended`, nhưng spec FE-07 (dòng 2021, 2658) còn `MinutesIssued`, `Archived`. Giữ hay bỏ?
- **Q8.** UC-44 End Meeting khi còn agenda item `InProgress` hoặc còn vote đang mở: chặn, tự đóng, hay cho phép? Trước đây MSG113 chặn khi còn session; nay chưa có quy tắc thay thế.
- **Q9.** UC-73: "Participant đủ điều kiện" gồm những ai (Chair, Secretary, Member, delegate, Guest)?
- **Q10.** Tên FE-12 sau khi bỏ Resolution: đề xuất "Biểu quyết trong cuộc họp / Voting Management" — cần khớp Report 1.
- **Q11.** UC-15: có yêu cầu Planned Start Time phải ở tương lai khi tạo mới không (BR-75 hiện chỉ áp cho Reschedule)?
- **Q12.** BF-03 có bước "Set Meeting to InProgress & **Notify Participants**" — UC-43 hiện không gửi thông báo. Có thêm vào UC-43 không?


---

# PHẦN B — `docs/08-reports/` (Report 1, 2, 3 — sửa trên DOCX rồi sync)

## Audit `docs/08-reports/` theo quyết định PO ngày 09/10/2026 (D1, D2, D3)

Phạm vi: `Report-1_Project_Introduction.md`, `Report-2_Project_Management_Plan.md`,
`Report-3_Software_Requirement_Specification.md`, `README.md` trong `docs/08-reports/`.
Số dòng lấy theo nhánh `docs/bf-meeting-no-session`, commit `be458da`.

Quy ước trong tài liệu này:

- Hướng dẫn viết bằng tiếng Việt. Văn bản đưa vào report viết bằng tiếng Anh, nằm trong khối
  `text` và chép **nguyên văn**.
- "Hiện tại" là đoạn trích ngắn để tìm đúng chỗ. Trong DOCX, tìm bằng Ctrl+F theo đoạn trích đó.
  Số dòng chỉ dùng cho bản Markdown.
- Trong ô bảng, `<br>` là xuống dòng hoặc sang đoạn mới **trong cùng một ô** DOCX. Đừng gõ chữ
  `<br>` vào Word.
- Mỗi thay đổi có mã (`R1-xx`, `R2-xx`, `R3-xx`). Đánh dấu **[BẮT BUỘC]** là hệ quả trực tiếp
  của D1–D3. **[KHUYẾN NGHỊ]** không bắt buộc nhưng nên làm để report khỏi tự mâu thuẫn.
  **[CHỜ PO]** phải đợi câu trả lời ở mục 6.

---

## 0. Nguồn sự thật và cách áp dụng — ĐỌC TRƯỚC

1. **Markdown trong `docs/08-reports/` là bản sinh tự động từ DOCX, không phải nguồn.**
   - `scripts/sync_reports.py` đọc `docs/08-reports/source/Report-1.docx`, `Report-2.docx`,
     `Report-3.docx`, chuyển đoạn văn và bảng sang Markdown rồi **ghi đè** ba file `.md`. Nó cũng
     tự thêm dòng tiêu đề `# Report N – …` và dòng `> Nguồn: [Google Drive](…)`.
   - `docs/08-reports/README.md` ghi rõ: "Google Drive là nguồn gốc."
   - Bản gốc nằm trên Google Drive:
     - Report 1: <https://docs.google.com/document/d/1Lbw_A5CHPJzDa4kkLQFdjjn_c5KRIwOW/edit>
     - Report 2: <https://docs.google.com/document/d/1d4K3K3zcURku4SG84nXj7Zj6D-KiqGXa/edit>
     - Report 3: <https://docs.google.com/document/d/1rQuQxHWq4nJPJYMrrMlAZQ3P173PTXJz/edit>
   - Commit gần nhất đụng tới các file `.md` là `07eac2a` (06/10/2026, "đồng bộ toàn bộ báo cáo
     từ DOCX sang Markdown"). Không có dấu hiệu ai sửa tay sau lần đó.
2. **Cách làm đúng:**
   1. Sửa trong Google Docs theo danh sách bên dưới.
   2. Tải từng report về dạng `.docx`, đặt vào `docs/08-reports/source/` với tên `Report-1.docx`,
      `Report-2.docx`, `Report-3.docx`. Thư mục này chưa có trong repo, phải tự tạo.
   3. Từ thư mục gốc repo, chạy `python scripts/sync_reports.py`.
   4. Kiểm tra bằng `git diff -- docs/08-reports`. Diff phải khớp danh sách thay đổi ở đây.
   5. **Không sửa tay file `.md`.** Lần sync sau sẽ ghi đè mọi chỉnh sửa tay.
3. **Hình ảnh không được xuất sang Markdown.** Script chỉ lấy chữ, nên audit này không thấy được
   hình. Trong DOCX phải tự mở và kiểm tra các hình sau xem có vẽ "Meeting Session", "Resolution",
   phiếu biểu quyết trước/sau họp, "time-boxed" hay "quorum" không. Nếu có thì vẽ lại hoặc thay hình:
   - Report 3 §I.1 Context Diagram.
   - Report 3 §I.2 Main Business Flow (xem `R3-04`). Thay bằng các file
     `docs/09-bussinessflow/BF-0x_*.drawio.png`.
   - Report 3 §I.3.1 ERD: không được còn thực thể `MeetingSession` hay `Resolution`, và
     `AgendaItem` phải nối thẳng vào `Meeting`.
   - Report 3 §I.4.3 Use Case Diagrams: UC-26 → UC-30 và UC-53 không được xuất hiện. UC-54 phải là
     `<<extend>>` UC-17. UC-72 và UC-76 mang tên mới.
   - Report 3 §I.5.1.a Screen Flow: bỏ màn "Meeting Sessions" nếu có.
   - Report 2: nếu có hình WBS hoặc Gantt, bỏ FE-05 và đổi tên FE-12.
   - Report 1: nếu có hình sơ đồ tính năng, bỏ FE-05 và đổi tên FE-12.
4. **Không sửa các dòng Change Log cũ.** Ví dụ Report 1 dòng 22, Report 3 dòng 18–26 có nhắc tới
   Meeting Session. Đó là lịch sử, giữ nguyên, và chỉ **thêm** một dòng mới.
5. **Không đổi số ID.** UC-01 → UC-94, FE-01 → FE-16, BR-01 → BR-157 và mã MSG đều giữ nguyên số.
   Mục nào bị bỏ thì đánh dấu "Retired", không xoá số và không cấp lại số đó cho mục khác.

### Quy ước Change Log (áp dụng cho cả 3 report)

Bảng có 4 cột: `Date | A* M, D | In charge | Change Description`.

- `Date` viết dạng `dd/mm/yyyy`, ví dụ `09/10/2026`.
- Cột `A*` ghi `A` (Added), `M` (Modified), `D` (Deleted), nhiều giá trị thì ghép bằng dấu phẩy
  (ví dụ `M, D`).
- `In charge` viết theo kiểu Tên + chữ cái đầu của họ và tên đệm: `KhanhPG` (Pham Gia Khanh),
  `LocHD` (Hoang Duc Loc). Theo quy ước đó Tran Viet Hai là `HaiTV`.
- Mô tả là một câu tiếng Anh, kết thúc bằng dấu chấm.
- Dòng mới thêm **ở cuối bảng**, ngay trên chú thích `*A - Added M - Modified D - Deleted`.

---

## 1. `Report-1_Project_Introduction.md` (Report 1 – Project Introduction)

### R1-01 [BẮT BUỘC] Change Log — thêm dòng mới
- **Dòng:** sau dòng 23, tức sau dòng `| 05/10/2026 | M | KhanhPG | Synchronize the PO-confirmed Master UC baseline: …`.
- **Mục:** `## Change Log`
- **Hành động:** thêm một hàng mới vào cuối bảng. Ô `In charge` điền tên người sửa DOCX, mặc định
  `KhanhPG` vì BA chốt report; nếu Hải tự sửa thì ghi `HaiTV`.
- **Văn bản mới (4 ô):**
```text
09/10/2026 | M, D | KhanhPG | Apply the Product Owner decisions of 09/10/2026: one meeting now represents a single sitting and carries its own schedule, so FE-04 Meeting Management absorbs scheduling and post-publication rescheduling, FE-05 Meeting Session Management is removed and its identifier retired without renumbering later features, and FE-06 Agenda Items belong directly to the meeting; voting is limited to votes held during the meeting with a conclusion recorded by the Chair, so FE-12 is renamed Voting Management, time-boxed votes, quorum rules and immutable resolutions are removed, and GAP-05, FE-13, FE-16 and Section IV are aligned.
```

### R1-02 Mục III.1 Fellow — KHÔNG SỬA
- **Dòng:** 95. **Hiện tại:** "…structured voting process with formal voting sessions and recorded voting outcomes".
- **Hành động:** giữ nguyên. Câu này nói về "voting session" của sản phẩm Fellow, không phải
  Meeting Session.

### R1-03 [BẮT BUỘC] Mục IV — Proposed Solution, đoạn 1
- **Dòng:** 138
- **Hiện tại:** "a meeting approval workflow before invitations are sent, voting during or after sessions, and speaker-attributed transcription"
- **Hành động:** trong đoạn này thay đúng cụm `voting during or after sessions` bằng cụm mới, phần
  còn lại giữ nguyên.
- **Văn bản mới:**
```text
voting during the meeting
```

### R1-04 [BẮT BUỘC] Mục V — bảng Gap, dòng GAP-05
- **Dòng:** 156
- **Hiện tại (cột Description):** "Structured voting on an agenda item, an immutable record of resolutions, digitally signed minutes and an audit trail of who approved what are absent…"
- **Hành động:** thay toàn bộ ô Description của GAP-05. Cột ID và cột Gap giữ nguyên.
- **Văn bản mới (ô Description):**
```text
Structured voting on an agenda item during the meeting, a recorded result and conclusion for each vote, digitally signed minutes and an audit trail of who approved what are absent from the reviewed products, so formally binding meeting outcomes cannot be produced inside the tool. (Fireflies.ai, Asana + Zoom + AI Studio, ClickUp)
```

### R1-05 [BẮT BUỘC] Mục V.1 Major Features — FE-04
- **Dòng:** 166
- **Hiện tại (cột Description):** "Meeting Organizers create and update meeting-level information, submit Draft or Rejected meetings … session scheduling, agenda preparation and participant management are handled by their dedicated features."
- **Hành động:** thay toàn bộ ô Description. Tên `Meeting Management` và cột Gaps `GAP-03, GAP-05` giữ nguyên.
- **Văn bản mới (ô Description):**
```text
A meeting represents a single sitting. Meeting Organizers create a meeting together with its schedule - planned start and end time, meeting mode, and location or online meeting link - update it while it is Draft or Rejected, submit it to an assigned Meeting Approver, and publish it once approved. After publication, only the time, location and online meeting link may be changed (rescheduling), and every invitee is notified of the new schedule. The assigned approver approves or rejects with a recorded reason, while the Chair explicitly starts and ends the meeting. Every lifecycle transition is retained for traceability; agenda preparation and participant management are handled by their dedicated features.
```

### R1-06 [BẮT BUỘC] Mục V.1 — FE-05
- **Dòng:** 167
- **Hiện tại:** `| FE-05 | Meeting Session Management | The Secretary creates, updates or cancels Planned Meeting Sessions … | GAP-03 |`
- **Hành động (phương án đề xuất là giữ số FE, xem câu hỏi Q8):** **giữ** hàng FE-05 nhưng thay cả
  3 ô Feature Name, Description và Addressed Gaps. **Không** đánh số lại FE-06 → FE-16.
- **Văn bản mới:**
  - Ô Feature Name:
```text
Meeting Session Management (Removed)
```
  - Ô Description:
```text
Removed on 09/10/2026. A meeting now represents a single sitting and carries its own schedule and lifecycle (FE-04), so a separate Meeting Session level is no longer needed. The identifier FE-05 is retired and is not reused; the features that follow keep their numbers.
```
  - Ô Addressed Gaps:
```text
—
```
- **Lưu ý:** nếu PO chọn đánh số lại (Q8), phương án đó **không** nằm trong tài liệu này. Khi đó
  phải sửa lại toàn bộ R1, R2, R3 và mọi tài liệu khác có dùng FE-06 → FE-16.

### R1-07 [BẮT BUỘC] Mục V.1 — FE-06
- **Dòng:** 168
- **Hiện tại:** "The Secretary creates, updates, deletes and reorders Agenda Items for a Planned Meeting Session. During an active session, the Chair sets the current Agenda Item…"
- **Hành động:** thay toàn bộ ô Description.
- **Văn bản mới:**
```text
The Secretary creates, updates, deletes and reorders the Agenda Items of a meeting before the meeting starts. While the meeting is in progress, the Chair sets the current Agenda Item and completes it so that discussion progress and actual time used are recorded in a structured meeting record.
```

### R1-08 [BẮT BUỘC] Mục V.1 — FE-12
- **Dòng:** 174
- **Hiện tại:** `| FE-12 | Voting & Resolution Management | The Chair opens a vote on an agenda item live during a meeting, or publishes a time-boxed vote … a quorum rule. The outcome is recorded as a resolution attached to the meeting and cannot be altered afterwards. | GAP-05 |`
- **Hành động:** thay ô Feature Name và ô Description. Gaps giữ `GAP-05`.
- **Văn bản mới:**
  - Ô Feature Name:
```text
Voting Management
```
  - Ô Description:
```text
While a meeting is in progress, the Chair creates and opens a vote on an agenda item, with configurable options, an eligible-voter list and open or anonymous ballots. Eligible participants cast their votes, the Chair closes the vote, the system tallies the result, and the Chair records the conclusion as Passed, Not Passed or Deferred. Votes are held only during the meeting; there is no voting before or after it. Each vote's result and conclusion are kept with the meeting and compiled into the minutes (FE-13).
```

### R1-09 [KHUYẾN NGHỊ] Mục V.1 — FE-13
- **Dòng:** 175
- **Hiện tại:** "Approved summaries, decisions and voting results are compiled into formal minutes…"
- **Hành động:** chỉ thay đúng cụm đầu câu, phần còn lại của ô giữ nguyên.
- **Văn bản mới (thay cho `Approved summaries, decisions and voting results are compiled`):**
```text
Approved summaries, decisions, and vote results with the conclusions recorded by the Chair are compiled
```

### R1-10 [BẮT BUỘC] Mục V.1 — FE-16
- **Dòng:** 178
- **Hiện tại:** "…obtain aggregated figures on meetings held, attendance, decisions and resolutions recorded and minutes signed…"
- **Hành động:** thay cụm `decisions and resolutions recorded` bằng cụm mới.
- **Văn bản mới:**
```text
votes held and their conclusions, decisions recorded
```
  Sau khi thay, câu sẽ đọc là: "…aggregated figures on meetings held, attendance, votes held and
  their conclusions, decisions recorded and minutes signed, broken down by…"

### R1-11 Các chỗ đã kiểm tra, KHÔNG SỬA
- FE-10 (dòng 172) và LI-01 (dòng 184) đã ghi Secretary là người upload audio, tức đã đáp ứng D3.
  Report 1 không có chữ "Audio Uploader".
- FE-14 (dòng 176) có "voting calls" và FE-15 (dòng 177) có "open votes": vẫn đúng với biểu quyết
  trong họp.
- FE-01 (dòng 163) có "session expiry": đây là phiên đăng nhập, không phải Meeting Session.
- Mục I.2 (dòng 52) có "decisions and voting results": vẫn đúng.

**Tổng Report 1: 9 thay đổi (R1-01, R1-03 → R1-10), trong đó 8 bắt buộc và 1 khuyến nghị.**

---

## 2. `Report-2_Project_Management_Plan.md` (Report 2 – Project Management Plan)

> Report 2 §II.6.1 (dòng 641) yêu cầu: thay đổi phạm vi phải đi qua change request, sửa **Report 2
> trước** rồi mới tới các report khác. Thứ tự áp dụng là Report 2 → Report 1 → Report 3, mỗi report
> có dòng Change Log riêng.

### R2-01 [BẮT BUỘC] Change Log — thêm dòng mới
- **Dòng:** sau dòng 20.
- **Văn bản mới (4 ô):**
```text
09/10/2026 | M, D | KhanhPG | Synchronize the project plan with the Product Owner decisions of 09/10/2026: remove FE-05 Meeting Session Management from the work packages and iterations (its identifier is retired without renumbering later features), fold meeting scheduling and post-publication rescheduling into FE-04, rename FE-12 to Voting Management, and restate the Iteration 5 voting scope as votes held during the meeting with a conclusion recorded by the Chair, without time-boxed votes, quorum rules or immutable resolutions; effort and dates are unchanged.
```

### R2-02 [BẮT BUỘC] Mục I.1 — câu dẫn danh sách tính năng
- **Dòng:** 38
- **Hiện tại:** "The work to be implemented consists of the sixteen major features baselined in Report 1, Section V.1, each of which is traceable to a gap identified in Report 1, Section III:"
- **Hành động:** thay cả câu.
- **Văn bản mới:**
```text
The work to be implemented consists of the fifteen major features baselined in Report 1, Section V.1 - identified FE-01 to FE-16, FE-05 having been retired on 09/10/2026 without renumbering the others - each of which is traceable to a gap identified in Report 1, Section III:
```

### R2-03 [BẮT BUỘC] Mục I.1 — gạch đầu dòng "Meeting operations"
- **Dòng:** 42
- **Hiện tại:** "Meeting operations - FE-04 Meeting Management, FE-05 Meeting Session Management, FE-06 Agenda Management, …"
- **Hành động:** xoá đúng cụm `FE-05 Meeting Session Management, ` (có cả dấu phẩy và khoảng trắng).
- **Kết quả:**
```text
Meeting operations - FE-04 Meeting Management, FE-06 Agenda Management, FE-07 Meeting Participant Management, FE-08 Personal Document Library & Meeting Document Management, FE-09 Online Meeting Room Management.
```

### R2-04 [BẮT BUỘC] Mục I.1 — gạch đầu dòng "Meeting governance"
- **Dòng:** 46
- **Hiện tại:** "Meeting governance - FE-12 Voting & Resolution Management, FE-13 Meeting Minutes & Digital Signing."
- **Văn bản mới:**
```text
Meeting governance - FE-12 Voting Management, FE-13 Meeting Minutes & Digital Signing.
```

### R2-05 [KHUYẾN NGHỊ] Mục I.3 — đoạn mở đầu bảng WBS
- **Dòng:** 83
- **Hiện tại:** "The feature identifiers (FE-01 to FE-16) are those baselined in Report 1, Section V.1."
- **Văn bản mới:**
```text
The feature identifiers (FE-01 to FE-16, with FE-05 retired) are those baselined in Report 1, Section V.1.
```

### R2-06 [KHUYẾN NGHỊ] Bảng WBS, hàng 2 — tiêu đề Iteration 2
- **Dòng:** 102
- **Hiện tại:** "Iteration 2: Meeting, Document & Notification Core - FE-04 to FE-08, FE-14 (weeks 4-5)"
- **Hành động:** thay cụm `FE-04 to FE-08, FE-14` bằng cụm mới. Áp dụng **giống hệt** cho tiêu đề
  ở dòng 301 và cho câu ở dòng 309 (xem R2-10, R2-11).
- **Văn bản mới (cụm):**
```text
FE-04, FE-06 to FE-08, FE-14
```

### R2-07 [BẮT BUỘC] Bảng WBS, hàng 2.5
- **Dòng:** 107
- **Hiện tại:** `| 2.5 | FE-04 to FE-07 Meeting Management, Meeting Session Management, Agenda Management & Meeting Participant Management (including the meeting approval workflow) | 6 | 18/10/2026 |`
- **Hành động:** thay ô Work Package. **Giữ effort `6` và ngày `18/10/2026`** (xem Q9).
- **Văn bản mới (ô Work Package):**
```text
FE-04, FE-06 and FE-07 Meeting Management, Agenda Management & Meeting Participant Management (including meeting scheduling and rescheduling and the meeting approval workflow)
```

### R2-08 [BẮT BUỘC] Bảng WBS, hàng 5.3
- **Dòng:** 132
- **Hiện tại:** `| 5.3 | FE-12 Voting & Resolution Management | 6 | 29/11/2026 |`
- **Hành động:** thay ô Work Package, giữ effort `6` và ngày (xem Q9).
- **Văn bản mới (ô Work Package):**
```text
FE-12 Voting Management - in-meeting votes and the Chair's conclusion
```

### R2-09 [BẮT BUỘC] Bảng rủi ro, R04
- **Dòng:** 196
- **Hiện tại (cột Description):** "Sixteen features in fifteen weeks exceeds what five part-time members can build…"
- **Hành động 1:** trong cột Description, thay `Sixteen features in fifteen weeks` bằng:
```text
Fifteen features in fifteen weeks
```
- **Hành động 2:** trong cột Mitigation, thay `must-have (FE-01 to FE-14)` bằng:
```text
must-have (FE-01 to FE-14, excluding the retired FE-05)
```

### R2-10 [KHUYẾN NGHỊ] Tiêu đề Iteration 2
- **Dòng:** 301
- **Hiện tại:** "Iteration 2: Meeting, Document & Notification Core - FE-04 to FE-08, FE-14 (2 weeks)"
- **Văn bản mới:**
```text
Iteration 2: Meeting, Document & Notification Core - FE-04, FE-06 to FE-08, FE-14 (2 weeks)
```

### R2-11 [KHUYẾN NGHỊ] Iteration 2 — Planning
- **Dòng:** 309
- **Hiện tại:** "Planning - Set the iteration goal and select FE-04 to FE-08 and FE-14; …"
- **Hành động:** thay `select FE-04 to FE-08 and FE-14` bằng:
```text
select FE-04, FE-06 to FE-08 and FE-14
```

### R2-12 [BẮT BUỘC] Iteration 2 — Requirements
- **Dòng:** 311
- **Hiện tại:** "Requirements - Elaborate meeting lifecycle management; Meeting Session scheduling; agenda preparation and live agenda control; …"
- **Hành động:** thay cụm `Meeting Session scheduling;` bằng:
```text
meeting scheduling, including rescheduling a published meeting with notification to its invitees;
```

### R2-13 [BẮT BUỘC] Iteration 2 — Implementation
- **Dòng:** 315
- **Hiện tại:** "Implementation - Build FE-04 Meeting Management, FE-05 Meeting Session Management, FE-06 Agenda Management, …"
- **Hành động:** xoá cụm `FE-05 Meeting Session Management, ` (có cả dấu phẩy và khoảng trắng).

### R2-14 [BẮT BUỘC] Iteration 5 — Iteration goal
- **Dòng:** 371
- **Hiện tại:** "Iteration goal: Turn a meeting discussion into a formally recorded outcome: a resolution and signed, sealed minutes."
- **Văn bản mới:**
```text
Iteration goal: Turn a meeting discussion into a formally recorded outcome: a vote conclusion recorded by the Chair and signed, sealed minutes.
```

### R2-15 [BẮT BUỘC] Iteration 5 — Requirements
- **Dòng:** 377
- **Hiện tại:** "Requirements - Elaborate both voting modes - a vote opened live on an agenda item during a meeting, and a time-boxed vote published with a deadline - including options, eligible-voter lists, open/anonymous ballots, quorum and immutable Resolution rules; define the structure of the minutes; …"
- **Hành động:** thay phần đầu câu, từ `Elaborate both voting modes` đến hết `immutable Resolution rules;`.
  Phần sau, bắt đầu từ ` define the structure of the minutes;`, giữ nguyên.
- **Văn bản mới (phần đầu):**
```text
Requirements - Elaborate voting during the meeting - a vote created and opened by the Chair on an agenda item while the meeting is in progress, cast by eligible participants, closed by the Chair and tallied by the system, after which the Chair records the conclusion as Passed, Not Passed or Deferred - including options, eligible-voter lists and open/anonymous ballots;
```

### R2-16 [BẮT BUỘC] Iteration 5 — Analysis & design
- **Dòng:** 379
- **Hiện tại:** "Analysis & design - Design the resolution and signature records so that they become immutable once sealed, and the template that compiles the approved summary, decisions and voting results into the minutes; …"
- **Hành động:** thay `Design the resolution and signature records so that they become immutable once sealed, and the template that compiles the approved summary, decisions and voting results into the minutes;` bằng:
```text
Design the vote and ballot records, the signature records that become immutable once the minutes are sealed, and the template that compiles the approved summary, decisions, vote results and vote conclusions into the minutes;
```

### R2-17 [BẮT BUỘC] Iteration 5 — Implementation
- **Dòng:** 381
- **Hiện tại:** "Implementation - Build FE-12 Voting & Resolution Management and FE-13 Meeting Minutes & Digital Signing."
- **Văn bản mới:**
```text
Implementation - Build FE-12 Voting Management and FE-13 Meeting Minutes & Digital Signing.
```

### R2-18 [BẮT BUỘC] Iteration 5 — Testing
- **Dòng:** 383
- **Hiện tại:** "…integration tests along the chain from approved summary and vote to resolution and signed minutes; …"
- **Hành động:** thay `from approved summary and vote to resolution and signed minutes` bằng:
```text
from approved summary and closed vote with its recorded conclusion to signed minutes
```

### R2-19 [BẮT BUỘC] Iteration 5 — Evaluation
- **Dòng:** 385
- **Hiện tại:** "…the summary is approved, a vote is recorded as a resolution, and the minutes are signed and sealed."
- **Hành động:** thay `a vote is recorded as a resolution` bằng:
```text
a vote held during the meeting has its conclusion recorded by the Chair
```

### R2-20 [BẮT BUỘC] Iteration 5 — Increment delivered
- **Dòng:** 387
- **Hiện tại:** "Software Package v5.0 - increment 4 plus voting and resolutions and digitally signed meeting minutes."
- **Hành động:** thay `voting and resolutions and digitally signed meeting minutes` bằng:
```text
in-meeting voting with recorded conclusions and digitally signed meeting minutes
```

### R2-21 [BẮT BUỘC] Mục Team — đoạn mô tả đội
- **Dòng:** 546
- **Hiện tại:** "Because five members cannot cover sixteen features in specialised silos, …"
- **Hành động:** thay `sixteen features` bằng:
```text
fifteen features
```

### R2-22 Các chỗ đã kiểm tra, KHÔNG SỬA
- Dòng 289 "session expiry" là phiên đăng nhập. Dòng 361 "a full-team session" và các dòng 417,
  419, 515, 517, 521 "acceptance session", "the session" là buổi kiểm thử. Không đụng tới.
- Dòng 54, 81, 189, 195 có chữ "deadline" nhưng đó là hạn của dự án hoặc rủi ro, không phải
  deadline của phiếu biểu quyết.
- Dòng 333 và 355 đã ghi Secretary upload audio, tức D3 đã đáp ứng. Report 2 không có chữ
  "Audio Uploader".
- Dòng 227, 305, 375 và 401 nói "votes", "voting results" chung chung, vẫn đúng.
- Dòng 18–20 là Change Log cũ, giữ nguyên.

**Tổng Report 2: 21 thay đổi (R2-01 → R2-21), trong đó 17 bắt buộc và 4 khuyến nghị.**

---

## 3. `Report-3_Software_Requirement_Specification.md` (Report 3 – SRS)

### Quy tắc chung cho Report 3

- **UC bị bỏ** (UC-26, 27, 28, 29, 30, 53): giữ ID và giữ tiêu đề mục, thêm `(Retired)` sau tên,
  thay nội dung bằng một ghi chú ngắn (xem R3-30 → R3-36).
- **BR bị bỏ:** giữ hàng trong bảng, viết lại ô Rule Statement theo mẫu bên dưới. Ô `Applies To`
  ghi `—`.
```text
[Retired 09/10/2026] <tóm tắt quy tắc cũ>. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.
```
  BR nào vẫn còn ý nghĩa (chỉ đổi chủ thể từ Session sang Meeting) thì **viết lại tại chỗ, giữ
  nguyên ID**.
- **MSG bị bỏ:** giữ hàng, thêm tiền tố `[Retired] ` vào đầu ô Context, không đổi số.
- **Khi làm xong:** tìm "Meeting Session" trong DOCX. Chỉ được phép còn ở các chỗ sau:
  - Change Log cũ (dòng 18–26).
  - Mục §II.5 đã retire.
  - 6 hàng UC retired trong §I.4.2.
  - Các BR retired.
  - Các MSG retired.

  Đếm hiện tại là 182 lần.
- **Không đụng tới** các chữ "session" sau vì chúng là phiên đăng nhập hoặc phiên khách:
  - Dòng 160, 343, 405, 415–417 nói về đăng nhập.
  - Dòng 476 và 487 là "user session".
  - Dòng 797 và 799 là "guest session".
  - Dòng 857 và 859 là "guest session". Ở 2 dòng này chỉ sửa phần Meeting Session.
  - BR-18 (dòng 1350).
  - MSG01/MSG02 "Session Renewal" (dòng 1502–1503).

### R3-01 [BẮT BUỘC] Change Log — thêm dòng mới
- **Dòng:** sau dòng 29.
- **Văn bản mới (4 ô):** chọn `[A]` hoặc `[B]` theo câu trả lời của PO ở Q2. Nếu chọn `[A]`, ghi
  `UC-76 renamed Record Vote Conclusion`. Nếu chọn `[B]`, ghi `UC-76 View Resolution retired`.
```text
09/10/2026 | M, D | KhanhPG | Apply the Product Owner decisions of 09/10/2026. Remove the Meeting Session level so that a meeting is a single sitting with its own planned schedule: retire FE-05 and UC-26 to UC-30 and UC-53 without renumbering (UC-26 and UC-27 merged into UC-15 and UC-16, UC-29 and UC-30 duplicate UC-43 and UC-44); add Reschedule a Published Meeting to UC-16; attach Agenda Items directly to the meeting in UC-31 to UC-36 and make UC-54 extend UC-17; remove Meeting Session conditions from UC-15 to UC-21, UC-43, UC-44 and UC-45 to UC-52; retire BR-54 to BR-61, BR-63, BR-77, BR-128, BR-133 to BR-136, BR-138 and BR-142; reword BR-25, BR-27, BR-30, BR-33, BR-62, BR-64, BR-65, BR-67 to BR-69, BR-71 to BR-76, BR-137 and BR-141; add BR-158; retire MSG48 to MSG59, MSG113 and MSG127. Limit voting to votes held during the meeting with a conclusion recorded by the Chair: rename FE-12 to Voting Management and UC-72 to Open Vote; [A] UC-76 renamed Record Vote Conclusion / [B] UC-76 View Resolution retired.
```

### R3-02 Mục I.1 Context Diagram — KHÔNG SỬA CHỮ
- Dòng 46–54 không nhắc tới Session, quorum hay resolution. Chỉ cần kiểm tra **hình** context
  diagram trong DOCX (xem mục 0.3).

### R3-03 Mục I.3 Conceptual Data Model — KHÔNG SỬA CHỮ
- Dòng 76–138 vẫn là mẫu template (User, Job Posting, Application…), chưa có thực thể của
  TrustCabinet.
- Khi điền thật, **không** tạo khối `MeetingSession` hay `Resolution`. Thực thể Meeting phải có
  Planned Start/End Time, Mode, Location/Online link và vòng đời
  `Draft → WaitingApproval → Approved | Rejected → Published → InProgress → Ended`.

### R3-04 [KHUYẾN NGHỊ] Mục I.2 Main Business Flow — thay mẫu template bằng BF thật
- **Dòng:** 60–74. Hiện là mẫu "Candidate Self-Service (BF-01)", "End-to-End Recruitment (BF-02)",
  "Business Flow Name (Code)".
- **Hành động:** xoá 3 tiểu mục 2.1, 2.2, 2.3 mẫu và thay bằng 4 tiểu mục dưới đây.
  - Ở mỗi tiểu mục, chèn hình PNG tương ứng từ `docs/09-bussinessflow/` ngay sau dòng
    "End condition".
  - Chú thích hình đặt ngay dưới hình.
  - Giữ nguyên dòng hướng dẫn `[List all flows with swimlane diagrams as samples below].` nếu
    template bắt buộc, không thì xoá.
- **Văn bản mới:**
```text
2.1 System Access & Administration (BF-01)
Trigger: The System Admin creates or updates a user account or role, or an internal user opens TrustCabinet to sign in.
End condition: The user is signed in with the permissions of their role, or the sign-in is refused with an error or the account is locked.
Figure 1 – BF-01 System Access & Administration

2.2 Meeting Preparation & Approval (BF-02)
Trigger: The Meeting Organizer creates a meeting with its title, schedule, mode, location and sensitivity.
End condition: The meeting is Published, invitations have been sent and invitees have responded; if the meeting was rescheduled, every invitee has been notified of the new schedule.
Figure 2 – BF-02 Meeting Preparation & Approval

2.3 Meeting Execution & Decision Making (BF-03)
Trigger: The Chair starts a Published meeting.
End condition: The meeting is Ended, every vote held during it has a conclusion recorded by the Chair (Passed, Not Passed or Deferred), and the online meeting room is closed.
Figure 3 – BF-03 Meeting Execution & Decision Making

2.4 Post-Meeting Processing & Formal Records (BF-04)
Trigger: The meeting has Ended and the system asks the Secretary to upload the meeting audio.
End condition: The minutes, compiled from the meeting information, the approved summary and the vote results, are signed by every designated signer, finalized and sealed, and authorized users can access the meeting records.
Figure 4 – BF-04 Post-Meeting Processing & Formal Records
```
- **Định dạng:** `2.1`…`2.4` dùng style Heading 4, giống "2.1 Candidate Self-Service (BF-01)" hiện
  tại. Hai dòng "Trigger:" và "End condition:" là đoạn thường.

### R3-05 [BẮT BUỘC] Mục I.4.1 Actors — Meeting Organizer
- **Dòng:** 147
- **Hiện tại:** "A system-authorized user responsible for creating, updating, submitting, organizing, and publishing meetings under their management."
- **Văn bản mới (ô Description):**
```text
A system-authorized user responsible for creating, updating, submitting, organizing, publishing and, after publication, rescheduling meetings under their management.
```

### R3-06 [BẮT BUỘC] Mục I.4.1 Actors — Chair
- **Dòng:** 150
- **Hiện tại:** "…including starting or ending the meeting, controlling Meeting Sessions, and moderating authorized meeting-room interactions."
- **Hành động:** thay `controlling Meeting Sessions,` bằng:
```text
setting and completing the current agenda item, creating, opening and closing votes and recording their conclusions,
```

### R3-07 [BẮT BUỘC] Mục I.4.1 Actors — Secretary
- **Dòng:** 151
- **Hiện tại:** "A user assigned to support a specific meeting by preparing Meeting Sessions and agenda items, …"
- **Hành động:** thay `preparing Meeting Sessions and agenda items,` bằng:
```text
preparing agenda items,
```
  Phần còn lại giữ nguyên. Phần này đã ghi "uploading the post-meeting audio", tức D3 đã đáp ứng.

### R3-08 [KHUYẾN NGHỊ] Mục I.4.1 Actors — Participant
- **Dòng:** 152
- **Hiện tại:** "…responding to invitations, and voting."
- **Hành động:** thay `and voting.` bằng:
```text
and voting on votes opened during the meeting.
```

### R3-09 [BẮT BUỘC] Mục I.4.2 bảng Use Cases — các hàng UC-15 → UC-54
Thay ô **Use Case Description**. Riêng các hàng có ghi chú thì thay cả ô Use Case và ô Feature.

| Dòng | UC | Ô cần thay | Văn bản mới (chép nguyên văn) |
|---|---|---|---|
| 172 | UC-15 | Description | `Create a new meeting, which represents a single sitting, with its required information including the planned start and end time, meeting mode, and location or online meeting link.` |
| 173 | UC-16 | Description | `Update the information of a Draft or Rejected meeting, or reschedule a Published meeting by changing only its time, location or online meeting link.` |
| 174 | UC-17 | Description | `View the details of an authorized meeting, including its planned schedule, without loading its Agenda Items.` |
| 178 | UC-21 | Description | `Publish an approved meeting and initiate participant invitations.` |
| 183 | UC-26 | Use Case / Feature / Description | Use Case: `Create Meeting Session (Retired)`; Feature: `—`; Description: `Retired on 09/10/2026; merged into UC-15 Create Meeting. The ID is kept and never reused.` |
| 184 | UC-27 | Use Case / Feature / Description | Use Case: `Update Meeting Session (Retired)`; Feature: `—`; Description: `Retired on 09/10/2026; merged into UC-16 Update Meeting, which covers rescheduling after publication. The ID is kept and never reused.` |
| 185 | UC-28 | Use Case / Feature / Description | Use Case: `Cancel Meeting Session (Retired)`; Feature: `—`; Description: `Retired on 09/10/2026; there is no Meeting Session to cancel. The ID is kept and never reused.` |
| 186 | UC-29 | Use Case / Feature / Description | Use Case: `Start Meeting Session (Retired)`; Feature: `—`; Description: `Retired on 09/10/2026; duplicate of UC-43 Start Meeting. The ID is kept and never reused.` |
| 187 | UC-30 | Use Case / Feature / Description | Use Case: `End Meeting Session (Retired)`; Feature: `—`; Description: `Retired on 09/10/2026; duplicate of UC-44 End Meeting. The ID is kept and never reused.` |
| 188 | UC-31 | Description | `Create a planned agenda item for a meeting before the meeting starts.` |
| 189 | UC-32 | Description | `Update the planned information of an agenda item before the meeting starts.` |
| 190 | UC-33 | Description | `Remove an unnecessary agenda item before the meeting starts.` |
| 191 | UC-34 | Description | `Change the order of the agenda items of a meeting before the meeting starts.` |
| 192 | UC-35 | Description | `Set an agenda item as the topic currently being discussed while the meeting is InProgress.` |
| 200 | UC-43 | Description | `Start a Published meeting and move the Meeting lifecycle to InProgress independently of room participation.` |
| 201 | UC-44 | Description | `Explicitly end an InProgress meeting and close its online meeting room.` |
| 203 | UC-46 | Description | `Leave the online meeting room without changing the Meeting lifecycle.` |
| 210 | UC-53 | Use Case / Feature / Description | Use Case: `View Meeting Sessions (Retired)`; Feature: `—`; Description: `Retired on 09/10/2026; the planned schedule is shown in UC-17 View Meeting Details. The ID is kept and never reused.` |
| 211 | UC-54 | Description | `View the Agenda Items of an authorized meeting; extends UC-17 View Meeting Details (<<extend>>).` |

- Hàng UC-36 (dòng 193) **không sửa**. Nó không nhắc tới Session.

### R3-10 [BẮT BUỘC] Mục I.4.2 bảng Use Cases — UC-70 → UC-76 (biểu quyết)
- **Dòng:** 227–233
- **Hành động 1:** ở cả 7 hàng UC-70 → UC-76, đổi ô Feature từ `Voting & Resolution Management` thành:
```text
Voting Management
```
- **Hành động 2:** dòng 229, UC-72, đổi ô Use Case từ `Open / Publish Vote` thành:
```text
Open Vote
```
  và thay ô Description bằng:
```text
Allow the Chair to open a configured vote for casting while the meeting is InProgress.
```
- **Hành động 3:** dòng 233, UC-76 **[CHỜ PO — Q2]**. Chọn **một** trong hai phương án:
  - **Phương án A — đổi thành Record Vote Conclusion.**
    - Ô Use Case: `Record Vote Conclusion`
    - Ô Feature: `Voting Management`
    - Ô Description:
```text
Allow the Chair to record the conclusion of a closed vote as Passed, Not Passed or Deferred.
```
  - **Phương án B — bỏ UC-76.**
    - Ô Use Case: `View Resolution (Retired)`
    - Ô Feature: `—`
    - Ô Description:
```text
Retired on 09/10/2026; there is no separate Resolution record. Vote results are viewed through UC-74 View Vote Status and the Chair records the conclusion in UC-75 Close Vote. The ID is kept and never reused.
```
    - Với phương án B, sửa thêm hàng UC-75 (dòng 232). Thay ô Description `Placeholder only - …` bằng:
```text
Allow the Chair to close an open vote; the system tallies the ballots and the Chair records the conclusion as Passed, Not Passed or Deferred.
```
- **[KHUYẾN NGHỊ]** Hàng UC-74 (dòng 231): thay ô Description `Placeholder only - …` bằng:
```text
View the progress of a vote and, once it is closed, its tally.
```
  Các hàng UC-70, 71, 73 (và UC-75 nếu chọn phương án A) giữ nguyên `Placeholder only…`.

### R3-11 Mục I.4.3 Use Case Diagrams, I.4.4 Permission Matrix, I.5 System Functionalities — KHÔNG SỬA CHỮ
- Các mục này vẫn là template (Guest/Candidate, Admin/HR Manager…). Chỉ cần kiểm tra **hình**
  (mục 0.3).
- Khi vẽ UC diagram thật thì:
  - không vẽ UC-26 → 30 và UC-53;
  - nối UC-54 với UC-17 bằng `<<extend>>`;
  - UC-72 đặt tên "Open Vote";
  - UC-76 đặt tên theo Q2.

### R3-12 [BẮT BUỘC] §II.4.1 UC-15 Create Meeting
- **Dòng:** 502–507
- **Ô Description (dòng 502).** Hiện tại: "Allows the Meeting Organizer to create a new meeting using the required meeting-level information. Planned Start Time and Planned End Time are defined through Meeting Sessions and are not required when the meeting is first created." Thay toàn bộ ô bằng:
```text
Allows the Meeting Organizer to create a new meeting. A meeting represents a single sitting, so its planned schedule - Planned Start Time, Planned End Time, Meeting Mode, and Location or Online Meeting Link - is entered as part of the meeting.
```
- **Ô Postconditions (dòng 504).** Thay đúng đoạn `● POST-4: Meeting scheduling is managed separately through Meeting Sessions when the schedule becomes known.` bằng:
```text
● POST-4: The planned schedule is stored with the meeting. (BR-25)
```
- **Ô Normal Flow (dòng 505), bước 3.** Thay danh sách
  `○ Meeting Name<br>○ Meeting Mode<br>○ Location<br>○ Confidentiality Level<br>○ Description` bằng:
```text
○ Meeting Name<br>○ Planned Start Time<br>○ Planned End Time<br>○ Meeting Mode<br>○ Location / Online Meeting Link<br>○ Confidentiality Level<br>○ Description
```
  Đây là 7 dòng con trong ô. Mỗi `<br>` là một lần xuống dòng.
- **Ô Normal Flow, bước 5.** Thay `5. The system validates the entered meeting-level information (BR-24).` bằng:
```text
5. The system validates the entered meeting information, including that the Planned End Time is later than the Planned Start Time. (BR-24, BR-25, BR-62)
```
- **Ô Exception Flows, EX-1 bước 1.** Thay `1. At Step 5, one or more entered values are missing or invalid.` bằng:
```text
1. At Step 5, one or more entered values are missing or invalid, or the Planned End Time is not later than the Planned Start Time. (BR-24, BR-62)
```
- **[CHỜ PO — Q4, Q5]** Hai điểm còn chờ PO: một trường "Location / Online Meeting Link" hay hai
  trường riêng, và lịch có bắt buộc ngay khi tạo hay không. Văn bản trên giả định một trường gộp
  và **bắt buộc** ngay khi tạo (theo BF-02).

### R3-13 [BẮT BUỘC] §II.4.2 UC-16 Update Meeting — thêm Reschedule
- **Dòng:** 513–518
- **Ô Description (dòng 513).** Thay toàn bộ ô bằng:
```text
Allows the Meeting Organizer to modify the information of a meeting, including its planned schedule, while the meeting is Draft or Rejected, and to reschedule a Published meeting by changing only its Planned Start Time, Planned End Time, or Location / Online Meeting Link.
```
- **Ô Preconditions (dòng 514).** Thay `● PRE-4: At the time the edit action is initiated, the meeting is in Draft or Rejected status.` bằng:
```text
● PRE-4: At the time the edit action is initiated, the meeting is in Draft or Rejected status; for AF-1 Reschedule a Published Meeting, the meeting is in Published status.
```
- **Ô Postconditions (dòng 515).** Giữ nguyên POST-1 → POST-3 và thêm vào cuối ô (sau POST-3, xuống dòng trong ô):
```text
● POST-4: After a reschedule (AF-1), all current internal participants and active external Guest invitees are notified of the new schedule.
```
- **Ô Normal Flow, bước 4.** Thay `4. The system validates the submitted meeting-level information (BR-27).` bằng:
```text
4. The system validates the submitted meeting information, including that the Planned End Time is later than the Planned Start Time. (BR-27, BR-62)
```
- **Ô Alternative Flows (dòng 517).** Hiện là `N/A`. Thay bằng:
```text
AF-1 – Reschedule a Published Meeting<br>Entry Point: Step 1 of the Main Flow.<br>1. The Meeting Organizer selects a Published meeting and chooses Reschedule Meeting.<br>2. The system displays the current Planned Start Time, Planned End Time, and Location / Online Meeting Link; no other meeting information can be changed. (BR-158)<br>3. The Meeting Organizer changes one or more of these values and submits them.<br>4. The system validates that the Planned End Time is later than the Planned Start Time and that the Planned Start Time is later than the current time. (BR-62, BR-75)<br>5. The system verifies that the Meeting Organizer is still authorized and that the meeting is still Published. (BR-158)<br>6. The system saves the new schedule; the meeting remains Published. (BR-28)<br>7. The system notifies all current internal participants and active external Guest invitees of the new schedule by in-app notification and email. A delivery failure does not undo the reschedule. (BR-76)<br>8. The system displays MSG138 – "Meeting rescheduled successfully. Invitees will be notified."<br>9. The use case ends.
```
  Mã `MSG138` là mã kế tiếp **tại thời điểm audit**. Xem Q10 và R3-70 trước khi dùng.
- **Ô Exception Flows (dòng 518), EX-1 bước 1.** Thay `1. At Step 4, the submitted information is invalid.` bằng:
```text
1. At Step 4 of the Main Flow or Step 4 of AF-1, the submitted information is invalid, the Planned End Time is not later than the Planned Start Time, or, for a reschedule, the Planned Start Time is not later than the current time. (BR-62, BR-75)
```
  Trong EX-1, thay `4. The flow resumes at Step 4 of the Main Flow.` bằng:
```text
4. The flow resumes at the step where the error was detected.
```
- **Exception Flows, EX-2 bước 1.** Thay `1. At Step 4, the system determines that the meeting status has changed and the meeting is no longer Draft or Rejected.` bằng:
```text
1. At Step 4 of the Main Flow, the system determines that the meeting status has changed and the meeting is no longer Draft or Rejected; or at Step 5 of AF-1, the meeting is no longer Published or the Meeting Organizer is no longer authorized. (BR-27, BR-158)
```
- **Exception Flows, EX-3 bước 1.** Thay `1. At Step 5, the system cannot complete the update.` bằng:
```text
1. At Step 5 of the Main Flow or Step 6 of AF-1, the system cannot complete the update.
```
- **[CHỜ PO — Q3]** Ai được dời lịch: Organizer hay cả Chair? Có cho dời lịch khi meeting đang
  `Approved` không? Văn bản trên giả định chỉ Organizer và chỉ khi `Published`.

### R3-14 [BẮT BUỘC] §II.4.3 UC-17 View Meeting Details
- **Dòng:** 524–529
- **Description (dòng 524).** Thay `Meeting Sessions and Agenda Items are viewed through separate use cases.` bằng:
```text
Agenda Items are viewed through UC-54 View Meeting Agenda, which extends this use case.
```
- **Postconditions (dòng 526).** Thay `● POST-2: No Meeting, Meeting Session, or Agenda information is modified.` bằng:
```text
● POST-2: No Meeting or Agenda information is modified.
```
- **Normal Flow (dòng 527), bước 4.** Thay `4. The system displays Meeting Name, Meeting Mode, Location, Confidentiality Level, Description, and Meeting Status.` bằng:
```text
4. The system displays Meeting Name, Planned Start Time, Planned End Time, Meeting Mode, Location / Online Meeting Link, Confidentiality Level, Description, and Meeting Status.
```
- **Normal Flow, bước 5.** Thay `5. The user reviews the meeting-level details. Meeting Sessions and Agenda Items are not loaded by this use case.` bằng:
```text
5. The user reviews the meeting details. Agenda Items are not loaded by this use case; the user may open them through UC-54 View Meeting Agenda (<<extend>>).
```

### R3-15 [BẮT BUỘC] §II.4.4 UC-18 View Meeting List
- **Dòng:** 539, ô Alternative Flows, AF-2 bước 2.
- **Hiện tại:** "For Time Range, a meeting matches only when at least one non-cancelled Meeting Session has a planned schedule overlapping the selected range; meetings without a scheduled Meeting Session do not match. (BR-30)"
- **Hành động:** thay đúng câu trên bằng:
```text
For Time Range, a meeting matches when its planned schedule, from Planned Start Time to Planned End Time, overlaps the selected range. (BR-30)
```

### R3-16 [BẮT BUỘC] §II.4.5 UC-19 Submit Meeting for Approval
- **Dòng:** 549, ô Normal Flow, bước 2.
- **Hiện tại:** "2. The system verifies that all mandatory meeting-level information is complete. Meeting Sessions are not required for approval submission. (BR-33, BR-55)"
- **Văn bản mới:**
```text
2. The system verifies that all mandatory meeting information is complete, including the Planned Start Time and Planned End Time. (BR-33)
```

### R3-17 [BẮT BUỘC] §II.4.7 UC-21 Publish Meeting
- **Dòng:** 568–571
- **Description (dòng 568).** Xoá câu cuối `Meeting Sessions may be prepared before or after publication and are not required for publication.`
- **Preconditions (dòng 569), PRE-5.** Thay `Invitation Response and Meeting Session creation are not publication prerequisites. (BR-56, BR-124, BR-125, BR-129)` bằng:
```text
Invitation Response is not a publication prerequisite. (BR-124, BR-125, BR-129)
```
- **Postconditions (dòng 570).** Xoá đoạn `<br>● POST-4: Existing Meeting Sessions, if any, remain unchanged.`, tức là xoá hẳn dòng POST-4 trong ô.
- **Normal Flow (dòng 571), bước 3.** Thay `Meeting Sessions are not required. (BR-37, BR-56, BR-124, BR-125, BR-129)` bằng:
```text
(BR-37, BR-124, BR-125, BR-129)
```

### R3-18 [BẮT BUỘC] §II.4.8 UC-43 Start Meeting
- **Dòng:** 579–582
- **Description (dòng 579).** Xoá câu `A Meeting may be started even when no Meeting Session has been created.`
- **Preconditions (dòng 580), PRE-4.** Thay `Meeting Sessions are optional. (BR-56, BR-127, BR-133)` bằng:
```text
(BR-127)
```
- **Postconditions (dòng 581).** Xoá dòng `● POST-3: Existing Meeting Sessions, if any, remain unchanged.` và đổi `● POST-4: Existing room connections, if any, remain unchanged.` thành:
```text
● POST-3: Existing room connections, if any, remain unchanged.
```
- **Normal Flow (dòng 582), bước 3.** Thay `Meeting Sessions are not required. (BR-56, BR-127, BR-130, BR-131, BR-133)` bằng:
```text
(BR-127, BR-130, BR-131)
```
- **Normal Flow, bước 6.** Thay `6. Existing Meeting Sessions remain unchanged, and the Meeting Room remains available according to the meeting mode and lifecycle. (BR-135, BR-139)` bằng:
```text
6. The Meeting Room remains available according to the meeting mode and lifecycle. (BR-139)
```
- **[KHUYẾN NGHỊ, theo BF-03 "Set Meeting to InProgress & Notify Participants"]** Chèn một bước mới
  giữa bước 6 và bước 7, rồi đổi số bước 7 cũ thành 8:
```text
7. The system notifies the current internal participants and active external Guest invitees that the meeting has started.
```
  Nếu không làm khuyến nghị này thì giữ nguyên bước 7 cũ.

### R3-19 [BẮT BUỘC] §II.4.9 UC-44 End Meeting
- **Dòng:** 590–595
- **Description (dòng 590).** Thay toàn bộ ô bằng:
```text
Allows the Chair to explicitly end an InProgress meeting.
```
- **Preconditions (dòng 591).** Xoá dòng `● PRE-3: No Meeting Session remains Planned or InProgress. A meeting with no Meeting Sessions satisfies this condition. (BR-134)`
- **Postconditions (dòng 592).** Xoá dòng `● POST-3: Existing Meeting Session statuses remain unchanged.` và đổi `● POST-4: The online meeting room becomes unavailable …` thành `● POST-3: The online meeting room becomes unavailable …`. Phần chữ sau tiền tố giữ nguyên.
- **Normal Flow (dòng 593), bước 4.** Thay `4. The system verifies that the Chair is still assigned and active, the meeting is still InProgress, and no Meeting Session remains Planned or InProgress. (BR-130, BR-132, BR-134)` bằng:
```text
4. The system verifies that the Chair is still assigned and active and the meeting is still InProgress. (BR-130, BR-132)
```
- **Normal Flow, bước 7.** Thay `Existing Meeting Session records remain unchanged. (BR-135, BR-143)` bằng:
```text
(BR-143)
```
- **Exception Flows (dòng 595):**
  1. Xoá trọn khối EX-1, từ `EX-1 – Unfinished Meeting Sessions Remain` đến hết `4. The use case ends.` đầu tiên.
  2. Đổi tiêu đề `EX-2 – Meeting Cannot Be Ended` thành `EX-1 – Meeting Cannot Be Ended`.
  3. Đổi tiêu đề `EX-3 – End Cannot Be Completed` thành `EX-2 – End Cannot Be Completed`.
- **Lưu ý:** sequence diagram nào dẫn chiếu "UC-44 EX-2/EX-3" thì phải đổi theo. Việc này ngoài
  phạm vi tài liệu này.
- **[CHỜ PO — Q7]** Chưa có câu trả lời cho trường hợp còn phiếu đang mở hoặc agenda item đang
  `InProgress` khi kết thúc họp. Văn bản trên không thêm điều kiện nào.

### R3-20 → R3-26 [BẮT BUỘC] §II.6 Agenda Management (UC-31 → UC-36, UC-54)

**Quyết định áp dụng cho cả mục này:**

- "Cửa sổ soạn agenda" là khi meeting ở trạng thái **Draft, Rejected, Approved hoặc Published**, tức
  là trước khi Chair bấm Start. Không cho soạn khi `WaitingApproval`, `InProgress` hoặc `Ended`.
- Các thao tác điều khiển agenda trong lúc họp chạy khi **meeting InProgress**.
- Hai điểm trên chờ PO xác nhận ở Q6.

**Thay ở mọi chỗ trong UC-31 → UC-34:** đổi câu thông báo
`"The agenda for this meeting session can no longer be modified."` thành:
```text
"The agenda for this meeting can no longer be modified."
```
Câu này xuất hiện 4 lần, ở dòng 676, 687, 698 và 709.

#### R3-20 UC-31 Create Agenda Item (dòng 671–676)
- **Description (671).** Thay toàn bộ ô bằng:
```text
Allows the Secretary to add a planned agenda item to a meeting so that the agenda can be prepared before the meeting starts.
```
- **Preconditions (672).** Thay `● PRE-2: The selected Meeting Session exists and belongs to the selected meeting.<br>● PRE-3: The selected Meeting Session is in Planned status.` bằng:
```text
● PRE-2: The selected meeting exists.<br>● PRE-3: The meeting is in Draft, Rejected, Approved, or Published status. (BR-65, BR-74)
```
- **Postconditions (673).**
  - Thay `associated with the selected Meeting Session.` bằng `associated with the selected meeting.`
  - Thay `at the end of the current agenda order for that Meeting Session.` bằng `at the end of the current agenda order of the meeting.`
- **Normal Flow (674).**
  - Thay `1. The Secretary opens the agenda of a selected Meeting Session.` bằng `1. The Secretary opens the agenda of the selected meeting.`
  - Thay `associates it with the selected Meeting Session, and places it at the end of the session agenda.` bằng `associates it with the selected meeting, and places it at the end of the meeting agenda.`
- **Exception Flows (676), EX-2 bước 1.** Thay `the Meeting Session is no longer Planned or the Secretary is no longer assigned to the meeting.` bằng:
```text
the meeting is no longer in Draft, Rejected, Approved, or Published status or the Secretary is no longer assigned to the meeting.
```

#### R3-21 UC-32 Update Agenda Item (dòng 682–687)
- **Description (682).** Thay `while its Meeting Session is still being prepared.` bằng `while the meeting has not yet started.`
- **Preconditions (683).** Thay `● PRE-2: The selected Agenda Item exists and belongs to a Meeting Session of the selected meeting.<br>● PRE-3: The parent Meeting Session is in Planned status.` bằng:
```text
● PRE-2: The selected Agenda Item exists and belongs to the selected meeting.<br>● PRE-3: The meeting is in Draft, Rejected, Approved, or Published status. (BR-65, BR-74)
```
- **Postconditions (684).** Thay `● POST-3: The Agenda Item remains associated with the same Meeting Session.` bằng `● POST-3: The Agenda Item remains associated with the same meeting.`
- **Normal Flow (685).** Thay `1. The Secretary opens the agenda of a planned Meeting Session.` bằng `1. The Secretary opens the agenda of a meeting that has not yet started.`
- **Exception Flows (687), EX-2 bước 1.** Thay `the Meeting Session is no longer Planned, the Agenda Item is no longer Pending,` bằng:
```text
the meeting is no longer in Draft, Rejected, Approved, or Published status, the Agenda Item is no longer Pending,
```

#### R3-22 UC-33 Delete Agenda Item (dòng 693–698)
- **Description (693).** Thay `while its Meeting Session is still being prepared.` bằng `while the meeting has not yet started.`
- **Preconditions (694).** Làm y hệt R3-21: thay PRE-2 và PRE-3 bằng đúng khối văn bản mới của R3-21.
- **Postconditions (695).**
  - Thay `● POST-1: The selected Agenda Item is removed from the Meeting Session agenda.` bằng `● POST-1: The selected Agenda Item is removed from the meeting agenda.`
  - Xoá dòng `<br>● POST-3: Other Meeting Sessions and their agendas remain unchanged.`
- **Normal Flow (696).** Thay `1. The Secretary opens the agenda of a planned Meeting Session.` bằng `1. The Secretary opens the agenda of a meeting that has not yet started.`
- **Exception Flows (698), EX-1 bước 1.** Thay `the Meeting Session is no longer Planned, the Agenda Item is no longer Pending,` bằng:
```text
the meeting is no longer in Draft, Rejected, Approved, or Published status, the Agenda Item is no longer Pending,
```

#### R3-23 UC-34 Reorder Agenda Items (dòng 704–709)
- **Description (704).** Thay `within a planned Meeting Session` bằng `of a meeting that has not yet started`.
- **Preconditions (705).** Thay `● PRE-2: The selected Meeting Session exists and is in Planned status.<br>● PRE-3: The Meeting Session contains Agenda Items that can be reordered.` bằng:
```text
● PRE-2: The selected meeting exists and is in Draft, Rejected, Approved, or Published status. (BR-65, BR-74)<br>● PRE-3: The meeting contains Agenda Items that can be reordered.
```
- **Postconditions (706).** Thay toàn bộ ô bằng:
```text
● POST-1: The Agenda Items of the selected meeting are stored in the new order.<br>● POST-2: No Agenda Item content, status, or meeting assignment is changed.
```
- **Normal Flow (707).**
  - Thay `1. The Secretary opens the agenda of a planned Meeting Session.` bằng `1. The Secretary opens the agenda of a meeting that has not yet started.`
  - Thay `4. The system verifies that the Meeting Session is still open for agenda planning and that all reordered Agenda Items belong to the same Meeting Session.` bằng `4. The system verifies that the meeting is still open for agenda planning and that all reordered Agenda Items belong to the same meeting.`
- **Exception Flows (709), EX-1 bước 1.** Thay `the Meeting Session is no longer Planned or the Secretary` bằng:
```text
the meeting is no longer in Draft, Rejected, Approved, or Published status or the Secretary
```

#### R3-24 UC-35 Set Current Agenda Item (dòng 715–720)
- **Description (715).** Thay `during an active Meeting Session` bằng `while the meeting is InProgress`.
- **Preconditions (716).** Thay toàn bộ ô bằng:
```text
● PRE-1: The Chair is authenticated and assigned to the selected meeting.<br>● PRE-2: The selected meeting is InProgress.<br>● PRE-3: The selected Agenda Item belongs to that meeting and is in Pending status.<br>● PRE-4: No other Agenda Item of the same meeting is currently InProgress.
```
- **Postconditions (717).** Thay `becomes the current Agenda Item of the Meeting Session.` bằng `becomes the current Agenda Item of the meeting.`
- **Normal Flow (718).**
  - Thay `1. The Chair opens the agenda of the active Meeting Session.` bằng `1. The Chair opens the agenda of the InProgress meeting.`
  - Thay `the Meeting Session is InProgress, the selected Agenda Item belongs to that Meeting Session and is Pending, and no other Agenda Item of the same session is InProgress.` bằng `the meeting is InProgress, the selected Agenda Item belongs to that meeting and is Pending, and no other Agenda Item of the same meeting is InProgress.`
  - Thay `authorized users viewing the same Meeting Session.` bằng `authorized users viewing the same meeting.`
- **Exception Flows (720).**
  - Thay `another Agenda Item of the same Meeting Session is already InProgress.` bằng `another Agenda Item of the same meeting is already InProgress.`
  - Thay `the selected Agenda Item does not belong to the Meeting Session, the Meeting Session is no longer InProgress,` bằng `the selected Agenda Item does not belong to the meeting, the meeting is no longer InProgress,`

#### R3-25 UC-36 Complete Agenda Item (dòng 726–731)
- Ô Description (726) **không sửa**.
- **Preconditions (727).** Thay toàn bộ ô bằng:
```text
● PRE-1: The Chair is authenticated and assigned to the selected meeting.<br>● PRE-2: The selected meeting is InProgress.<br>● PRE-3: The selected Agenda Item belongs to that meeting and is currently InProgress.
```
- **Postconditions (728).** Thay `● POST-3: The Meeting Session has no current Agenda Item until the Chair starts another Agenda Item.` bằng:
```text
● POST-3: The meeting has no current Agenda Item until the Chair starts another Agenda Item.
```
- **Normal Flow (729).**
  - Thay `1. The Chair selects the current Agenda Item of the active Meeting Session.` bằng `1. The Chair selects the current Agenda Item of the InProgress meeting.`
  - Thay `the Meeting Session is InProgress, and the selected Agenda Item belongs to that Meeting Session and is currently InProgress.` bằng `the meeting is InProgress, and the selected Agenda Item belongs to that meeting and is currently InProgress.`
  - Thay `authorized users viewing the same Meeting Session.` bằng `authorized users viewing the same meeting.`
- **Exception Flows (731).** Thay `the selected Agenda Item does not belong to the Meeting Session, the Meeting Session is no longer InProgress,` bằng:
```text
the selected Agenda Item does not belong to the meeting, the meeting is no longer InProgress,
```

#### R3-26 UC-54 View Meeting Agenda (dòng 737–742)
- **Description (737).** Thay toàn bộ ô bằng:
```text
Allows an authorized user to view the Agenda Items of a selected meeting. This use case extends UC-17 View Meeting Details (<<extend>>): the agenda is loaded only when the user opens it from the meeting details.
```
- **Preconditions (738).** Xoá dòng `<br>● PRE-3: A Meeting Session belonging to the selected meeting has been selected.`
- **Postconditions (739).** Thay toàn bộ ô bằng:
```text
● POST-1: The agenda of the selected meeting is displayed, or an empty agenda is shown when no Agenda Items exist.<br>● POST-2: No meeting or Agenda Item information is modified.
```
- **Normal Flow (740).**
  - Thay `1. The user selects a Meeting Session and requests to view its agenda.` bằng `1. While viewing the meeting details (UC-17), the user requests to view the meeting agenda.`
  - Thay `2. The system verifies that the user may view the parent meeting under BR-29 and that the selected Meeting Session belongs to that meeting.` bằng `2. The system verifies that the user may view the meeting under BR-29.`
  - Thay `3. The system retrieves the Agenda Items belonging to the selected Meeting Session. (BR-64)` bằng `3. The system retrieves the Agenda Items belonging to the selected meeting. (BR-64)`
- **Alternative Flows (741).** Thay `1. The selected Meeting Session has no Agenda Items.` bằng `1. The selected meeting has no Agenda Items.`
- **Exception Flows (742).** Thay `EX-1 – Meeting Session Is Not Accessible<br>1. At Step 2, the parent meeting is not accessible or the selected Meeting Session does not belong to that meeting.` bằng:
```text
EX-1 – Meeting Is Not Accessible<br>1. At Step 2, the meeting does not exist or is not accessible to the user.
```

### R3-27 [BẮT BUỘC] §II.9 Online Meeting Room (UC-45 → UC-52) — bỏ "Meeting Session"

| Dòng | UC / ô | Thay cụm (nguyên văn) | Bằng (nguyên văn) |
|---|---|---|---|
| 856 | UC-45 Description | `The room belongs to the Meeting, not to a Meeting Session, so a Meeting Session is not required for room access.` | `The room belongs to the Meeting.` |
| 857 | UC-45 Preconditions | `<br>● PRE-4: No Meeting Session is required. (BR-138)` | *(xoá, không thay gì)* |
| 858 | UC-45 Postconditions | `● POST-3: The Meeting and all Meeting Session lifecycle statuses remain unchanged.` | `● POST-3: The Meeting lifecycle status remains unchanged.` |
| 859 | UC-45 Normal Flow, bước 5 | `5. The Meeting and Meeting Session lifecycles remain unchanged. (BR-141)` | `5. The Meeting lifecycle remains unchanged. (BR-141)` |
| 861 | UC-45 Exception EX-3 bước 2 | `2. The Meeting and Meeting Session lifecycles remain unchanged.` | `2. The Meeting lifecycle remains unchanged.` |
| 867 | UC-46 Description | `without ending the Meeting or any Meeting Session.` | `without ending the Meeting.` |
| 869 | UC-46 Postconditions | `● POST-3: The Meeting and Meeting Session lifecycle statuses remain unchanged.` | `● POST-3: The Meeting lifecycle status remains unchanged.` |
| 870 | UC-46 Normal Flow, bước 4 | `4. The Meeting and Meeting Session lifecycles remain unchanged. (BR-141)` | `4. The Meeting lifecycle remains unchanged. (BR-141)` |
| 880 | UC-47 Postconditions | `● POST-2: No Meeting or Meeting Session lifecycle state is changed.` | `● POST-2: The Meeting lifecycle state is not changed.` |
| 891 | UC-48 Postconditions | `● POST-2: No Meeting or Meeting Session lifecycle state is changed.` | `● POST-2: The Meeting lifecycle state is not changed.` |
| 902 | UC-49 Postconditions | `● POST-3: No Meeting or Meeting Session lifecycle state is changed.` | `● POST-3: The Meeting lifecycle state is not changed.` |
| 903 | UC-49 Normal Flow, bước 7 | `7. Meeting and Meeting Session lifecycles remain unchanged. (BR-141)` | `7. The Meeting lifecycle remains unchanged. (BR-141)` |
| 913 | UC-50 Postconditions | `● POST-2: The Meeting participant roster and Meeting Session data are not modified.` | `● POST-2: The Meeting participant roster is not modified.` |
| 924 | UC-51 Postconditions | `● POST-3: The Meeting and Meeting Session lifecycles remain unchanged.` | `● POST-3: The Meeting lifecycle remains unchanged.` |
| 925 | UC-51 Normal Flow, bước 6 | `6. The Meeting and Meeting Session lifecycles remain unchanged. (BR-141)` | `6. The Meeting lifecycle remains unchanged. (BR-141)` |
| 935 | UC-52 Postconditions | `● POST-2: The Meeting and Meeting Session lifecycles remain unchanged.` | `● POST-2: The Meeting lifecycle remains unchanged.` |
| 936 | UC-52 Normal Flow, bước 5 | `5. The Meeting and Meeting Session lifecycles remain unchanged. (BR-141)` | `5. The Meeting lifecycle remains unchanged. (BR-141)` |

- Dòng 857 và 859 còn chữ "guest session". **Giữ nguyên** phần đó.

### R3-28 → R3-29 [BẮT BUỘC] §II.12 Voting (dòng 984–1012)

#### R3-28 Đổi tên mục và thêm ghi chú phạm vi
- **Dòng 984.** Đổi tiêu đề `12. FE-12 – Voting & Resolution Management` thành:
```text
12. FE-12 – Voting Management
```
- Ngay dưới tiêu đề này, trước `12.1 UC-70`, **chèn một đoạn thường**:
```text
Scope note (09/10/2026): Votes are held only while the meeting is InProgress. The Chair creates and opens a vote on an agenda item, eligible participants cast their ballots, the Chair closes the vote, the system tallies the result, and the Chair records the conclusion as Passed, Not Passed or Deferred. There are no time-boxed, pre-meeting or post-meeting votes, no quorum rule and no separate Resolution record; the meeting minutes (FE-13) take each vote's result and conclusion.
```

#### R3-29 Đổi tên các tiểu mục
- **Dòng 994.** Đổi `12.3 UC-72 – Open / Publish Vote` thành:
```text
12.3 UC-72 – Open Vote
```
- **Dòng 1010 [CHỜ PO — Q2].** Làm theo phương án đã chọn:
  - **Phương án A:** đổi tiêu đề thành `12.7 UC-76 – Record Vote Conclusion`. Nội dung
    `[Placeholder] …` giữ nguyên.
  - **Phương án B:** đổi tiêu đề thành `12.7 UC-76 – View Resolution (Retired)`. Thay dòng
    `[Placeholder] …` bên dưới bằng:
```text
Retired on 09/10/2026. There is no separate Resolution record: vote results are viewed through UC-74 View Vote Status and the Chair records the conclusion when closing the vote in UC-75 Close Vote. The ID is kept and never reused.
```
- Các tiểu mục 12.1, 12.2, 12.4, 12.5, 12.6 không đổi tên. Nội dung placeholder giữ nguyên.

### R3-30 → R3-36 [BẮT BUỘC] §II.5 FE-05 Meeting Session Management — retire toàn mục (dòng 597–663)

- **R3-30 Dòng 597.** Đổi tiêu đề `5. FE-05 – Meeting Session Management` thành:
```text
5. FE-05 – Meeting Session Management (Retired)
```
  Ngay dưới tiêu đề, chèn một đoạn thường:
```text
Retired on 09/10/2026 by Product Owner decision. A meeting now represents a single sitting and carries its own planned schedule, lifecycle and agenda, so the separate Meeting Session level has been removed. The use case IDs below are kept for traceability and are never reused.
```
- **R3-31 → R3-36.** Với **mỗi** tiểu mục 5.1 → 5.6:
  1. Đổi tiêu đề bằng cách thêm ` (Retired)` vào cuối.
  2. **Xoá toàn bộ bảng đặc tả** của tiểu mục đó (bảng 7 hàng Primary Actors … Exception Flows).
  3. Thay bảng bằng đúng một đoạn văn như sau:

| ID | Dòng tiêu đề | Tiêu đề mới | Đoạn văn thay cho bảng (dòng bảng cũ) |
|---|---|---|---|
| R3-31 | 599 | `5.1 UC-26 – Create Meeting Session (Retired)` | (601–608) `Retired. Merged into UC-15 Create Meeting: the planned schedule is entered when the meeting is created.` |
| R3-32 | 610 | `5.2 UC-27 – Update Meeting Session (Retired)` | (612–619) `Retired. Merged into UC-16 Update Meeting; after publication the Meeting Organizer reschedules the meeting through UC-16 AF-1 Reschedule a Published Meeting.` |
| R3-33 | 621 | `5.3 UC-28 – Cancel Meeting Session (Retired)` | (623–630) `Retired. There is no Meeting Session to cancel.` |
| R3-34 | 632 | `5.4 UC-29 – Start Meeting Session (Retired)` | (634–641) `Retired. Duplicate of UC-43 Start Meeting.` |
| R3-35 | 643 | `5.5 UC-30 – End Meeting Session (Retired)` | (645–652) `Retired. Duplicate of UC-44 End Meeting.` |
| R3-36 | 654 | `5.6 UC-53 – View Meeting Sessions (Retired)` | (656–663) `Retired. The planned schedule of the meeting is shown in UC-17 View Meeting Details.` |

- **Không** đánh số lại các mục 6 → 16 phía sau.
- **Phương án thay thế (nếu nhóm muốn Report gọn hơn):** xoá hẳn mục 5. Khi đó mục 6 → 16 vẫn
  **giữ số cũ** để khớp FE-06 → FE-16, và việc xoá được ghi trong Change Log. Đề xuất vẫn là giữ
  mục 5 với ghi chú Retired như trên.

### R3-37 → R3-69 [BẮT BUỘC] §V.1 Business Rules (dòng 1331–1489)

Thay ô `Rule Statement` và ô `Applies To` của từng hàng như bảng dưới. Hai cột Rationale và
Enforced By không đổi.

| ID | Dòng | BR | Hành động | Rule Statement mới (nguyên văn) | Applies To mới |
|---|---|---|---|---|---|
| R3-37 | 1357 | BR-25 | Viết lại | `Planned Start Time and Planned End Time are meeting-level schedule fields. A meeting represents a single sitting and has exactly one planned schedule.` | `UC-15, UC-16, UC-17, UC-18, UC-19` |
| R3-38 | 1359 | BR-27 | Viết lại | `Meeting information may be edited only while the meeting is in Draft or Rejected status. While the meeting is Published, only its schedule may be changed, through rescheduling (BR-158).` | `UC-16` *(giữ)* |
| R3-39 | 1361 | BR-29 | Sửa Applies To | *(giữ nguyên Rule Statement)* | `UC-17, UC-25, UC-54` |
| R3-40 | 1362 | BR-30 | Viết lại | `The meeting list may be searched by Meeting Name and filtered by Meeting Status, Confidentiality Level, and Time Range. A meeting matches a Time Range when its planned schedule, from Planned Start Time to Planned End Time, overlaps the selected range.` | `UC-18` *(giữ)* |
| R3-41 | 1365 | BR-33 | Viết lại | `A meeting may be submitted or resubmitted for approval only when all mandatory meeting information, including Planned Start Time and Planned End Time, is complete and the meeting is in Draft or Rejected status.` | `UC-19` *(giữ)* |
| R3-42 | 1386 | BR-54 | Retire | `[Retired 09/10/2026] Meeting Session planning rights and status window. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | `—` |
| R3-43 | 1387 | BR-55 | Retire | `[Retired 09/10/2026] A meeting did not need a Meeting Session to be submitted for approval. Meeting Sessions were removed; the schedule is now mandatory meeting information (BR-33). This BR-ID is not reused.` | `—` |
| R3-44 | 1388 | BR-56 | Retire | `[Retired 09/10/2026] Meeting Sessions were optional for publication. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | `—` |
| R3-45 | 1389 | BR-57 | Retire | `[Retired 09/10/2026] Meeting Sessions were cancelled rather than deleted. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | `—` |
| R3-46 | 1390 | BR-58 | Retire | `[Retired 09/10/2026] Meeting Session lifecycle Planned → InProgress → Completed, or Planned → Cancelled. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | `—` |
| R3-47 | 1391 | BR-59 | Retire | `[Retired 09/10/2026] Only one Meeting Session could be InProgress at a time. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | `—` |
| R3-48 | 1392 | BR-60 | Retire | `[Retired 09/10/2026] A Meeting Session could start only while its parent Meeting was InProgress. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | `—` |
| R3-49 | 1393 | BR-61 | Retire | `[Retired 09/10/2026] Closing the last Meeting Session did not end the parent Meeting. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | `—` |
| R3-50 | 1394 | BR-62 | Viết lại | `A meeting's Planned End Time must be later than its Planned Start Time.` | `UC-15, UC-16` |
| R3-51 | 1395 | BR-63 | Retire | `[Retired 09/10/2026] Only the Chair could start or end a Meeting Session. Meeting Sessions were removed; starting and ending the meeting is governed by BR-130. This BR-ID is not reused.` | `—` |
| R3-52 | 1396 | BR-64 | Viết lại | `Each Agenda Item belongs to exactly one meeting. Reordering changes the order only within that meeting.` | `UC-31, UC-34, UC-35, UC-36, UC-54` *(giữ)* |
| R3-53 | 1397 | BR-65 | Viết lại | `Only the Secretary assigned to the meeting may create, update, delete, or reorder its Agenda Items, and only while the meeting is in Draft, Rejected, Approved, or Published status.` | `UC-31, UC-32, UC-33, UC-34` *(giữ)* |
| R3-54 | 1399 | BR-67 | Viết lại | `A newly created Agenda Item starts in Pending status and is assigned an explicit order within its meeting. The Secretary may change that order while agenda planning is open (BR-65).` | `UC-31, UC-34` *(giữ)* |
| R3-55 | 1400 | BR-68 | Viết lại | `After a meeting becomes InProgress, its agenda structure cannot be created, updated, deleted, or reordered. Only agenda execution state may change while the meeting is InProgress.` | `UC-31, UC-32, UC-33, UC-34` *(giữ)* |
| R3-56 | 1401 | BR-69 | Viết lại | `Only the Chair assigned to the meeting may set or complete the current Agenda Item, and only while the meeting is InProgress.` | `UC-35, UC-36` *(giữ)* |
| R3-57 | 1403 | BR-71 | Viết lại | `Only one Agenda Item of the same meeting may be InProgress at a time. The current Agenda Item must be completed before another Agenda Item can be set as current.` | `UC-35` *(giữ)* |
| R3-58 | 1404 | BR-72 | Viết lại | `Setting an Agenda Item as current records its actual start time. Completing it records its actual end time and actual time used. Completing an Agenda Item does not automatically end the meeting.` | `UC-35, UC-36` *(giữ)* |
| R3-59 | 1405 | BR-73 | Viết lại | `Changes to the current or completed Agenda Item must be reflected in real time for authorized users viewing the same meeting.` | `UC-35, UC-36` *(giữ)* |
| R3-60 | 1406 | BR-74 | Viết lại | `Agenda planning is available while the meeting is in Draft, Rejected, Approved, or Published status and ends when the Chair starts the meeting. Agenda planning is not available while the meeting is WaitingApproval, InProgress, or Ended.` | `UC-31, UC-32, UC-33, UC-34` *(giữ)* |
| R3-61 | 1407 | BR-75 | Viết lại | `When a Published meeting is rescheduled, the new Planned Start Time must be later than the current time. This prevents retroactive scheduling after publication.` | `UC-16` |
| R3-62 | 1408 | BR-76 | Viết lại | `Rescheduling a Published meeting must notify all current internal participants and active external Guest invitees of the new schedule. A notification delivery failure does not undo the reschedule.` | `UC-16` |
| R3-63 | 1409 | BR-77 | Retire | `[Retired 09/10/2026] A Published meeting could have zero Planned Meeting Sessions. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | `—` |
| R3-64 | 1419 + 1461 | BR-87, BR-129 | Sửa Applies To | *(giữ nguyên Rule Statement của cả hai)* | BR-87: `UC-21, UC-43`; BR-129: `UC-21, UC-43` |
| R3-65 | 1460 | BR-128 | Retire | `[Retired 09/10/2026] Starting a Meeting Session required an active Chair and Secretary. Meeting Sessions were removed; starting the meeting is governed by BR-127. This BR-ID is not reused.` | `—` |
| R3-66 | 1465–1468 | BR-133, BR-134, BR-135, BR-136 | Retire (4 hàng) | BR-133: `[Retired 09/10/2026] A Meeting could start with zero Meeting Sessions. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.`<br>BR-134: `[Retired 09/10/2026] A Meeting could end only when no Meeting Session remained Planned or InProgress. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.`<br>BR-135: `[Retired 09/10/2026] Changing the Meeting lifecycle did not change any Meeting Session status. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.`<br>BR-136: `[Retired 09/10/2026] Changing a Meeting Session lifecycle did not change the parent Meeting status. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | `—` (cả 4) |
| R3-67 | 1469 + 1470 | BR-137, BR-138 | BR-137 viết lại; BR-138 retire | BR-137: `The Online Meeting Room belongs to the Meeting.`<br>BR-138: `[Retired 09/10/2026] A Meeting Session was not required to use the Online Meeting Room. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | BR-137: *(giữ)*; BR-138: `—` |
| R3-68 | 1473 + 1474 | BR-141, BR-142 | BR-141 viết lại; BR-142 retire | BR-141: `Joining, leaving, microphone/camera control, screen sharing, room moderation, and in-room chat do not change the Meeting lifecycle.`<br>BR-142: `[Retired 09/10/2026] The same Online Meeting Room remained available across multiple Meeting Sessions. Meeting Sessions were removed; a meeting is a single sitting. This BR-ID is not reused.` | BR-141: *(giữ)*; BR-142: `—` |
| R3-69 | sau 1489 | **BR-158 (mới)** | Thêm hàng mới ở cuối bảng, sau BR-157 | `A Published meeting may be rescheduled only by its Meeting Organizer. Rescheduling may change only the Planned Start Time, Planned End Time, and Location / Online Meeting Link, does not change the meeting lifecycle status, and is not allowed once the meeting is InProgress or Ended.` | `UC-16` |

Lưu ý cho bảng BR:

- **BR-58 (dòng 1390) trong bản Markdown có ô Rule Statement trống.** Bản DOCX có thể vẫn có chữ
  hoặc bị lỗi khi sync. Danh mục `docs/01-requirements/06-business-rules.md` ghi BR-58 là "A
  Meeting Session follows the lifecycle Planned → InProgress → Completed…". Khi retire thì ghi theo
  R3-46, không cần quan tâm ô đang trống hay có chữ.
- BR-66 và BR-70 **không sửa** vì không nhắc tới Session.
- BR-38 → BR-45 **không sửa**. Riêng BR-45 có "approaching its start time": nay start time thuộc
  về meeting, vẫn đúng.
- BR-158 là số kế tiếp sau BR-157. Trước khi dùng, kiểm tra không ai đã cấp BR-158 trong
  `docs/01-requirements/06-business-rules.md`.
- **Không có BR nào về biểu quyết** trong Report 3 hiện tại (§12 chỉ có placeholder), nên D2 không
  kéo theo sửa BR.

### R3-70 → R3-75 [BẮT BUỘC] §V.2 System Messages (dòng 1493–1630)

#### R3-70 Retire các mã của Meeting Session
- **Dòng 1564–1575 (MSG48 → MSG59), 1613 (MSG113), 1627 (MSG127).** Ở mỗi hàng, thêm tiền tố
  `[Retired] ` vào **đầu ô Context**. Ví dụ:
  - Trước: `UC-26 – Create Meeting Session / Main Flow`
  - Sau: `[Retired] UC-26 – Create Meeting Session / Main Flow`

  Cột `#`, mã và Content giữ nguyên. Tổng cộng 14 hàng.

#### R3-71 MSG31 — thêm lỗi cho trường lịch họp
- **Dòng 1542 (UC-15) và 1545 (UC-16).** Trong ô Content, ngay sau dòng `- title: …`, chèn hai dòng
  field error mới (mỗi dòng là một dòng mới trong ô):
```text
- plannedStartTime: Planned start time is required. / Planned start time must be in the future when a published meeting is rescheduled.
- plannedEndTime: Planned end time is required. / Planned end time must be after planned start time.
```
- Tên field `plannedStartTime` và `plannedEndTime` là đề xuất. Khi BE làm endpoint thì phải khớp
  với tên field thật.

#### R3-72 MSG33 — dùng chung cho Reschedule
- **Dòng 1548.** Ô Context hiện là `UC-16 – Update Meeting / Exception Flows`. Đổi thành:
```text
UC-16 – Update Meeting / Exception Flows (including AF-1 Reschedule a Published Meeting)
```

#### R3-73 Thêm mã thành công cho Reschedule [CHỜ — Q10]
- Thêm **một hàng mới ở cuối bảng** (sau `| 134 | MSG128 | …`) để không phải đánh số lại cột `#`:
```text
135 | MSG138 | Toast | UC-16 – Update Meeting / Alternative Flows (AF-1 Reschedule a Published Meeting) | Meeting rescheduled successfully. Invitees will be notified.
```
- `MSG138` là "Mã tiếp theo" trong `docs/03-api/00-danh-muc-ma-thong-bao.md` tại thời điểm audit.
  Trước khi áp dụng, đọc lại mục "Mã tiếp theo". Nếu số đã bị dùng thì lấy số kế tiếp và sửa luôn
  R3-13 (AF-1 bước 8) cho khớp.
- Theo AGENTS.md, khi cấp mã mới phải cập nhật danh mục mã **trong cùng commit**. Việc đó ngoài
  phạm vi report nhưng phải làm.

#### R3-74 MSG67 — đổi nội dung
- **Dòng 1583.** Trong ô Content, thay `The agenda for this meeting session can no longer be modified.` bằng:
```text
The agenda for this meeting can no longer be modified.
```

#### R3-75 Ghi chú dưới bảng
- **Dòng 1630.** Thêm vào **cuối** đoạn "Note: Message codes follow…" câu sau (cách câu trước một
  dấu cách):
```text
MSG48–MSG59, MSG113 and MSG127 were retired on 09/10/2026 together with UC-26 to UC-30 and UC-53 and are never reused.
```

### R3-76 Các mục còn lại của Report 3 — KHÔNG SỬA
- **UC-20, UC-37 → UC-42, UC-88:** không nhắc tới Session hay Resolution. UC-41 có "guest
  session", giữ nguyên.
- **UC-38/39/40 (dòng 766, 777, 788):** có trạng thái `MinutesIssued` và `Archived`, không thuộc
  vòng đời D1. Không sửa trong đợt này, xem Q11.
- **§III Functional Requirements, §IV Non-Functional (dòng 1110–1326):** vẫn là template, không
  có chữ nào liên quan.
- **§I.5.3 Background jobs (dòng 319):** "kiểm tra nhắc lịch đến hạn" nay dựa trên Planned Start
  Time của meeting. Câu chữ vẫn đúng.
- **Report 3 không có** "Audio Uploader", "quorum", "time-boxed" hay "deadline" theo nghĩa phiếu.
  D3 đã đáp ứng ở Actors (dòng 151).

**Tổng Report 3:**
- 72 mã thay đổi: R3-01 → R3-76, trừ 4 mục "không sửa" là R3-02, R3-03, R3-11, R3-76.
- Thực tế khoảng 140 thao tác sửa câu hoặc ô, trải trên khoảng 40 UC (gồm cả các hàng trong bảng
  §I.4.2), 39 BR (gồm BR-158 mới) và 19 hàng MSG.
- Trong đó 1 khuyến nghị lớn (R3-04 Business Flow), 2 khuyến nghị nhỏ (R3-08, R3-18 bước notify),
  và 4 điểm [CHỜ PO] (UC-76, Reschedule actor, Online link/lịch bắt buộc, cửa sổ agenda).

---

## 4. `docs/08-reports/README.md`

- **Không cần sửa.** File chỉ mô tả cách sync, không có nội dung nghiệp vụ.
- Hai điều cần biết khi áp dụng:
  - Thư mục `docs/08-reports/source/` chưa có trong repo và cũng chưa nằm trong `.gitignore`.
    **Không commit file DOCX** vào đó trừ khi nhóm đã thống nhất.
  - Câu cuối "Report 3 được giữ nguyên thành một file tổng; các tài liệu UC/BR/MSG riêng tiếp tục
    do agent cập nhật" xác nhận rằng `docs/01-requirements/*` và `docs/03-api/*` phải sửa **riêng**.
    Các file đó không tự sinh từ DOCX.

---

## 5. Những chữ "session" và "deadline" cố ý KHÔNG đụng tới

| File | Dòng | Ngữ cảnh | Lý do giữ |
|---|---|---|---|
| Report 1 | 22 | Change Log 05/10 | Lịch sử |
| Report 1 | 95 | "formal voting sessions" (Fellow) | Mô tả sản phẩm khác |
| Report 1 | 163 | "session expiry" | Phiên đăng nhập |
| Report 2 | 289 | "session expiry" | Phiên đăng nhập |
| Report 2 | 361, 417, 419, 515, 517, 521 | "full-team session", "acceptance session" | Buổi kiểm thử |
| Report 2 | 54, 81, 189, 195 | "deadline(s)" | Hạn dự án hoặc rủi ro |
| Report 3 | 18–26 | Change Log cũ | Lịch sử |
| Report 3 | 160, 343, 405, 415–417, 476, 487, 1350, 1502–1503 | authenticated/user/server-side session | Phiên đăng nhập |
| Report 3 | 797, 799, 857, 859 | "guest session" | Phiên khách |
| Report 3 | 100 | "application deadline" | Template mẫu |

---

## 6. Câu hỏi mở (cần PO hoặc Team Leader trả lời trước khi áp dụng mục [CHỜ])

1. **Q1 — Cancel Meeting:** có thêm UC "Cancel Meeting" không? AI-eCabinet không có UC này. Tài liệu
   này **không** thêm UC đó. Ghi chú retire của UC-28 cố ý không nhắc tới.
2. **Q2 — UC-76:** chọn A (đổi thành "Record Vote Conclusion") hay B (retire, gộp việc ghi kết luận
   vào UC-75)? Ảnh hưởng R3-01, R3-10 và R3-29.
3. **Q3 — Reschedule:** ai được dời lịch, chỉ Meeting Organizer hay cả Chair? Có cho dời lịch khi
   meeting còn `Approved` (đã duyệt, chưa công bố) không? Bản đề xuất giả định chỉ Organizer và chỉ
   khi `Published`. Ảnh hưởng R1-05, R3-05, R3-13 và BR-158.
4. **Q4 — "Online link":**
   - TrustCabinet tự có phòng họp (FE-09) và LI-05 loại trừ Zoom, Meet, Teams, nên một link nhập tay
     dễ mâu thuẫn với LI-05.
   - Phải chốt link là (a) ô text tự do, gộp chung với Location, hay (b) link phòng họp do hệ thống
     sinh ra, khi đó không ai "đổi link" được.
   - Bản đề xuất dùng một trường "Location / Online Meeting Link" theo đúng chữ của D1 và BF-02.
5. **Q5 — Lịch bắt buộc khi tạo:** Planned Start/End bắt buộc ngay khi tạo (theo BF-02 "Create
   Meeting (title, schedule, …)") hay chỉ bắt buộc khi gửi duyệt? Bản đề xuất chọn bắt buộc khi
   tạo. BR-33 vẫn kiểm lại lúc gửi duyệt.
6. **Q6 — Cửa sổ soạn agenda:** đề xuất là `Draft, Rejected, Approved, Published`, không gồm
   `WaitingApproval` (để người duyệt xem đúng bản đang duyệt) và đóng khi Start. Cần xác nhận.
   Agenda có được sửa sau khi Approved mà không phải duyệt lại không? Đề xuất là có, giống quy tắc
   roster BR-80.
7. **Q7 — Kết thúc họp khi còn việc dở:** khi Chair bấm End Meeting mà còn phiếu đang mở hoặc agenda
   item đang `InProgress` thì sao: chặn lại, tự đóng, hay bỏ qua? Hiện chưa có quy tắc. UC-44 cũ chỉ
   chặn theo Session.
8. **Q8 — Đánh số FE:** đề xuất **giữ số**, đánh dấu FE-05 Removed. Lưu ý tiền lệ ngược lại: ngày
   27/09 và 05/10 nhóm từng đánh số lại FE. Đánh số lại sẽ kéo theo sửa FE-06 → FE-16 ở cả 3 report,
   Jira, class diagram và mọi tài liệu khác.
9. **Q9 — Effort Report 2:** WBS 2.5 (6 pds) và 5.3 (6 pds) được giữ nguyên. Lý do: phần công bỏ
   Session được bù bằng việc làm Reschedule và migration bỏ bảng `MeetingSession`. Nếu Team Leader
   muốn ước lượng lại thì phải làm change request theo Report 2 §II.6.1.
10. **Q10 — Mã thông báo cho Reschedule:** cấp mã mới (đề xuất `MSG138`) hay dùng lại `MSG30`
    "Meeting updated successfully."? AGENTS.md quy định "một mã = một tình huống". Dời lịch có báo
    cho người được mời nên đề xuất dùng mã mới. MSG31 và MSG33 dùng lại được vì cùng tình huống.
11. **Q11 — `MinutesIssued` / `Archived`:** UC-38/39/40 (EX "Participant List Cannot Be Modified")
    còn nhắc hai trạng thái này, không thuộc vòng đời D1 (kết thúc ở `Ended`). Có thể là trạng thái
    lỗi thời. Ngoài D1–D3 nhưng nên hỏi BA.
12. **Q12 — Người ghi Change Log:** ghi `KhanhPG` (BA chốt report) hay `HaiTV`?

---

## 7. Việc ngoài phạm vi `docs/08-reports/` nhưng phải đồng bộ (để người điều phối giao việc)

- `docs/01-requirements/06-business-rules.md`: retire và viết lại đúng các BR như R3-37 → R3-69,
  thêm BR-158.
- `docs/01-requirements/05-use-case-specifications.md` và Master UC Inventory: UC-26 → UC-30 và
  UC-53 retire, UC-15/16/17/21/43/44, agenda UC, UC-72, UC-76.
- `docs/03-api/00-danh-muc-ma-thong-bao.md`: đánh dấu MSG48 → MSG59, MSG113, MSG127 là "không còn
  dùng". Thêm mã Reschedule và cập nhật "Mã tiếp theo" trong cùng commit. Sửa chữ của MSG67.
- Sequence diagram `meeting-session-management/`, các sequence UC-44 (EX đã đánh số lại), class
  diagram FE-05, ERD, `docs/02-design/04-luong-nghiep-vu-chinh.md`.
- `frontend/src/pages/scopeData.ts`: AGENTS.md yêu cầu cập nhật khi thay đổi phạm vi.
- Ghi chú thay đổi yêu cầu vào ticket Jira tương ứng (DEVELOPMENT_WORKFLOW §2.1).


---

# PHẦN C — Thiết kế, API, ADR, sơ đồ, README, AGENTS.md và danh sách code để sau

## Audit thay đổi tài liệu thiết kế theo quyết định PO 09/10/2026 (D1 bỏ Meeting Session, D2 biểu quyết chỉ trong họp, D3 Secretary upload audio)

Nhánh khảo sát: `docs/bf-meeting-no-session` (HEAD `be458da`). Audit chỉ đọc; chưa sửa file nào trong repo.

## 0. Quy ước khi áp dụng danh sách này

1. **Số dòng là số dòng hiện tại (trước khi sửa).** Trong mỗi file, sửa **từ dưới lên** (dòng lớn trước) để số dòng phía trên không bị lệch. Nếu số dòng không khớp, tìm theo "Câu hiện tại" (trích ngắn, khớp chính xác).
2. **Pha A / Pha B.**
   - **[A] = sửa ngay** (tài liệu thiết kế/đích, sơ đồ, ADR, danh mục mã dạng "Dành sẵn").
   - **[B] = sửa trong cùng commit với code/migration** bỏ `MeetingSession` (AGENTS.md: thay đổi hợp đồng API phải đi cùng commit code). Các file trong `docs/03-api/` mô tả hợp đồng đã code, nên phần lớn là [B]; riêng banner cảnh báo và danh mục mã là [A].
3. **Không sửa nội dung ADR đã viết.** Chỉ được đổi dòng `Trạng thái` của ADR cũ và dòng tương ứng trong `docs/05-decisions/README.md`; nội dung mới đi vào ADR mới (mục 5).
4. **Không đánh lại số mã MSG, không xoá dòng.** Mã ngừng dùng giữ nguyên dòng, chỉ thêm chú thích *Ngừng dùng*. **Cột "Use case" của các dòng `MSG48`–`MSG77` phải giữ nguyên chữ `UC-xx`** — test `MessageCodeCatalogueTests.Meeting_workflow_preserves_main_code_reservations` đọc cột đó (regex `^\|\s*\`(MSG\d+)\`\s*\|\s*((?:UC-|ADR-)[^|]+)\|`).
5. **Không đánh lại số UC.** UC-26 → UC-30 và UC-53 thuộc dải đóng băng: ghi "không còn dùng", không tái cấp.
6. **Không đụng** `VotingSession` (tên entity), `CallSession`, guest session, login session/refresh token — trừ chỗ ghi rõ bên dưới (D2 chạm vào *trường* `Kind`/`ClosesAt`/`Conclusion` của `VotingSession`, không đổi tên).
7. Sau khi sửa `.txt` sequence diagram: xuất lại PNG bằng `node scripts/export-diagrams.js <đường-dẫn-.txt>` (cần mạng tới sequencediagram.org). Sau khi sửa `.drawio`: mở bằng draw.io, *File → Export as → PNG* (tick "Include a copy of my diagram") và PDF nếu thư mục đang có PDF.
8. Mã/tên dùng chung trong toàn bộ danh sách:
   - Trường mới của Meeting (theo D1): `PlannedStartTime`, `PlannedEndTime`, `ActualStartedAt`, `ActualEndedAt`, `OnlineLink` (JSON: `plannedStartTime`, `plannedEndTime`, `actualStartedAt`, `actualEndedAt`, `onlineLink`).
   - ADR mới: **ADR-0013** `0013-mot-cuoc-hop-la-mot-buoi-hop.md`, **ADR-0014** `0014-bieu-quyet-chi-trong-cuoc-hop.md` (mục 5).
   - Mã MSG mới đề xuất: **`MSG138`** — "Cuộc họp đã công bố chỉ được dời thời gian, địa điểm hoặc link họp" (UC-16 nhánh Reschedule). Mã tiếp theo sau đó: **`MSG139`**.
   - Câu hỏi mở cho PO được đánh số **PO-Q1 … PO-Q6** (mục cuối). Chỗ nào gắn PO-Q thì dùng **giá trị mặc định ghi trong ngoặc** cho tới khi PO trả lời.
9. **Đọc file này ở dạng văn bản thô.** Nhiều ô bảng chứa nguyên một dòng bảng Markdown của file đích (có dấu `|`) hoặc dấu `\`` thoát; khi render, bảng có thể vỡ. Ký hiệu `\|` và `` \` `` trong cột "Câu hiện tại"/"Thay bằng" nghĩa là ký tự `|` và `` ` `` thật trong file đích. Ký hiệu `/` giữa hai đoạn trích nghĩa là xuống dòng.

---

## 1. `docs/02-design/`

### 1.1 `docs/02-design/01-vong-doi-cuoc-hop.md` [A] — 7 thay đổi

| # | Dòng | Câu hiện tại | Hành động | Thay bằng |
|---|---|---|---|---|
| 1 | 11 | `` | `Draft` | Nháp | Thư ký đang soạn, chưa ai khác thấy | `` | Thay cả dòng | `` | `Draft` | Nháp | Người tổ chức đang soạn, Thư ký chuẩn bị thành phần, agenda, tài liệu; chưa ai khác thấy | `` |
| 2 | 15 | `` | `Published` | Đã công bố | Đã gửi thư mời, thành phần tham dự nhìn thấy | `` | Thay cả dòng | `` | `Published` | Đã công bố | Đã gửi thư mời, thành phần tham dự nhìn thấy; Người tổ chức chỉ còn được dời lịch (thời gian, địa điểm, link) | `` |
| 3 | 16 | `Chủ toạ đã bắt đầu phiên họp` | Thay cụm | `Chủ toạ đã bắt đầu cuộc họp; phòng họp trực tuyến mở nếu hình thức Online/Hybrid` |
| 4 | 17 | `Phiên họp đóng, chờ xử lý hậu kỳ (ASR, biên bản)` | Thay cụm | `Chủ toạ đã kết thúc cuộc họp, phòng trực tuyến đóng; chờ xử lý hậu kỳ (audio, transcript, tóm tắt, biên bản)` |
| 5 | 21–27 | khối sơ đồ ```` ``` ```` từ `Draft ──gửi duyệt──>` tới `Archived <──lưu trữ──` | Thay cả khối (giữ hai dòng rào ```` ``` ````) | xem khối 1.1-a bên dưới |
| 6 | 29–30 | `Khi gửi duyệt theo UC-19, … cho tới bước này.` | Giữ nguyên, **chèn sau dòng 30** một dòng trống và mục mới | xem khối 1.1-b |
| 7 | 34–35 | `- **Phân quyền bám theo trạng thái.** Sửa nội dung chỉ được phép ở \`Draft\` và \`Rejected\`. Biểu quyết chỉ mở khi \`InProgress\`. Biên bản chỉ sinh được sau \`Ended\`.` | Thay cả gạch đầu dòng (2 dòng) | `- **Phân quyền bám theo trạng thái.** Sửa nội dung chỉ được phép ở \`Draft\` và \`Rejected\`; ở \`Published\` chỉ được dời lịch. Biểu quyết chỉ tạo và mở khi \`InProgress\`. Biên bản chỉ sinh được sau \`Ended\`.` |

Khối 1.1-a (thay dòng 22–26):

```
Draft ──gửi duyệt──> WaitingApproval ──duyệt──> Approved ──công bố──> Published ──(dời lịch)──┐
                        ^      │                                         │  ^                  │
                        │   từ chối                                  bắt đầu họp └──────────────┘
                        │      v                                         v
                        └─gửi duyệt lại── Rejected                   InProgress
                                                                         │ kết thúc họp
        Archived <──lưu trữ── MinutesIssued <──ban hành biên bản──── Ended <┘
```

Khối 1.1-b (chèn sau dòng 30):

```markdown

## Lịch họp

Một cuộc họp là **một buổi họp** — không có Meeting Session bên trong
([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)). Lịch nằm trên chính cuộc họp:

| Trường | Ý nghĩa |
|---|---|
| `PlannedStartTime`, `PlannedEndTime` | Lịch dự kiến, nhập khi tạo (UC-15); `PlannedEndTime` phải sau `PlannedStartTime` |
| `ActualStartedAt`, `ActualEndedAt` | Hệ thống ghi khi Chủ toạ bắt đầu (UC-43) và kết thúc (UC-44) |
| `Mode`, `Location`, `OnlineLink` | Hình thức, địa điểm, link họp |

Sau `Published`, Người tổ chức chỉ được dời `PlannedStartTime`, `PlannedEndTime`, `Location`,
`OnlineLink` (UC-16, nhánh Reschedule); hệ thống báo lại cho người được mời.
```

### 1.2 `docs/02-design/02-erd.md` [A] — 34 thay đổi

Ghi chú: file mô tả **mô hình đích**, nên cập nhật ngay; bảng "Trạng thái triển khai" ghi lại việc code cần migration.

| # | Dòng | Câu hiện tại | Hành động | Thay bằng |
|---|---|---|---|---|
| 1 | 3 | `Mô hình đích có đúng **30 thực thể nghiệp vụ/dữ liệu**.` | Thay cụm | `Mô hình đích có đúng **29 thực thể nghiệp vụ/dữ liệu**.` |
| 2 | 13 | `Lịch dự kiến chỉ thuộc MeetingSession.` | Thay câu | `Một Meeting là một buổi họp và tự mang lịch dự kiến; không có MeetingSession ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)). Biểu quyết chỉ diễn ra khi Meeting \`InProgress\` ([ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md)).` |
| 3 | 24 | `` | Meeting | Hồ sơ và vòng đời cuộc họp | IMPLEMENTED | Không lưu lịch dự kiến; lịch thuộc MeetingSession. | `` | Thay cả dòng | `` | Meeting | Hồ sơ, lịch và vòng đời cuộc họp (một cuộc họp = một buổi họp) | NEED MIGRATION / RESTRUCTURE | Thêm \`PlannedStartTime\`, \`PlannedEndTime\`, \`ActualStartedAt\`, \`ActualEndedAt\`, \`OnlineLink\`; bỏ navigation \`Sessions\` (ADR-0013). | `` |
| 4 | 28 | `` | MeetingSession | Lịch và vòng đời của phiên họp | IMPLEMENTED | … SCRUM-68. | `` | **Xoá cả dòng** | — |
| 5 | 29 | `` | AgendaItem | Mục nghị sự của phiên họp | NEED MIGRATION / RESTRUCTURE | Đổi MeetingId thành MeetingSessionId; trạng thái đích Pending/InProgress/Done. | `` | Thay cả dòng | `` | AgendaItem | Mục nghị sự của cuộc họp | NEED MIGRATION / RESTRUCTURE | Giữ MeetingId (không đổi sang MeetingSessionId); trạng thái đích Pending/InProgress/Done. | `` |
| 6 | 37 | `` | VotingSession | Phiên biểu quyết | IMPLEMENTED | Giữ; AgendaItem liên kết tùy chọn. | `` | Thay cả dòng | `` | VotingSession | Một lần biểu quyết trong cuộc họp | NEED MIGRATION / RESTRUCTURE | Bỏ \`Kind\`, \`OpensAt\`, \`ClosesAt\` (không còn biểu quyết trước/sau họp); \`Conclusion\` thành Passed/NotPassed/Deferred; AgendaItem liên kết tùy chọn (ADR-0014). | `` |
| 7 | 49 | `phòng gắn với Meeting, không gắn với MeetingSession.` | Thay cụm | `phòng gắn với Meeting.` |
| 8 | sau 50 | (dòng trống sau bảng) | **Chèn** một đoạn sau dòng 50 | `\n\`MeetingSession\` (bảng \`meeting_sessions\`) đang có trong code nhưng **đã bị bỏ khỏi mô hình đích** từ 09/10/2026 (ADR-0013). Migration mới chép lịch về \`meetings\` rồi drop bảng; không sửa migration cũ đã lên main.` |
| 9 | 61 | `` | Cuộc họp, thành phần và lịch | Meeting, MeetingStatusHistory, MeetingParticipant, MeetingGuestInvitation, MeetingSession, AgendaItem | `` | Thay cả dòng | `` | Cuộc họp, thành phần và nghị sự | Meeting, MeetingStatusHistory, MeetingParticipant, MeetingGuestInvitation, AgendaItem | `` |
| 10 | 68 | `Các nhóm trên gồm 30 thực thể;` | Thay cụm | `Các nhóm trên gồm 29 thực thể;` |
| 11 | sau 107 | `        string Location` | **Chèn sau dòng 107** 5 dòng (thụt 8 dấu cách như các dòng quanh) | `        string OnlineLink "nullable"`<br>`        timestamptz PlannedStartTime`<br>`        timestamptz PlannedEndTime "sau PlannedStartTime"`<br>`        timestamptz ActualStartedAt "nullable, ghi khi Start Meeting"`<br>`        timestamptz ActualEndedAt "nullable, ghi khi End Meeting"` |
| 12 | 150–151 | `Nhận lời rồi không đến là chuyện bình thường, và số liệu quorum` / `tính trên \`Attendance\`.` | Thay hai dòng | `Nhận lời rồi không đến là chuyện bình thường; điểm danh trong biên bản và báo cáo dùng \`Attendance\`. Không có quorum (ADR-0014).` |
| 13 | 155 | `## 2. Phiên họp, nghị sự và tài liệu` | Thay | `## 2. Nghị sự và tài liệu` |
| 14 | 159–160 | `    Meeting \|\|--o{ MeetingSession : "scheduling"` / `    MeetingSession \|\|--o{ AgendaItem : "containing"` | Thay hai dòng bằng một | `    Meeting \|\|--o{ AgendaItem : "containing"` |
| 15 | 171–181 | khối `    MeetingSession {` … `    }` và dòng trống sau nó | **Xoá** 11 dòng | — |
| 16 | 184 | `        uuid MeetingSessionId FK` | Thay | `        uuid MeetingId FK` |
| 17 | 278 | `        string Kind "Live\|Asynchronous"` | **Xoá dòng** | — |
| 18 | 281 | `        timestamptz ClosesAt` | **Xoá dòng** | — |
| 19 | 283 | `        string Conclusion` | Thay | `        string Conclusion "Passed\|NotPassed\|Deferred; null tới khi Chair ghi"` |
| 20 | 398 | `Phòng trực tuyến thuộc Meeting, không thuộc MeetingSession. MeetingSession và CallSession là hai khái niệm độc lập; việc vào/ra phòng không làm thay đổi lifecycle cuộc họp hoặc session.` | Thay cả đoạn | `Phòng trực tuyến thuộc Meeting và chỉ mở khi Meeting \`InProgress\` với hình thức Online/Hybrid. CallSession là một lượt mở phòng, không phải một buổi họp; việc vào/ra phòng không làm thay đổi lifecycle cuộc họp.` |
| 21 | 503 | `` | Meeting | 1 → 0..* | MeetingSession | Lập lịch | `` | **Xoá dòng** | — |
| 22 | 504 | `` | MeetingSession | 1 → 0..* | AgendaItem | Chứa | `` | Thay | `` | Meeting | 1 → 0..* | AgendaItem | Chứa | `` |
| 23 | 541 | `1. Lịch dự kiến chỉ thuộc MeetingSession. MeetingSession là tùy chọn; gửi duyệt, công bố và bắt đầu Meeting không yêu cầu phải có session.` | Thay | `1. Lịch dự kiến thuộc Meeting: \`PlannedStartTime\` và \`PlannedEndTime\` bắt buộc, \`PlannedEndTime > PlannedStartTime\`. Không có MeetingSession; một Meeting là một buổi họp (ADR-0013).` |
| 24 | 542 | `2. Mỗi Meeting có tối đa một MeetingSession ở trạng thái InProgress; …; Completed và Cancelled là trạng thái kết thúc.` | Thay | `2. \`ActualStartedAt\`/\`ActualEndedAt\` do hệ thống ghi khi Start Meeting (Published → InProgress) và End Meeting (InProgress → Ended). Sau \`Published\` chỉ được dời \`PlannedStartTime\`, \`PlannedEndTime\`, \`Location\`, \`OnlineLink\`; mỗi lần dời phải thông báo người được mời.` |
| 25 | 543 | `3. Mỗi AgendaItem thuộc đúng một MeetingSession; chỉ sắp xếp lại trong session đó; mỗi session có tối đa một AgendaItem InProgress. Trạng thái đích: Pending → InProgress → Done.` | Thay | `3. Mỗi AgendaItem thuộc đúng một Meeting; chỉ sắp xếp lại trong Meeting đó; mỗi Meeting có tối đa một AgendaItem InProgress. Trạng thái đích: Pending → InProgress → Done.` |
| 26 | 550 | `Nếu có AgendaItemId, AgendaItem đó phải thuộc một MeetingSession của cùng Meeting.` | Thay cụm | `Nếu có AgendaItemId, AgendaItem đó phải thuộc cùng Meeting.` |
| 27 | 554 | `14. Phòng trực tuyến thuộc Meeting, không thuộc MeetingSession. Vào/rời phòng và điều khiển media không làm đổi vòng đời meeting/session; mỗi Meeting có tối đa một CallSession MeetingRoom đang Active.` | Thay | `14. Phòng trực tuyến thuộc Meeting và chỉ mở khi Meeting \`InProgress\` với hình thức Online/Hybrid. Vào/rời phòng và điều khiển media không làm đổi vòng đời meeting; mỗi Meeting có tối đa một CallSession MeetingRoom đang Active.` |
| 28 | sau 557 | `17. Lời mời Guest …` | **Chèn dòng mới sau 557** | `18. VotingSession chỉ được tạo và mở khi Meeting \`InProgress\`; Chair mở và đóng; hệ thống đếm phiếu; Chair ghi \`Conclusion\` là Passed, NotPassed hoặc Deferred. Không có quorum, không có biểu quyết trước/sau họp, không có entity Resolution (ADR-0014).` |
| 29 | 563 | `` | Meeting trước SCRUM-70 có StartsAt/EndsAt. | Meeting không lưu lịch dự kiến. | Đã backfill Meeting chưa có session và bỏ field/index bằng migration SCRUM-70. | `` | Thay | `` | Meeting trước SCRUM-70 có StartsAt/EndsAt; SCRUM-70 chuyển lịch sang \`meeting_sessions\`. | Meeting mang lịch dự kiến và thời gian thực tế (ADR-0013). | Migration mới: thêm cột vào \`meetings\`, backfill từ \`meeting_sessions\` (giờ bắt đầu sớm nhất và giờ kết thúc muộn nhất trong các session không Cancelled), rồi drop \`meeting_sessions\`. Không sửa migration \`MeetingSessionsForPublication\` / \`MoveMeetingScheduleToSessions\`. | `` |
| 30 | 564 | `` | AgendaItem hiện có MeetingId. | Bắt buộc có AgendaItem.MeetingSessionId. | Thêm session và gán mỗi agenda item vào một session. | `` | Thay | `` | AgendaItem hiện có MeetingId. | Giữ AgendaItem.MeetingId. | Không đổi. | `` |
| 31 | 571 | `Sẽ triển khai/migration sau; hiện chưa phải bảng trong DB. MeetingSession đã có schema.` | Thay cụm | `Sẽ triển khai/migration sau; hiện chưa phải bảng trong DB.` |
| 32 | sau 571 | — | **Chèn dòng mới** vào cuối bảng | `` | VotingSession có \`Kind\` (Live/Asynchronous), \`OpensAt\`, \`ClosesAt\`, \`Conclusion\` dạng chuỗi tự do. | Chỉ biểu quyết trong họp; \`Conclusion\` là Passed/NotPassed/Deferred (ADR-0014). | Migration mới bỏ \`kind\`, \`opens_at\`, \`closes_at\`; đổi \`conclusion\` sang chuỗi enum nullable. | `` |
| 33 | 575 | `- **ADR-0010:** hiện gắn CallSession với MeetingSession; … nhóm cần đối chiếu hoặc thay thế ADR riêng.` | Thay | `- **ADR-0010:** phần gắn CallSession với MeetingSession đã được [ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md) thay thế: phòng ở cấp Meeting, mở khi Meeting \`InProgress\`.` |
| 34 | 577 | `- **Tài liệu vòng đời cuộc họp:** giữ nguyên trục trạng thái; … Cần chỉnh mũi tên nếu sơ đồ hiện tại bị hiểu là không cho phép gửi lại.` | Thay | `- **Tài liệu vòng đời cuộc họp:** đã chỉnh sơ đồ — Rejected gửi duyệt lại trực tiếp sang WaitingApproval; thêm mục Lịch họp.` |

Không sửa: dòng 153, 545 (guest session), 311–319 (phiếu kín, SCRUM-28), 444 ("hủy họp" — giữ cho tới PO-Q1).

### 1.3 `docs/02-design/03-co-so-du-lieu.md` [B] — 2 thay đổi

| # | Dòng | Câu hiện tại | Hành động | Thay bằng |
|---|---|---|---|---|
| 1 | 131 | `` (`meeting_status_history`, `starts_at`) `` | Thay cụm | `` (`meeting_status_history`, `planned_start_time`) `` |
| 2 | 162 | `SELECT title, status, sensitivity, mode, starts_at FROM meetings ORDER BY starts_at;` | Thay | `SELECT title, status, sensitivity, mode, planned_start_time FROM meetings ORDER BY planned_start_time;` |

Ghi chú: câu SQL dòng 162 **đã sai với schema hiện tại** (SCRUM-70 đã drop `starts_at`). Tên cột `planned_start_time` phải khớp tên cột migration mới sinh (`UseSnakeCaseNamingConvention` của `PlannedStartTime`).

### 1.4 `docs/02-design/04-luong-nghiep-vu-chinh.md` [A] — viết lại theo BF-02/03/04 mới

Nguồn đã đọc: `docs/09-bussinessflow/BF-02…drawio`, `BF-03…drawio`, `BF-04…drawio` (trích node/edge). Mục 2 (BF-01, dòng 62–147) **giữ nguyên**. Các mục khác thay theo bảng dưới; nội dung thay thế viết đầy đủ ngay sau bảng.

| # | Dòng | Phạm vi hiện tại | Hành động |
|---|---|---|---|
| 1 | 3 | Câu "Tài liệu mô tả **4 Main Business Flows** và **Shared Voting Sub-flow** …" | Thay bằng khối 1.4-a |
| 2 | 9–58 | `# 1. Tổng quan` tới hết `> **Scope note:** …` | Thay bằng khối 1.4-b |
| 3 | 151–309 | `# 3. BF-02 …` tới hết mục `## 3.8 Optional – Pre-meeting Time-boxed Vote` | Thay bằng khối 1.4-c |
| 4 | 310–367 | `---` + `# 4. Shared Voting Sub-flow` tới `---` trước `# 5.` | **Xoá** (giữ lại đúng một dòng `---` ngăn cách) |
| 5 | 368–493 | `# 5. BF-03 …` tới hết câu `Meeting, Meeting Session và Online Meeting Room là các lifecycle liên quan nhưng độc lập.` | Thay bằng khối 1.4-d |
| 6 | 497–691 | `# 6. BF-04 …` tới hết `> Không thêm state \`MinutesIssued\` …` | Thay bằng khối 1.4-e |
| 7 | 695–775 | `# 7. Quan hệ giữa 4 Main Business Flow` tới hết khối Shared Voting Sub-flow | Thay bằng khối 1.4-f |
| 8 | 779–796 | `# 8. Quy tắc khi chuyển sang Swimlane Diagram` | Đổi tiêu đề thành `# 7. Quy tắc khi chuyển sang Swimlane Diagram`; thay dòng 794–796 bằng khối 1.4-g |
| 9 | 800–810 | `# 9. Các điểm còn cần đồng bộ / xác nhận …` | Thay bằng khối 1.4-h |

Khối 1.4-a (dòng 3):

```markdown
> Tài liệu mô tả **4 Main Business Flows** của TrustCabinet, làm nguồn cho swimlane diagram trong [09-bussinessflow/](../09-bussinessflow/) và Report 3 §I.2. Cập nhật 09/10/2026 theo quyết định PO: một cuộc họp là một buổi họp (không có Meeting Session), biểu quyết chỉ diễn ra trong cuộc họp, Secretary tự upload audio.
```

Khối 1.4-b (dòng 9–58):

````markdown
# 1. Tổng quan

TrustCabinet được mô tả bằng 4 Main Business Flow:

1. **BF-01 – System Access & Administration**
2. **BF-02 – Meeting Preparation, Approval & Participant Coordination**
3. **BF-03 – Meeting Execution & Decision Making**
4. **BF-04 – Post-Meeting Processing & Formal Records**

Quan hệ tổng quát:

```text
BF-01
System Access & Administration
        ↓
Người dùng có account / role / permission phù hợp
        ↓
BF-02
Meeting Preparation, Approval & Participant Coordination
        ↓
Draft → WaitingApproval → Approved / Rejected → Published (→ dời lịch nếu cần)
        ↓
BF-03
Meeting Execution & Decision Making
        ↓
Published → InProgress → Ended
        ↓
BF-04
Post-Meeting Processing & Formal Records
        ↓
Secretary Upload Audio → Transcript → Transcript Review
→ AI Summary → Summary Approval / Publication
→ Minutes → Signing → Finalize / Seal
```

## Mô hình cuộc họp

Một cuộc họp là **một buổi họp**. Cuộc họp tự mang lịch dự kiến (thời gian bắt đầu, kết thúc), hình thức, địa điểm hoặc link họp, vòng đời, agenda, thành phần, tài liệu, biểu quyết, audio / transcript / tóm tắt và biên bản. Không có cấp "Meeting Session" bên trong cuộc họp; chữ "phiên" trên giao diện AI-eCabinet là agenda item. Xem [ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md).

## Biểu quyết

Biểu quyết chỉ diễn ra **trong cuộc họp đang `InProgress`**, trong vòng agenda của BF-03: Chair tạo và mở biểu quyết, người dự bỏ phiếu, Chair đóng, TrustCabinet đếm phiếu và hiển thị kết quả, Chair ghi kết luận **Thông qua / Không thông qua / Hoãn**. Không có biểu quyết trước họp hay sau họp, không có quorum, không có đối tượng "Resolution" bất biến riêng; kết quả và kết luận của từng lần biểu quyết là nguồn cho biên bản. Xem [ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md).

> **Scope note:** Attendance (UC-88) đã được PO xác nhận. Không có UC chỉ định "Audio Uploader": Secretary tự upload audio bằng UC-62.
````

Khối 1.4-c (dòng 151–309):

```markdown
# 3. BF-02 – Meeting Preparation, Approval & Participant Coordination

**Trigger:** Meeting Organizer xác định cần tổ chức một cuộc họp mới.

**End condition:** Meeting đã **Published**, lời mời đã được gửi, người được mời có thể phản hồi; nếu lịch thay đổi sau khi công bố thì lịch mới đã được báo lại cho người được mời.

## Swimlanes

- **Meeting Organizer**
- **Secretary**
- **TrustCabinet**
- **Meeting Approver**
- **Participant / Guest**
- **Email Service**

## 3.1 Tạo cuộc họp

Meeting Organizer tạo cuộc họp với tên, lịch dự kiến (thời gian bắt đầu, kết thúc), hình thức (InPerson / Online / Hybrid), địa điểm hoặc link họp và mức độ mật (UC-15).

TrustCabinet lưu cuộc họp ở trạng thái **Draft** và ghi lifecycle history.

## 3.2 Gán Secretary đầu tiên

Meeting Organizer gán Secretary đầu tiên cho cuộc họp (UC-38, nhánh Organizer). Chỉ sau khi đã có Secretary, Secretary mới chuẩn bị thành phần, vai trò và agenda.

## 3.3 Secretary chuẩn bị cuộc họp

Theo thứ tự trên BF-02:

1. Quản lý thành phần nội bộ và mời Guest bằng tên và email (UC-37, UC-38).
2. Gán vai trò trong cuộc họp — Chair, Member (UC-39).
3. Chuẩn bị Agenda Items (UC-31 → UC-34). Agenda thuộc trực tiếp cuộc họp.
4. Thêm tài liệu cuộc họp (UC-57).

Meeting Organizer có thể sửa thông tin cuộc họp khi cuộc họp ở **Draft** hoặc **Rejected** (UC-16).

> Không có bước tạo / sửa / hủy Meeting Session và không có bước chỉ định Audio Uploader.

## 3.4 Gửi duyệt

1. Meeting Organizer chọn Meeting Approver; **không được chọn chính mình**.
2. Organizer chọn **Submit for Approval** (UC-19).
3. TrustCabinet kiểm tra thông tin bắt buộc, chuyển cuộc họp sang **WaitingApproval**, ghi lifecycle history và thông báo cho Meeting Approver.

## 3.5 Duyệt

Meeting Approver mở và review cuộc họp (UC-20).

**Approve Meeting?**

- **Không:** Approver nhập lý do. TrustCabinet chuyển cuộc họp sang **Rejected**, ghi lifecycle history kèm lý do và thông báo cho Organizer. Organizer sửa cuộc họp (UC-16) rồi gửi duyệt lại — quay về 3.4.
- **Có:** TrustCabinet chuyển cuộc họp sang **Approved**, ghi lifecycle history và thông báo cho Organizer.

Approval **không tự động Publish** cuộc họp.

## 3.6 Công bố

Meeting Organizer chọn **Publish Meeting** (UC-21). TrustCabinet kiểm tra:

**Đã có Chair và Secretary đang hoạt động?**

- **Không:** cuộc họp chưa được công bố; Secretary gán vai trò còn thiếu (UC-38 / UC-39), rồi Organizer Publish lại.
- **Có:** TrustCabinet chuyển cuộc họp sang **Published**, ghi lifecycle history, tạo lời mời / notification; Email Service gửi thư mời.

Publish không có điều kiện nào về lịch phiên — lịch đã nằm trên cuộc họp từ bước 3.1.

## 3.7 Phản hồi lời mời

Participant / Guest mở lời mời; Guest xác minh email trước (UC-87). Người được mời chọn **Accept** hoặc **Decline** (UC-41). TrustCabinet lưu phản hồi mới nhất để Secretary và người có quyền theo dõi.

Guest không có internal account và không nhận system role. Nhánh Delegate của participant nội bộ vẫn theo UC-41; BF-02 không vẽ nhánh này.

## 3.8 Dời lịch sau khi công bố (Reschedule)

**Cần dời lịch?**

- **Có:** Meeting Organizer dời lịch — chỉ được đổi **thời gian, địa điểm, link họp** (UC-16, nhánh Reschedule). TrustCabinet lưu lịch mới và thông báo người được mời; Email Service gửi thư cập nhật lịch. Người được mời có thể phản hồi lại lời mời (quay về 3.7).
- **Không:** kết thúc BF-02.

> PO chưa chốt: Chair có được dời lịch không (hiện chỉ Organizer); có thêm Cancel Meeting hay không. BF-02 chưa có nhánh hủy cuộc họp.
```

Khối 1.4-d (dòng 368–493):

```markdown
# 4. BF-03 – Meeting Execution & Decision Making

**Trigger:** Chair quyết định bắt đầu một cuộc họp đang ở trạng thái **Published**.

**End condition:** Meeting đã **Ended**, phòng trực tuyến đã đóng; điểm danh chính thức, trạng thái và thời gian thực tế của từng agenda item, kết quả và kết luận biểu quyết đã được ghi nhận.

## Swimlanes

- **Participant / Guest**
- **Chair**
- **TrustCabinet**
- **Secretary**

## 4.1 Bắt đầu cuộc họp

Chair chọn **Start Meeting** (UC-43). TrustCabinet kiểm tra Chair và Secretary vẫn hợp lệ, chuyển cuộc họp **Published → InProgress**, ghi lifecycle history và thông báo người tham dự.

## 4.2 Vào phòng họp trực tuyến — nếu Online / Hybrid

**Online / Hybrid Meeting?**

- **Có:** Participant / Guest có quyền vào phòng họp trực tuyến (UC-45) và dùng các chức năng được phép: microphone, camera, chia sẻ màn hình, chat trong phòng, xem người đang kết nối.
- **Không (InPerson):** bỏ qua bước này.

Phòng chỉ mở khi cuộc họp **InProgress**. Vào / rời phòng **không thay đổi lifecycle** của cuộc họp.

## 4.3 Điểm danh chính thức

Secretary ghi nhận **official Attendance** (UC-88):

- Online / Hybrid: TrustCabinet có thể pre-fill từ room presence; Secretary kiểm tra và chỉnh.
- InPerson: Secretary ghi trực tiếp.

Official Attendance độc lập với phản hồi lời mời và với presence trong phòng, và là dữ liệu dùng cho biên bản / báo cáo.

## 4.4 Vòng agenda

1. Chair chọn **Set Current Agenda Item** (UC-35). Mỗi lúc tối đa một agenda item ở InProgress.
2. Participant / Guest thảo luận theo quyền; tài liệu, chat, chia sẻ màn hình có thể diễn ra song song.
3. **Vote Needed?**
   - **Có:**
     1. Chair tạo và mở biểu quyết (UC-71, UC-72 Open Vote).
     2. Participant đủ điều kiện bỏ phiếu (UC-73).
     3. Chair đóng biểu quyết (UC-75).
     4. TrustCabinet đếm phiếu và hiển thị kết quả (UC-74).
     5. Chair ghi kết luận **Thông qua / Không thông qua / Hoãn** (UC-76 nếu PO chốt "Record Vote Conclusion").
   - **Không:** sang bước 4.
4. Chair chọn **Complete Agenda Item** (UC-36). TrustCabinet lưu trạng thái và thời gian thực tế của agenda item.
5. **More Agenda Items?** Có → quay lại bước 1; Không → 4.5.

BF-03 **không giả định Secretary có chức năng note-taking trong hệ thống**. Nội dung thảo luận phục vụ AI được lấy từ audio upload sau cuộc họp (BF-04).

## 4.5 Kết thúc cuộc họp

Chair chọn **End Meeting** (UC-44). TrustCabinet:

- chuyển cuộc họp **InProgress → Ended**;
- ghi lifecycle history;
- đóng phòng họp trực tuyến và ngắt các kết nối còn lại.

End Meeting không kiểm tra "phiên chưa hoàn tất" vì không còn Meeting Session.
```

Khối 1.4-e (dòng 497–691):

```markdown
# 5. BF-04 – Post-Meeting Processing & Formal Records

**Trigger:** Meeting đã **Ended** và cần xử lý hồ sơ sau họp.

**End condition:** Meeting có biên bản chính thức đã ký đủ, đã finalize / seal, và hồ sơ cuộc họp được truy cập theo quyền.

## Swimlanes

- **AI / Speech Processing Provider**
- **TrustCabinet**
- **Secretary**
- **Chair / Designated Signers**
- **Authorized User**

## 5.1 Upload Audio

Sau khi cuộc họp Ended, TrustCabinet thông báo Secretary tải audio cuộc họp lên.

TrustCabinet **không tự ghi âm cuộc họp** và không chuyển giọng nói thời gian thực. Secretary upload file audio đã ghi bằng thiết bị / công cụ bên ngoài (UC-62).

**Audio File Valid?**

- **Không:** TrustCabinet báo lỗi (sai định dạng, vượt dung lượng, không xử lý được); Secretary chọn file hợp lệ và upload lại.
- **Có:** TrustCabinet lưu audio vào Media Library của cuộc họp, áp dụng access control của cuộc họp và đưa file vào hàng đợi xử lý.

## 5.2 Speech Processing

AI / Speech Processing Provider chuyển audio thành văn bản và nhận diện người nói (speaker diarization), tạo transcript có người nói.

**Processing Successful?**

- **Không:** TrustCabinet báo lỗi và cho phép thử xử lý lại.
- **Có:** chuyển sang Transcript Review.

## 5.3 Transcript Review và tạo tóm tắt

Secretary xem và sửa transcript, sửa speaker label (UC-64, UC-65), rồi chọn **Generate Summary** (UC-90). AI / Speech Processing Provider sinh tóm tắt từ transcript đã review. Tóm tắt có thể gồm các điểm thảo luận, quyết định và đề xuất việc cần làm — chỉ là nội dung văn bản.

## 5.4 Review, Approve và Publish Summary

1. Secretary review, chỉnh và **Approve Summary** (UC-67, UC-68).
2. Secretary chủ động **Publish Summary** như một hành động riêng (UC-69).
3. TrustCabinet công bố tóm tắt cho participant có quyền và gửi notification.

Approve và Publish là hai bước riêng.

## 5.5 Generate Draft Minutes

Secretary chọn **Generate Draft Minutes** (UC-78). TrustCabinet tổng hợp biên bản nháp từ:

- thông tin cuộc họp;
- tóm tắt đã duyệt / công bố;
- **kết quả và kết luận biểu quyết** ghi trong cuộc họp.

Không tự kéo thêm nguồn mơ hồ như ghi chú cũ hay "các nội dung liên quan khác".

## 5.6 Submit for Signing

Secretary review và chọn **Submit for Signing** (UC-80). TrustCabinet gửi yêu cầu ký đến Chair / Designated Signers.

**Minutes Acceptable?**

- **Có:** signer ký (UC-81). TrustCabinet lưu signer, thời điểm ký, chữ ký và audit trail.
- **Không:** signer yêu cầu sửa (UC-93). Secretary sửa biên bản; TrustCabinet **vô hiệu hóa mọi chữ ký đã có** rồi gửi lại yêu cầu ký.

## 5.7 Tất cả signer đã ký?

- **Chưa:** TrustCabinet tiếp tục gửi yêu cầu ký cho signer còn lại.
- **Rồi:** sang 5.8.

## 5.8 Finalize / Seal Minutes

Secretary chủ động **Finalize / Seal** (UC-83). TrustCabinet khóa biên bản, giữ chữ ký và audit trail, lưu biên bản như hồ sơ chính thức.

## 5.9 Records / Reporting

Authorized User truy cập các hồ sơ được phép: thông tin cuộc họp, tài liệu, điểm danh, transcript, tóm tắt đã duyệt / công bố, **kết quả và kết luận biểu quyết**, biên bản đã niêm phong.

Dashboard / Reporting là chức năng xuyên suốt, không phải bước bắt buộc ở cuối BF-04. Người có quyền báo cáo có thể xem số cuộc họp đã tổ chức, điểm danh, kết luận biểu quyết, trạng thái biên bản.

> Không đưa task tracking vào BF-04. Đề xuất việc cần làm chỉ là văn bản trong tóm tắt / biên bản, không thành task có người nhận, hạn và trạng thái.

> Không thêm state `MinutesIssued` hoặc `Archived` vào Main Business Flow cho tới khi lifecycle này được baseline chính thức trong SRS.
```

Khối 1.4-f (dòng 695–775; khối "Approval loop của BF-02" dòng 738–755 giữ nguyên chữ, chỉ nằm trong khối mới):

````markdown
# 6. Quan hệ giữa 4 Main Business Flow

```text
BF-01
System Access & Administration
        ↓
User được cấp account / role / permission
        ↓
BF-02
Create Meeting (lịch, hình thức, địa điểm / link)
→ Assign First Secretary
→ Participants & Guests / Meeting Roles / Agenda / Documents
→ Submit
→ Approve / Reject
→ Publish
→ Invitation / Response
→ [Reschedule nếu cần → báo lại người được mời]
        ↓
BF-03
Start Meeting
→ [Join Online Room nếu Online / Hybrid]
→ Record Attendance
→ Agenda loop: Set Current Agenda Item → Discussion
   → [Vote: Create & Open → Cast → Close → Tally → Record Conclusion]
   → Complete Agenda Item
→ End Meeting
        ↓
BF-04
Secretary Upload Audio
→ Speech-to-Text / Speaker Attribution
→ Secretary Reviews Transcript
→ Generate AI Summary
→ Approve Summary
→ Publish Summary
→ Generate Draft Minutes
→ Submit for Signing
→ Sign / Revision Loop (sửa → vô hiệu hóa chữ ký cũ → ký lại)
→ Finalize / Seal
```

## Approval loop của BF-02

```text
Draft
  ↓
Submit for Approval
  ↓
WaitingApproval
  ↓
Review
 ├─ Approve → Approved → Publish → Published
 └─ Reject  → Rejected
                  ↓
             Organizer edits
                  ↓
                Submit
                  └────────→ WaitingApproval
```

## Biểu quyết trong vòng agenda của BF-03

```text
Meeting InProgress, agenda item đang bàn
        ↓
Chair creates & opens Vote
        ↓
Participants cast votes
        ↓
Chair closes Vote
        ↓
TrustCabinet tallies votes & shows result
        ↓
Chair records conclusion: Passed / Not Passed / Deferred
```
````

(Dòng `---` số 777 của file gốc giữ nguyên ngay sau khối này.)

Khối 1.4-g (thay dòng 794–796):

```markdown
3. **Decision point** – Approve?, đủ Chair + Secretary?, cần dời lịch?, Online / Hybrid?, cần biểu quyết?, còn agenda item?, audio hợp lệ?, xử lý thành công?, biên bản chấp nhận được?, signer đã ký đủ?
4. **Business outcome** – Published Meeting, kết luận biểu quyết, Published Summary, Sealed Minutes.
5. **Loop quan trọng** – Reject → Edit → Resubmit; Reschedule → báo lại → phản hồi lại; Agenda loop; Signing revision loop.
```

Khối 1.4-h (dòng 800–810):

```markdown
# 8. Các điểm còn cần đồng bộ / xác nhận trước khi baseline cuối cùng

1. **Cancel Meeting** – PO chưa chốt có thêm hay không; BF-02 chưa có nhánh hủy cuộc họp.
2. **Người được dời lịch** – PO chốt chỉ Organizer hay cả Chair.
3. **UC-76** – PO chốt đổi thành "Record Vote Conclusion" (Chair ghi Thông qua / Không thông qua / Hoãn) hay bỏ hẳn vì xem kết quả đã nằm trong UC-74.
4. **Finalize / Seal actor** – Secretary chủ động thực hiện (UC-83).
5. **Meeting status sau Ended** – chưa thêm `MinutesIssued` / `Archived` cho tới khi SRS baseline lifecycle này.
6. **AI naming** – tài liệu dùng thống nhất **AI / Speech Processing Provider**.
7. **Reporting actor** – không tạo actor "Management"; chỉ dùng Authorized User / user có reporting permission cho tới khi actor canonical được chốt.
```

Đếm: 9 khối thay thế; xoá toàn bộ mục "Shared Voting Sub-flow", "Pre-meeting / Post-meeting Time-boxed Vote", "Meeting Session", "End Meeting Session", "Audio Uploader", "Resolution".

### 1.5 `docs/02-design/08-ke-hoach-phong-hop-truc-tuyen.md` [A] — 7 thay đổi

Đây là tài liệu kế hoạch (không phải ADR) nên sửa trực tiếp.

| # | Dòng | Câu hiện tại | Hành động | Thay bằng |
|---|---|---|---|---|
| 1 | 25–26 | `D1–D6 và D11–D13 được ghi thành ADR-0010; D10 thành ADR-0011. D7–D9 là thay đổi scope, chờ nhóm` / `quyết.` | Thay | `D1–D6 và D11–D13 được ghi thành ADR-0010; D10 thành ADR-0011; ADR-0013 điều chỉnh D5, D6, D10 sau khi PO bỏ Meeting Session (09/10/2026). D7–D9 là thay đổi scope, chờ nhóm quyết.` |
| 2 | 35 | dòng bảng `\| D5 \| Tên phòng LiveKit = …` | Thay cả dòng | `` | D5 | Tên phòng LiveKit = `meeting-{meetingId:D}`, một phòng cho một cuộc họp. Phòng tự tạo khi người đầu tiên vào. `CallSession` (lượt mở phòng — **không** phải cuộc họp nghiệp vụ) được tạo từ webhook, không tạo lúc cấp token. | Đã chốt — ADR-0010, ADR-0013 | `` |
| 3 | 36 | dòng bảng `\| D6 \| Phòng chỉ mở khi cuộc họp **đang có một Meeting Session \`InProgress\`** …` | Thay cả dòng | `` | D6 | Phòng chỉ mở khi cuộc họp `InProgress` **và** `Mode` là `Online` hoặc `Hybrid`. Chỉ người có dòng `meeting_participants` (trừ `Delegated`) được vào. Không còn Meeting Session (ADR-0013), nên `Meeting.Status = InProgress` là điều kiện chính thức, không còn là điều kiện tạm. Dời lịch sau `Published` không tự mở phòng. | Đã chốt — ADR-0010 / ADR-0013 | `` |
| 4 | 40 | `Agenda tách planning context (session \`Planned\`) và live context (session \`InProgress\`). \| Đã chốt — ADR-0011 \|` | Thay cụm | `Agenda tách planning context (cuộc họp chưa \`InProgress\`) và live context (cuộc họp \`InProgress\`). \| Đã chốt — ADR-0011 / ADR-0013 \|` |
| 5 | 553–554 | `// time: sync main and read the current catalogue. MSG65 stays reserved for RoomNotOpen (ADR-0010).` | Thay dòng 554 | `    // time: sync main and read the current catalogue. RoomNotOpen uses MSG117 (UC-45); MSG65 is UC-36.` |
| 6 | 930 | `Giữ \`MSG65\` cho phòng chưa mở theo ADR-0010; báo Quang Huy để tránh cấp trùng giữa các PR.` | Thay | `Phòng chưa mở dùng \`MSG117\` theo catalogue (ADR-0010 ghi \`MSG65\` là lệch — \`MSG65\` là UC-36); báo Quang Huy để tránh cấp trùng giữa các PR.` |
| 7 | 1116 | `- Phụ thuộc: vòng đời phiên họp do các UC \`Create / Update / Cancel / Start / End Meeting Session\` (Report 3) đảm nhiệm. … điều kiện mở phòng tạm là \`Meeting.Status = InProgress\`.` | Thay cả gạch | `- Phụ thuộc: không còn vòng đời Meeting Session (ADR-0013). Phòng mở theo \`Meeting.Status = InProgress\`, do UC-43 Start Meeting và UC-44 End Meeting điều khiển.` |
| 8 | 1136 | `3. ~~Ai làm vòng đời Meeting Session?~~ Report 3 đã chốt … để implementation.` | Thay | `3. ~~Ai làm vòng đời Meeting Session?~~ Không còn áp dụng: PO bỏ Meeting Session ngày 09/10/2026 (ADR-0013); phòng theo vòng đời Meeting.` |

(#5, #6 sửa mâu thuẫn có sẵn: ADR-0010/kế hoạch dành `MSG65` cho phòng chưa mở, nhưng catalogue cấp `MSG65` cho UC-36 Complete Agenda Item và `MSG117` cho UC-45 phòng không khả dụng.)

Không sửa: các dòng nói `call_sessions`, `CallSession`, `EnsureActiveRoomSessionAsync`, `RecordJoinAsync`… (đó là CallSession).

### 1.6 `docs/02-design/README.md` [A] — 1 thay đổi

| Dòng | Câu hiện tại | Thay bằng |
|---|---|---|
| 8 | ``- [`04-luong-nghiep-vu-chinh.md`](04-luong-nghiep-vu-chinh.md) — 4 Main Business Flows và Shared Voting Sub-flow`` | ``- [`04-luong-nghiep-vu-chinh.md`](04-luong-nghiep-vu-chinh.md) — 4 Main Business Flows (swimlane ở [../09-bussinessflow/](../09-bussinessflow/))`` |

---

## 2. `docs/03-api/`

### 2.1 `docs/03-api/00-danh-muc-ma-thong-bao.md` [A] — 19 thay đổi

Quy tắc khi đánh dấu: **chỉ sửa cột thứ ba ("Tình huống")** của bảng "Dành sẵn, chưa có code" bằng cách nối thêm hậu tố; **không đụng cột "Use case"** (test đọc cột đó).

Hậu tố chuẩn **S1** (dùng cho mã chỉ thuộc UC-26…UC-30, UC-53):
` — **Ngừng dùng** (09/10/2026): bỏ Meeting Session, UC-26–30 và UC-53 không còn dùng ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)). Không tái sử dụng mã.`

| # | Dòng | Mã | Hành động |
|---|---|---|---|
| 1 | 168 | `MSG48` (UC-26 Create Meeting Session) | Nối hậu tố S1 sau `*Meeting session created successfully.*` |
| 2 | 169 | `MSG49` (UC-27) | Nối S1 |
| 3 | 170 | `MSG50` (UC-28) | Nối S1 |
| 4 | 171 | `MSG51` (UC-29) | Nối S1 |
| 5 | 172 | `MSG52` (UC-30) | Nối S1 |
| 6 | 173 | `MSG53` (UC-26 / UC-27) | Nối: ` — **Ngừng dùng** (09/10/2026): bỏ Meeting Session; lịch nay kiểm tra trong UC-15/UC-16 bằng \`MSG31\` ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)). Không tái sử dụng mã.` |
| 7 | 174 | `MSG54` (UC-26) | Nối S1 |
| 8 | 175 | `MSG55` (UC-27) | Nối S1 |
| 9 | 176 | `MSG56` (UC-28) | Nối S1 |
| 10 | 177 | `MSG57` (UC-29) | Nối S1 |
| 11 | 178 | `MSG58` (UC-29) | Nối S1 |
| 12 | 179 | `MSG59` (UC-30) | Nối S1 |
| 13 | 187 | `MSG67` (UC-31…34) | **Đổi câu** (cùng tình huống, không đổi mã): `*The agenda for this meeting session can no longer be modified.*` → `*The agenda for this meeting can no longer be modified.*` |
| 14 | 197 | `MSG77` (UC-28) | Nối S1 |
| 15 | 218 | `MSG113` (UC-44) | Nối: ` — **Ngừng dùng** (09/10/2026): End Meeting không còn kiểm tra phiên chưa hoàn tất ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)). Không tái sử dụng mã.` |
| 16 | 232 | `MSG127` (UC-53 View Meeting Sessions) | Nối S1 |
| 17 | sau 233 | (cuối bảng "Dành sẵn") | **Chèn dòng mới**: `` | `MSG138` | UC-16 Update Meeting (Reschedule) | *A published meeting can only be rescheduled: change the planned time, location or online link.* (409) | `` |
| 18 | 30 | đoạn "Mã tiếp theo" bắt đầu `**\`MSG138\`** — workflow SCRUM-19 dùng …` | Thay cả đoạn | xem khối 2.1-a |
| 19 | 235 | `` `MSG83` và `MSG84` đã được workflow SCRUM-19 dùng. `MSG90` và `MSG91` chỉ được giữ để không tái cấp. `` | Nối thêm câu | `` `MSG48`–`MSG59`, `MSG77`, `MSG113` và `MSG127` cũng chỉ được giữ để không tái cấp (bỏ Meeting Session, ADR-0013). `` |

Khối 2.1-a (thay dòng 30):

```markdown
**`MSG139`** — workflow SCRUM-19 dùng `MSG41`–`MSG47`, `MSG78`–`MSG89` và `MSG130`–`MSG137`; `MSG90`–`MSG91` ngừng dùng, không tái cấp. `MSG48`–`MSG59`, `MSG77`, `MSG113` và `MSG127` ngừng dùng từ 09/10/2026 vì bỏ Meeting Session (ADR-0013), không tái cấp. Các mã còn lại trong `MSG60`–`MSG76` và `MSG95`–`MSG128` dành cho Agenda, participant, room và các màn đọc liên quan. `MSG138` dành cho dời lịch cuộc họp đã công bố (UC-16). Đặt lại mật khẩu UC-08 dùng `MSG92`–`MSG94` và `MSG129` (FE).
```

**Danh sách mã chỉ dùng cho UC session → "không còn dùng" (tổng 15):** `MSG48`, `MSG49`, `MSG50`, `MSG51`, `MSG52`, `MSG53`, `MSG54`, `MSG55`, `MSG56`, `MSG57`, `MSG58`, `MSG59`, `MSG77`, `MSG113` (UC-44 nhánh phiên chưa hoàn tất), `MSG127`. `MSG90` đã ngừng dùng từ trước (giữ nguyên). Không có mã biểu quyết nào trong catalogue (UC-70–76 chưa cấp mã) nên D2 không làm ngừng mã nào.

Mã giữ nhưng đổi ngữ cảnh "session → meeting": `MSG67` (câu chữ), `MSG64`–`MSG65`, `MSG72`–`MSG76` (UC-35/36 — câu chữ không nhắc session, không cần sửa), `MSG128` (UC-54 — giữ).

Lưu ý bắt buộc: thêm dòng `MSG138` thì "Mã tiếp theo" phải là `MSG139` (test `The_next_code_follows_the_highest_one_in_use`). Nếu nhóm không muốn cấp mã mới lúc này thì **bỏ #17 và giữ "Mã tiếp theo" là `MSG138`**, đồng thời UC-16 sequence (mục 4.3) dùng `MSG33` cho nhánh EX-4.

### 2.2 `docs/03-api/05-quan-ly-cuoc-hop.md` — 1 thay đổi [A] + 24 thay đổi [B]

[A] Chèn sau dòng 4 (sau `UC-18 View Meeting List — SCRUM-18; UC-19 Submit / UC-20 Review / UC-21 Publish — SCRUM-19.**`), thêm một dòng trống rồi:

```markdown
> **Thay đổi đã chốt 09/10/2026, chờ migration ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)):** cuộc họp tự mang lịch (`plannedStartTime`, `plannedEndTime`, `onlineLink`), không còn Meeting Session; sau `Published` Organizer chỉ được dời lịch (thời gian, địa điểm, link) và hệ thống báo lại người được mời. Các đoạn bên dưới nói "lịch thuộc Meeting Session" mô tả code hiện tại và sẽ được thay trong cùng commit với migration.
```

[B] (áp dụng cùng commit migration; xoá luôn banner [A] ở trên khi áp dụng):

| # | Dòng | Câu hiện tại (trích) | Thay bằng |
|---|---|---|---|
| 1 | 3–4 | `UC-15 Create Meeting / UC-16 Update Meeting — SCRUM-17;` | `UC-15 Create Meeting / UC-16 Update Meeting (gồm dời lịch sau Published) — SCRUM-17;` |
| 2 | 9–11 | `Meeting-level information gồm tên, hình thức, địa điểm, mức độ mật và mô tả; lịch dự kiến thuộc` / `Meeting Sessions theo BR-25. Tạo/sửa Meeting …` | `Meeting-level information gồm tên, lịch dự kiến (Planned Start Time, Planned End Time), hình thức, địa điểm, link họp, mức độ mật và mô tả. Tạo/sửa Meeting không chọn hoặc gán người duyệt; việc chọn và gán người duyệt diễn ra trong UC-19. Cập nhật Meeting không làm thay đổi trạng thái vòng đời (BR-28).` |
| 3 | 18–20 | `Meeting creation yêu cầu … không bao gồm Planned Start Time hay` / `Planned End Time. Meeting Session được lập lịch riêng theo UC-26/UC-27. Form khởi tạo …` | `Meeting creation yêu cầu các thông tin cấp Meeting theo BR-24, gồm Planned Start Time và Planned End Time (\`plannedEndTime > plannedStartTime\`, BR-62). Form khởi tạo Hình thức là \`InPerson\` và Mức độ mật là \`Normal\`.` |
| 4 | 51 | `; Meeting Session là tùy chọn theo BR-56` | Xoá cụm này (giữ phần còn lại của dòng) |
| 5 | 80 | dòng `\| \`from\`, \`to\` \| … lọc bằng \`EXISTS\` trên Meeting Session không bị hủy. … (BR-30) \|` | `` | `from`, `to` | ISO 8601 có `Z` hoặc offset; giây tùy chọn, ví dụ `2026-10-01T09:00+07:00`; chuẩn hóa UTC và lọc theo lịch dự kiến của chính Meeting. Hai đầu dùng `PlannedEndTime >= from && PlannedStartTime <= to`; chỉ `from` dùng `PlannedEndTime >= from`; chỉ `to` dùng `PlannedStartTime <= to` (BR-30) | `` |
| 6 | 82–83 | `\| \`sortBy\` \| \`title\`, \`createdAt\`; mặc định \`createdAt\` \|` / `\| \`sortDir\` \| … mặc định \`asc\` với title, \`desc\` với createdAt \|` | `` | `sortBy` | `title`, `createdAt`, `plannedStartTime`; mặc định `createdAt` | `` / `` | `sortDir` | `asc`, `desc`; mặc định `asc` với title và plannedStartTime, `desc` với createdAt | `` |
| 7 | 93–94 | `Timestamp thiếu timezone hoặc \`sortBy=startsAt\` trả \`400 MSG32\`; backend không` / `giữ alias lịch cấp Meeting.` | `Timestamp thiếu timezone hoặc \`sortBy=startsAt\` (tên cũ) trả \`400 MSG32\`; dùng \`plannedStartTime\`.` |
| 8 | sau 103 | `      "title": "Giao ban tuần",` (trong JSON danh sách) | Chèn sau dòng 103: `      "plannedStartTime": "2026-10-12T02:00:00+00:00",` và `      "plannedEndTime": "2026-10-12T04:00:00+00:00",` |
| 9 | 140 | `Request body của POST và PUT có các thông tin cấp Meeting; không có lịch phiên họp hoặc người duyệt:` | `Request body của POST và PUT có các thông tin cấp Meeting, gồm lịch dự kiến; không có người duyệt:` |
| 10 | sau 144, sau 146 | JSON request | Chèn sau dòng 144 (`"title"`): `  "plannedStartTime": "2026-10-12T09:00:00+07:00",` và `  "plannedEndTime": "2026-10-12T11:00:00+07:00",`; chèn sau dòng 146 (`"location"`): `  "onlineLink": null,` |
| 11 | 152 | `` `title` là bắt buộc theo BR-24. `` | `` `title`, `plannedStartTime`, `plannedEndTime` là bắt buộc theo BR-24. `` |
| 12 | 162–163 | `Trả \`200 OK\` và object meeting phẳng, không bọc envelope. Meeting object không có Planned Start Time` / `hoặc Planned End Time; lịch thuộc Meeting Sessions. …` | `Trả \`200 OK\` và object meeting phẳng, không bọc envelope, gồm \`plannedStartTime\`, \`plannedEndTime\`, \`actualStartedAt\`, \`actualEndedAt\` (null tới khi bắt đầu / kết thúc) và \`onlineLink\`. \`mode\`, \`sensitivity\` và \`status\` là chuỗi enum.` |
| 13 | sau 174 | (cuối mục PUT) | Chèn khối 2.2-a |
| 14 | 189 | `\| \`Publish\` \| Không \| Approved → Published; Meeting Session là tùy chọn theo BR-56 \|` | `\| \`Publish\` \| Không \| Approved → Published \|` |
| 15 | 241–244 | `Publish khóa meeting, … Meeting Session là tùy chọn: meeting Approved có không, một hoặc nhiều phiên` / `đều có thể được công bố theo BR-56; Publish không dùng lịch phiên làm điều kiện chuyển trạng thái.` | `Publish khóa meeting, kiểm tra quyền, trạng thái Approved, đúng một Chair active và ít nhất một Secretary active trong transaction trước khi ghi bất kỳ thay đổi nào. Invitation response không ảnh hưởng readiness. Lịch dự kiến đã được kiểm tra khi tạo/sửa nên Publish không kiểm tra thêm điều kiện lịch.` |
| 16 | 252 | `` `CalendarAttachment = null` vì lịch thuộc Meeting Session. `` | `` `CalendarAttachment = null` (chưa làm `.ics`). `` |
| 17 | 299 | `Seeder tạo lịch demo trong Meeting Session; việc không có phiên vẫn không chặn Publish theo BR-56.` | `Seeder tạo lịch demo trực tiếp trên Meeting.` |
| 18 | 313 | `chưa yêu cầu; Meeting Session không bắt buộc lúc gửi duyệt theo BR-55. Endpoint chỉ đọc,` | `chưa yêu cầu. Endpoint chỉ đọc,` |
| 19 | sau 324 / sau 326 | bảng readiness | Chèn sau dòng 324: `` | `Meeting.PlannedStartTime`, `Meeting.PlannedEndTime` | Bắt buộc; `PlannedEndTime > PlannedStartTime` | Non-null, `timestamptz` | ``; chèn sau dòng 326: `` | `Meeting.OnlineLink` | Không bắt buộc; tối đa 1.000 ký tự | Nullable (PO-Q4) | `` |
| 20 | 328–329 | `\| Agenda, Meeting Sessions \| … BR-56 xác định session là tùy chọn \|` / `\| Actual times, documents, voting sessions \| Không bắt buộc \| Phát sinh trong phiên họp hoặc thuộc flow khác \|` | `` | Agenda | Không bắt buộc khi submit hoặc Publish | Validation thuộc story quản lý agenda | `` / `` | Actual times, documents, voting sessions | Không bắt buộc | Phát sinh trong cuộc họp hoặc thuộc flow khác | `` |
| 21 | 389–393 | `Model/migration Meeting Session đã có để chuẩn bị cho UC-26–30; … Publish không` / `phụ thuộc vào số lượng phiên theo BR-56.` | `Lịch dự kiến nằm trên Meeting (ADR-0013). Migration bỏ Meeting Session chép lịch từ \`meeting_sessions\` về \`meetings\` rồi drop bảng; các migration SCRUM-19/SCRUM-70 cũ giữ nguyên vì đã lên main.` |
| 22 | sau 399 + 405–406 | bảng Validation baseline; `UC-15/16 không nhận lịch dự kiến. BR-62 (\`PlannedEndsAt > PlannedStartsAt\`) được áp dụng ở` / `Meeting Session. Không kiểm tra trùng tên …` | Chèn sau dòng 399 (`title`): `` | `plannedStartTime` | Bắt buộc; ISO 8601 có timezone; khi dời lịch sau Published phải ở tương lai | `` và `` | `plannedEndTime` | Bắt buộc; lớn hơn `plannedStartTime` (BR-62) | ``; chèn sau dòng 402 (`location`): `` | `onlineLink` | Tùy chọn; trim; tối đa 1.000 ký tự (PO-Q4) | ``. Thay 405–406 bằng: `BR-62 (\`plannedEndTime > plannedStartTime\`) áp dụng trực tiếp cho Meeting. Không kiểm tra trùng tên vì yêu cầu hiện tại chưa quy định luật này.` |
| 23 | sau 438 | bảng "Mã phản hồi" | Chèn sau dòng `\| 409 \| \`MSG33\` \| …`: `` | 409 | `MSG138` | Meeting `Published`: chỉ được dời thời gian, địa điểm, link | `` |
| 24 | 453 | `Planned Start Time và Planned End Time được quản lý riêng bởi MeetingSession.` | `Planned Start Time và Planned End Time là cột của \`meetings\`.` |
| 25 | 484–491 | gạch `- Publish không yêu cầu Meeting Session; …` và gạch `- **Lịch thuộc Meeting Session (SCRUM-70):** … (SCRUM-68).` | Thay 2 gạch bằng khối 2.2-b |
| 26 | 531 | `- Readiness chỉ kiểm tra thông tin cấp cuộc họp; agenda và Meeting Session không bắt buộc (UC-19).` | `- Readiness chỉ kiểm tra thông tin cấp cuộc họp (gồm lịch dự kiến); agenda không bắt buộc (UC-19).` |
| 27 | 537 | `- Người duyệt được chọn trong UC-19; Planned Start Time và Planned End Time thuộc Meeting Session.` | `- Người duyệt được chọn trong UC-19; Planned Start Time và Planned End Time thuộc Meeting (ADR-0013).` |

Khối 2.2-a (chèn sau dòng 174):

```markdown

#### Dời lịch cuộc họp đã công bố (Reschedule — UC-16, nhánh AF)

Khi meeting ở `Published`, Organizer vẫn gọi `PUT /api/meetings/{id}` với đủ body, nhưng chỉ
`plannedStartTime`, `plannedEndTime`, `location`, `onlineLink` được khác giá trị đã lưu. Field khác
thay đổi trả `409 MSG138`. `plannedStartTime` mới phải ở tương lai và `plannedEndTime >
plannedStartTime`; sai trả `400 MSG31` kèm `errors`. Thành công trả `200 MSG30`, không đổi trạng
thái và không ghi lifecycle history; cùng transaction tạo notification cho participant có user active
và `Response != Delegated`, sau commit tạo email job cập nhật lịch với deduplication key riêng cho sự
kiện dời lịch. Body giống hệt dữ liệu đã lưu thì không ghi DB và không gửi thông báo. Từ
`InProgress` trở đi trả `409 MSG33`. Hiện chỉ Organizer được dời lịch (PO-Q3).
```

Khối 2.2-b (thay dòng 484–491):

```markdown
- Publish không có điều kiện về lịch; FE không chặn nút công bố vì lịch.
- **Lịch thuộc Meeting (ADR-0013):** form tạo/sửa gửi `title`, `plannedStartTime`, `plannedEndTime`,
  `mode`, `location`, `onlineLink`, `sensitivity`, `description`. Danh sách có lại cột "Thời gian
  diễn ra" và sắp xếp được theo `plannedStartTime`. Bộ lọc ngày `from`/`to` khớp theo lịch của chính
  cuộc họp. Trang chi tiết hiện thời gian dự kiến (và thời gian thực tế khi có) ở "Thông tin chung";
  bỏ tab "Phiên họp". Ở `Published`, nút sửa mở form dời lịch chỉ cho sửa thời gian, địa điểm, link;
  nhận `409 MSG138` hoặc `409 MSG33` thì tải lại meeting.
```

### 2.3 `docs/03-api/06-scrum19-ban-giao-tich-hop.md` [A] — 1 thay đổi

Tài liệu bàn giao lịch sử (02/10/2026) — **không viết lại**, chỉ chèn banner sau dòng 1 (`# SCRUM-19 · Bàn giao FE, Tester và worker`), thêm một dòng trống rồi:

```markdown
> **Lưu ý 09/10/2026:** đây là bàn giao SCRUM-19 tại thời điểm 02/10/2026. PO đã bỏ Meeting Session ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)): các câu về Meeting Session, BR-55/BR-56, UC-26–30 và mã `MSG48`–`MSG59`/`MSG77` bên dưới (dòng 13, 25, 67, 76–77, 106, 111–112, 116–123) chỉ còn giá trị lịch sử; các mã đó đã ngừng dùng. Dòng 106 ghi "`MSG65` cho phòng chưa mở" là lệch catalogue — phòng chưa mở dùng `MSG117`. Hợp đồng hiện hành xem [05-quan-ly-cuoc-hop.md](05-quan-ly-cuoc-hop.md).
```

### 2.4 `docs/03-api/06-thong-bao-trong-ung-dung.md` [B] — 1 thay đổi

| Dòng | Câu hiện tại | Thay bằng |
|---|---|---|
| 175–178 | `- **Sắp bắt đầu:** worker kiểm tra Meeting Session không bị hủy của meeting \`Published\` hoặc` … `` `meeting-session-starting-soon:{sessionId}:{userId}` ngăn xếp lặp theo từng phiên/người nhận. `` | `- **Sắp bắt đầu:** worker kiểm tra meeting \`Published\` mỗi 5 giây và xếp một thông báo cho mỗi participant còn hoạt động khi còn tối đa **15 phút** trước \`PlannedStartTime\` của meeting. Unique key \`meeting-starting-soon:{meetingId}:{userId}\` ngăn xếp lặp (khớp [ERD §9](../02-design/02-erd.md)). Việc nhắc lại sau khi dời lịch do BE quyết định khi làm và ghi vào ticket.` |

### 2.5 `docs/03-api/07-gui-email-thu-moi.md` [B] — 1 thay đổi

| Dòng | Câu hiện tại | Thay bằng |
|---|---|---|
| 43–45 | `Mỗi thư Publish gồm nội dung tiếng Việt, thông tin Meeting, địa điểm/hình thức và đường dẫn trực tiếp` / `tới \`/mgmt/meetings/{id}\`. Thư không ghi Planned Start/End Time và không tạo tệp \`.ics\`, vì lịch dự` / `kiến thuộc từng Meeting Session chứ không thuộc Meeting.` | `Mỗi thư Publish gồm nội dung tiếng Việt, thông tin Meeting, thời gian dự kiến (\`PlannedStartTime\`–\`PlannedEndTime\`), địa điểm/hình thức/link họp và đường dẫn trực tiếp tới \`/mgmt/meetings/{id}\`. Chưa tạo tệp \`.ics\`. Dời lịch sau Published tạo một lượt thư mới với deduplication key riêng cho sự kiện dời lịch.` |

### 2.6 `docs/03-api/07-gui-email-thu-moi-note-cho-fe.md` [B] — 1 thay đổi

| Dòng | Câu hiện tại | Thay bằng |
|---|---|---|
| 46–47 | `- API Meeting không còn trả Planned Start/End Time; FE lấy lịch từ Meeting Session ở story tương ứng.` / `- Email Publish không hiển thị lịch và không đính kèm \`.ics\`; FE chỉ dùng link chi tiết Meeting.` | `- API Meeting trả \`plannedStartTime\`, \`plannedEndTime\` (và \`actualStartedAt\`, \`actualEndedAt\`); FE hiển thị lịch từ chính Meeting.` / `- Email Publish có thời gian dự kiến, không đính kèm \`.ics\`; FE chỉ dùng link chi tiết Meeting.` |

### 2.7 `docs/03-api/README.md` [B] — 1 thay đổi

| Dòng | Câu hiện tại | Thay bằng |
|---|---|---|
| 43–45 | `… (\`SCRUM-17\`/\`SCRUM-18\`, UC-15–UC-18): quyền theo từng cuộc họp, … mã \`MSG29\`–\`MSG38\`` | `… (\`SCRUM-17\`/\`SCRUM-18\`, UC-15–UC-18, gồm dời lịch sau công bố): quyền theo từng cuộc họp, lọc và phân trang ở server, validation baseline và các điểm cần BA bổ sung; mã \`MSG29\`–\`MSG38\`, \`MSG138\`` |

### 2.8 Không cần sửa

`01-xac-thuc*.md`, `02-quan-ly-nguoi-dung*.md`, `03-ma-tran-phan-quyen*.md`, `04-*.md`, `07-gui-email-thu-moi-note-cai-dat-may-moi.md`, `postman/README.md`, `postman/uc08-uc09-quan-ly-nguoi-dung.postman_collection.json` — chữ "session/phiên" ở đó là phiên đăng nhập / refresh token.

---

## 3. `docs/05-decisions/`

### 3.1 ADR bị ảnh hưởng

| ADR | Phần bị thay | Hành động (chỉ dòng trạng thái) |
|---|---|---|
| ADR-0010 `0010-phong-hop-truc-tuyen-dung-livekit-sfu.md` | Bối cảnh dòng 29–38 (Meeting Session), bảng thuật ngữ 40–50, Quyết định 2 gạch 1–2 (dòng 60–66: "đang có một phiên họp `InProgress`", BR-59), Quyết định 3 gạch 2 và 4 (dòng 72–78: đóng phòng theo phiên, cột `call_sessions.meeting_session_id`), Hệ quả gạch 3 (dòng 116–123, gồm câu `MSG65`) | Dòng 3 `- **Trạng thái:** Đã chấp nhận` → `- **Trạng thái:** Thay thế một phần bởi ADR-0013` |
| ADR-0011 `0011-tinh-nang-hop-la-panel-dung-chung.md` | Quyết định 6 "Dữ liệu theo phiên họp" (dòng 52–71), mục 3 gạch "bắt đầu / kết thúc phiên họp" (dòng 83), Kiểm thử 7 (dòng 145–149) | Dòng 3 `- **Trạng thái:** Đã chấp nhận` → `- **Trạng thái:** Thay thế một phần bởi ADR-0013` |
| ADR-0012 `0012-giao-dien-hanh-chinh-sidebar-doc.md` | Bảng "Màn / khối — Chờ" dòng "Lịch tuần … `GET /api/meetings/{id}/sessions`" (dòng 57), đoạn ghép 2N+1 request (dòng 64–69), chữ "mỗi phiên một thẻ" (dòng 49–51) | Dòng 3 `- **Trạng thái:** Đề xuất` → `- **Trạng thái:** Thay thế một phần bởi ADR-0013` |
| ADR-0005 `0005-khach-ngoai-tham-du-bang-link-moi.md` | Chỉ câu phụ "**Không tính vào quorum.** …" (dòng 27–28) | **Không đổi trạng thái** (quyết định chính về khách không đổi). ADR-0014 ghi câu này hết ý nghĩa. |
| ADR-0006, ADR-0007 | "phiên bản" / session refresh token | Không ảnh hưởng |

### 3.2 `docs/05-decisions/README.md` [A] — 5 thay đổi

| # | Dòng | Câu hiện tại | Thay bằng |
|---|---|---|---|
| 1 | 23 | `` | [0010](…) | Phòng họp trực tuyến dùng LiveKit (SFU) tự host | Đề xuất | `` | đổi cột cuối thành `Thay thế một phần bởi ADR-0013` |
| 2 | 24 | `` | [0011](…) | Tính năng dùng trong lúc họp là panel … | Đề xuất | `` | đổi cột cuối thành `Thay thế một phần bởi ADR-0013` |
| 3 | 25 | `` | [0012](…) | Giao diện phong cách hành chính, một sidebar dọc | Đề xuất | `` | đổi cột cuối thành `Thay thế một phần bởi ADR-0013` |
| 4 | sau 25 | — | chèn `` | [0013](0013-mot-cuoc-hop-la-mot-buoi-hop.md) | Một cuộc họp là một buổi họp — bỏ Meeting Session | Đề xuất | `` |
| 5 | sau 4 | — | chèn `` | [0014](0014-bieu-quyet-chi-trong-cuoc-hop.md) | Biểu quyết chỉ trong cuộc họp — bỏ biểu quyết có hạn, quorum và Resolution | Đề xuất | `` |

Ghi nhận lệch có sẵn: index ghi ADR-0010 và ADR-0011 là "Đề xuất" nhưng file ghi "Đã chấp nhận". Thay đổi #1–#2 xoá luôn chỗ lệch này.

### 3.3 ADR mới — `docs/05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md` [A]

Viết theo `0000-template.md`. Dàn ý (tiếng Việt):

```markdown
# ADR-0013: Một cuộc họp là một buổi họp — bỏ Meeting Session

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-10-09
- **Người quyết định:** PO (quyết định 09/10/2026), nhóm SEP490-G116
- **Thay thế một phần:** ADR-0010 (Meeting Session, điều kiện "có phiên InProgress", cột `call_sessions.meeting_session_id`), ADR-0011 (Quyết định 6, sự kiện hub theo phiên, Kiểm thử 7), ADR-0012 (lịch tuần đọc `GET /api/meetings/{id}/sessions`)

## Bối cảnh
- Report 3 thêm Meeting Session (UC-26–30, UC-53; BR-54–BR-63, BR-64/65/67/68/71–77 theo phiên). SCRUM-19 thêm bảng `meeting_sessions`, SCRUM-70 chuyển lịch từ `meetings` sang `meeting_sessions`.
- AI-eCabinet không có cấp nào dưới cuộc họp: chữ "phiên" trên UI của họ là agenda item (`meeting_agenda_items`). Cuộc họp tự mang `starts_at`/`ends_at`, địa điểm, link; sau Published chỉ dời giờ/địa điểm.
- Hai vòng đời lồng nhau (Meeting + Session) làm điều kiện mở phòng, agenda, End Meeting và lịch tuần phức tạp mà không có yêu cầu nghiệp vụ thật nào cần họp nhiều buổi trong một hồ sơ.
- PO chốt 09/10/2026; BF-02/03/04 đã vẽ lại.

## Quyết định
1. Meeting mang lịch: `PlannedStartTime`, `PlannedEndTime` (bắt buộc, End > Start), `ActualStartedAt`/`ActualEndedAt` (hệ thống ghi khi Start/End), `Mode`, `Location`, `OnlineLink` (tùy chọn — PO-Q4).
2. Bỏ entity/bảng `MeetingSession`, enum `MeetingSessionStatus`, `MeetingSessionRules`. AgendaItem thuộc trực tiếp Meeting (`AgendaItem.MeetingId` — code đã có).
3. Chỉ còn vòng đời Meeting. Publish không có điều kiện về lịch phiên; End Meeting không kiểm tra phiên chưa hoàn tất.
4. Sau `Published`, Organizer chỉ được dời lịch (thời gian, địa điểm, link) qua UC-16; hệ thống tạo notification và email cập nhật lịch cho người được mời. Không ghi lifecycle history cho việc dời lịch.
5. Phòng LiveKit `meeting-{meetingId}` mở khi `Meeting.Status = InProgress` và Mode Online/Hybrid — điều kiện "tạm" của ADR-0010 thành chính thức. Không thêm cột `call_sessions.meeting_session_id`. Phòng chưa mở dùng `MSG117` (ADR-0010 ghi `MSG65`, lệch catalogue).
6. Panel (ADR-0011) chỉ làm việc theo `meetingId`. Planning context = cuộc họp chưa `InProgress` (trạng thái cụ thể theo PO-Q5); live context = cuộc họp `InProgress`. Hub `/hubs/meetings` phát "meeting started / ended" thay cho "session started / ended / cancelled".
7. Lịch tuần đọc `GET /api/meetings?from&to` (đã lọc quyền ở server), mỗi cuộc họp một thẻ; bỏ gọi `/sessions`.
8. UC-26, UC-27, UC-28, UC-29, UC-30, UC-53: "không còn dùng", giữ số. UC-54 `<<extend>>` UC-17. Mã MSG48–59, MSG77, MSG113, MSG127: ngừng dùng, không tái cấp. Thêm `MSG138` cho dời lịch sai field.

## Phương án đã cân nhắc
| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ Meeting Session như Report 3 | Họp nhiều buổi trong một hồ sơ | Hai vòng đời, phòng/agenda/End Meeting phải biết phiên; eCabinet không có | Phức tạp, không có nhu cầu thật |
| Giữ bảng, ép luôn đúng một session | Không phải migration lớn | Dữ liệu thừa, mọi query vẫn join qua session | Vẫn trả giá của mô hình cũ |
| **Một cuộc họp = một buổi họp** (chọn) | Một vòng đời, khớp eCabinet, đơn giản | Họp nhiều buổi phải tạo nhiều cuộc họp | — |

## Hệ quả
- Migration mới (không sửa `MeetingSessionsForPublication`, `MoveMeetingScheduleToSessions`): thêm cột lịch vào `meetings`, backfill từ session (bắt đầu sớm nhất, kết thúc muộn nhất của session không Cancelled; meeting không có session → PO/BE chọn giá trị mặc định và ghi vào ticket), drop `meeting_sessions`.
- Cuộc họp cũ có nhiều session bị gộp thành một khoảng thời gian — ghi rõ trong mô tả PR.
- Code: xoá `MeetingSession*`, FE `meetingSessions.ts`, tab "Phiên họp"; lịch tuần đổi nguồn; notification "sắp bắt đầu" đọc `meetings.planned_start_time`.
- BA: Report 1 (FE-05, FE-06), Report 3, Master UC Inventory, UC Specification, Business Rules (BR-25, BR-30, BR-54–BR-63, BR-64/65/67/68/69/71–77) đổi phạm vi từ Session sang Meeting hoặc đánh dấu không còn dùng.
- Sequence `meeting-session-management/` và class diagram FE-05 bị xoá; FE-04, FE-06 vẽ lại.
```

### 3.4 ADR mới — `docs/05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md` [A]

```markdown
# ADR-0014: Biểu quyết chỉ trong cuộc họp — bỏ biểu quyết có hạn, quorum và Resolution

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-10-09
- **Người quyết định:** PO (quyết định 09/10/2026), nhóm SEP490-G116

## Bối cảnh
- Report 1 FE-12 hứa "time-boxed vote before or after the meeting", "quorum rule", kết quả "cannot be altered"; 04-luong-nghiep-vu-chinh có "Shared Voting Sub-flow" và "immutable Resolution".
- Code: `VotingSession.Kind` (`Live` | `Asynchronous`, SCRUM-24 "opinion before the meeting"), `OpensAt`, `ClosesAt`, `Conclusion` chuỗi tự do; comment `AttendanceStatus`/`MeetingParticipant.Attendance` nói "quorum count". README gốc có "(S) Biểu quyết bất đồng bộ".
- AI-eCabinet: tạo, mở, bỏ phiếu, đóng ngay trong cuộc họp, chủ trì ghi kết luận Thông qua / Không thông qua / Hoãn; không có phiếu trước/sau họp, không quorum, không đối tượng Resolution.

## Quyết định
1. Biểu quyết chỉ được tạo và mở khi Meeting `InProgress`; Chair tạo, mở, đóng; người dự đủ điều kiện bỏ phiếu; hệ thống đếm phiếu.
2. Sau khi đóng, Chair ghi `Conclusion` ∈ {`Passed`, `NotPassed`, `Deferred`}.
3. Không có quorum, không biểu quyết trước/sau họp, không biểu quyết có hạn chót, không entity Resolution. Biên bản lấy "kết quả và kết luận biểu quyết".
4. Dữ liệu: bỏ `VotingSession.Kind`, `OpensAt`, `ClosesAt` và enum `VotingKind`; `Conclusion` thành chuỗi enum nullable. Giữ `EligibleCount` (số người có quyền bỏ phiếu chốt lúc mở, để kết quả tái lập được — không dùng làm quorum). Giữ tách `VoteReceipt`/`VoteChoice` (phiếu kín). Tên entity `VotingSession` giữ nguyên.
5. UC-72 đổi tên "Open Vote". UC-76: PO-Q2 ("Record Vote Conclusion" hoặc bỏ).

## Phương án đã cân nhắc
| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ cả biểu quyết trong họp và lấy ý kiến có hạn | Đúng Report 1 | Thêm worker hạn chót, quorum, Resolution bất biến | Ngoài phạm vi eCabinet, tốn công |
| Biểu quyết trong họp + quorum theo Attendance | Gần quy chế họp | Phải định nghĩa ngưỡng, ủy quyền, khách | PO không yêu cầu |
| **Chỉ biểu quyết trong họp, Chair ghi kết luận** (chọn) | Khớp eCabinet, đơn giản | Không lấy ý kiến ngoài giờ họp | — |

## Hệ quả
- Câu "Không tính vào quorum" của ADR-0005 hết ý nghĩa; phần còn lại của ADR-0005 không đổi.
- Migration mới drop `kind`, `opens_at`, `closes_at` của `voting_sessions`, đổi `conclusion`.
- README gốc: bỏ "(S) Biểu quyết bất đồng bộ", thêm vào "Không làm". SCRUM-24 cần đóng hoặc sửa ticket.
- BA: Report 1 FE-12/GAP-05/FE-16, Report 2, Report 3 §12, Master UC Inventory, FE-13 (biên bản lấy kết quả biểu quyết).
```

---

## 4. `docs/06-sequencediagram/`

### 4.1 Xoá — `meeting-session-management/` (13 file) [A]

`git rm` toàn bộ thư mục (git history giữ bản cũ):

- `docs/06-sequencediagram/meeting-session-management/README.md`
- `UC26_CreateMeetingSession.txt`, `UC26_CreateMeetingSession.png`
- `UC27_UpdateMeetingSession.txt`, `UC27_UpdateMeetingSession.png`
- `UC28_CancelMeetingSession.txt`, `UC28_CancelMeetingSession.png`
- `UC29_StartMeetingSession.txt`, `UC29_StartMeetingSession.png`
- `UC30_EndMeetingSession.txt`, `UC30_EndMeetingSession.png`
- `UC53_ViewMeetingSessions.txt`, `UC53_ViewMeetingSessions.png`

### 4.2 `docs/06-sequencediagram/README.md` [A] — 21 thay đổi (README là quy ước, chỉ đổi ví dụ)

| # | Dòng | Câu hiện tại | Thay bằng |
|---|---|---|---|
| 1 | 20 | `Secretary->MeetingPage: Submit session information` | `Organizer->MeetingPage: Submit meeting information` |
| 2 | 26 | `Chair->MeetingPage: Start Meeting Session` | `Chair->MeetingPage: Start Meeting` |
| 3 | 32 | `Secretary->MeetingPage: Confirm Delete Session` | `Secretary->MeetingPage: Confirm Delete Agenda Item` |
| 4 | 66 | `actor "Secretary" as Secretary` | `actor "Meeting Organizer" as Organizer` |
| 5 | 71 | `participant ":Meeting Session Repository" as MeetingSessionRepository` | **Xoá dòng** |
| 6 | 120 | `MeetingPage->MeetingController: Create Meeting Session` | `MeetingPage->MeetingController: Create Meeting` |
| 7 | 121 | `MeetingController->MeetingService: createMeetingSession(meetingId, sessionInfo)` | `MeetingController->MeetingService: createMeeting(meetingInfo)` |
| 8 | 133 | `MeetingService-->MeetingController: Meeting Session created successfully` | `MeetingService-->MeetingController: Meeting created successfully` |
| 9 | 143 | `MeetingPage-->Secretary: Show MSG54` | `MeetingPage-->Organizer: Show MSG33` |
| 10 | 149 | `MeetingPage-->Secretary: MSG54 - Meeting sessions can no longer\nbe added to this meeting` | `MeetingPage-->Organizer: MSG33 - This meeting can no longer\nbe edited` |
| 11 | 155 | `MeetingPage-->Secretary: MSG48 - Meeting session created successfully` | `MeetingPage-->Organizer: MSG29 - Meeting created successfully` |
| 12 | 206 | `MeetingService->MeetingService: Validate session information\nand planned time (BR-62)` | `MeetingService->MeetingService: Validate meeting information\nand planned time (BR-24, BR-62)` |
| 13 | 212 | `MeetingService->MeetingSessionRepository: Create Meeting Session\nwith Planned status (BR-54, BR-58)` | `MeetingService->MeetingRepository: Create meeting\nwith Draft status (BR-26)` |
| 14 | 222 | `alt Session information is invalid - EX-1` | `alt Meeting information is invalid - EX-1` |
| 15 | 224 | `else Session information is valid` | `else Meeting information is valid` |
| 16 | 249 | `alt Other Meeting Sessions remain - AF-1` | `alt No Agenda Items exist - AF-1` |
| 17 | 251 | `else No unfinished Meeting Sessions remain` | `else Agenda Items exist` |
| 18 | 263 | (giống dòng 206) | (giống #12) |
| 19 | 283 | `` `MeetingSession` là business session. `CallSession` là technical LiveKit room run. Không trộn hai khái niệm trong các UC Meeting Session. `` | `` `CallSession` là một lượt mở phòng LiveKit (technical), không phải cuộc họp nghiệp vụ. Không có `MeetingSession` (đã bỏ — ADR-0013); một cuộc họp là một buổi họp. `` |
| 20 | 367–447 | `## 13. Reference — UC-26 Create Meeting Session` + toàn bộ khối code | Thay tiêu đề bằng `## 13. Reference — UC-15 Create Meeting` và thay khối code bằng **nguyên văn** nội dung file `meeting-management/UC15_CreateMeeting.txt` sau khi đã sửa theo 4.3-#1 |
| 21 | 464 | `- [ ] Không trộn technical \`CallSession\` với business \`MeetingSession\`.` | `- [ ] Không dùng \`MeetingSession\` (đã bỏ — ADR-0013); không trộn technical \`CallSession\` với cuộc họp nghiệp vụ.` |

### 4.3 `docs/06-sequencediagram/meeting-management/` [A] — 8 file sửa, 8 PNG xuất lại

| # | File | Dòng | Câu hiện tại | Hành động / thay bằng |
|---|---|---|---|---|
| 1 | `UC15_CreateMeeting.txt` | 19 | `MeetingService->MeetingService: Validate meeting-level information\n(BR-24; schedule belongs to Meeting Sessions)` | `MeetingService->MeetingService: Validate meeting information and planned schedule\n(BR-24, BR-62)` |
| 2 | `UC16_UpdateMeeting.txt` | nhiều | xem 4.3-a | Sửa theo 4.3-a |
| 3 | `UC17_ViewMeetingDetails.txt` | 47 | `MeetingService-->MeetingController: Meeting Name, Mode, Location, Confidentiality Level, Description, Status` | `MeetingService-->MeetingController: Meeting Name, Planned Start/End Time, Actual Start/End Time,\nMode, Location, Online Link, Confidentiality Level, Description, Status` |
| 4 | `UC17_ViewMeetingDetails.txt` | 55 | `note over MeetingService: This use case does not load Meeting Sessions or Agenda Items.` | `note over MeetingService: This use case does not load Agenda Items (see UC-54).` |
| 5 | `UC18_ViewMeetingList.txt` | 28 | `MeetingRepository->Database: Filter Time Range by overlapping planned schedules\nof non-cancelled Meeting Sessions (BR-30); meetings without a matching session are excluded` | `MeetingRepository->Database: Filter Time Range by overlapping\nplanned schedule of the meeting (BR-30)` |
| 6 | `UC19_SubmitMeetingForApproval.txt` | 49 | `MeetingService->MeetingService: Validate mandatory meeting-level information\nMeeting Sessions and Agenda are not required` | `MeetingService->MeetingService: Validate mandatory meeting-level information\nincluding planned schedule; Agenda is not required` |
| 7 | `UC21_PublishMeeting.txt` | 60 | `(BR-37, BR-56, BR-89)` | `(BR-37, BR-89)` |
| 8 | `UC43_StartMeeting.txt` | 55 | `MeetingService->MeetingService: Revalidate Chair assignment, Published status, active Chair and Secretary; sessions are optional (BR-56, BR-89, BR-90, BR-91)` | `MeetingService->MeetingService: Revalidate Chair assignment, Published status, active Chair and Secretary (BR-89, BR-90, BR-91)` |
| 9 | `UC43_StartMeeting.txt` | 65 | `MeetingService->MeetingRepository: Change Published to InProgress (BR-90)` | `MeetingService->MeetingRepository: Change Published to InProgress\nand record actual start time (BR-90)` (thể hiện D1 `ActualStartedAt`) |
| 10 | `UC44_EndMeeting.txt` | 9, 39, 48–112 | xem 4.3-b | Sửa theo 4.3-b |

Sau khi sửa: `node scripts/export-diagrams.js docs/06-sequencediagram/meeting-management` để xuất lại `UC15`, `UC16`, `UC17`, `UC18`, `UC19`, `UC21`, `UC43`, `UC44` `.png`.

**4.3-a — `UC16_UpdateMeeting.txt`** (sửa từ dưới lên):

(Thụt lề trong file `.txt` không ảnh hưởng kết quả vẽ; chỉ cần giữ thứ tự dòng.)

1. **Chèn sau dòng 95** (`                deactivate MeetingRepository`, ngay sau dòng 94 `MeetingRepository-->MeetingService: Updated meeting` và ngay trước dòng 96 `MeetingService-->MeetingController: Meeting updated successfully`):
   ```
                   opt Published meeting was rescheduled - AF-1
                       MeetingService->NotificationService: Notify invitees of the new schedule (BR-76)
                       activate NotificationService
                       NotificationService-->MeetingService: Notifications created
                       deactivate NotificationService
                       MeetingService->MeetingInvitationQueue: Queue schedule update emails (BR-76)
                       activate MeetingInvitationQueue
                       MeetingInvitationQueue-->MeetingService: Emails queued
                       deactivate MeetingInvitationQueue
                   end
   ```
2. Dòng 75: `MeetingService->MeetingRepository: Update meeting information;\nkeep lifecycle status and approval assignment unchanged` → giữ nguyên.
3. Dòng 65: `MeetingService->MeetingService: Validate submitted meeting-level information\n(BR-27; schedule belongs to Meeting Sessions)` → `MeetingService->MeetingService: Validate submitted meeting information and planned schedule\n(BR-27, BR-62); a rescheduled start must be in the future (BR-75)`
4. **Chèn sau dòng 62** (`        MeetingPage-->Organizer: MSG33 - This meeting can no longer be edited` của nhánh lồng `alt Meeting is no longer editable - EX-2`, trước dòng trống 63 và `    else Meeting remains editable` dòng 64):
   ```

       else Meeting is Published and fields other than time, location or link changed - EX-4
           MeetingService-->MeetingController: MSG138 - A published meeting can only be rescheduled:\nchange the planned time, location or online link
           deactivate MeetingService
           MeetingController-->MeetingPage: Return reschedule restriction
           deactivate MeetingController
           MeetingPage-->Organizer: MSG138 - A published meeting can only be rescheduled:\nchange the planned time, location or online link
   ```
5. Dòng 55: `MeetingService->MeetingService: Recheck Draft or Rejected status` → `MeetingService->MeetingService: Recheck Draft, Rejected or Published status;\nfor Published only planned time, location and online link may change`
6. Dòng 41: `else Meeting is Draft or Rejected` → `else Meeting is Draft, Rejected or Published`
7. Dòng 34: `else Meeting is no longer Draft or Rejected - EX-2` → `else Meeting is not Draft, Rejected or Published - EX-2`
8. **Chèn sau dòng 7** (`participant ":Meeting Repository" as MeetingRepository`):
   ```
   participant ":Notification Service" as NotificationService
   participant ":Meeting Invitation Queue" as MeetingInvitationQueue
   ```

(BR-75, BR-76 hiện là luật của Session — BA phải đổi phạm vi sang Meeting; xem mục 10 "Phụ thuộc".)

**4.3-b — `UC44_EndMeeting.txt`**:

1. Thay dòng 48–112 (từ `else Meeting and Chair are eligible` tới `    end` đóng nhánh lồng — dòng 112) bằng:
   ```
   else Meeting and Chair are eligible
       MeetingService->MeetingRepository: Change InProgress to Ended\nand record actual end time (BR-90, BR-92)
       activate MeetingRepository
       MeetingRepository->Database: Save meeting lifecycle transition
       activate Database

       alt Meeting end cannot be completed - EX-3
           Database-->MeetingRepository: Save failed
           deactivate Database
           MeetingRepository-->MeetingService: Meeting end failed
           deactivate MeetingRepository
           MeetingService-->MeetingController: MSG116 - Unable to end the meeting. Please try again
           deactivate MeetingService
           MeetingController-->MeetingPage: Return meeting end failure
           deactivate MeetingController
           MeetingPage-->Chair: MSG116 - Unable to end the meeting. Please try again

       else Meeting status is Ended
           Database-->MeetingRepository: Meeting status updated
           deactivate Database
           MeetingRepository-->MeetingService: Meeting status updated
           deactivate MeetingRepository

           MeetingService->MeetingStatusHistoryRepository: Record end action, actor, timestamp, and status (BR-38)
           activate MeetingStatusHistoryRepository
           MeetingStatusHistoryRepository->Database: Insert lifecycle history
           activate Database
           Database-->MeetingStatusHistoryRepository: History recorded
           deactivate Database
           MeetingStatusHistoryRepository-->MeetingService: History recorded
           deactivate MeetingStatusHistoryRepository

           MeetingService->MeetingRoom: Close room and disconnect connected users (BR-96)
           activate MeetingRoom
           MeetingRoom-->MeetingService: Room closed; connected users disconnected
           deactivate MeetingRoom

           MeetingService-->MeetingController: Meeting ended successfully
           deactivate MeetingService
           MeetingController-->MeetingPage: MSG111 - Meeting ended successfully
           deactivate MeetingController
           MeetingPage-->Chair: MSG111 - Meeting ended successfully
       end
   ```
   (Dòng 113 `end` và 115 `deactivate MeetingPage` giữ nguyên.)
2. Dòng 39: `MeetingService->MeetingService: Revalidate active Chair, InProgress status, and no Planned or InProgress sessions (BR-90, BR-91)` → `MeetingService->MeetingService: Revalidate active Chair and InProgress status (BR-90, BR-91)`
3. Dòng 9: `participant ":Meeting Session Repository" as MeetingSessionRepository` → **xoá dòng**.

Kết quả: nhánh EX-1 (`MSG113`) biến mất; Exception đánh số EX-2/EX-3 giữ nguyên số (không đánh lại — BA quyết khi review).

### 4.4 `docs/06-sequencediagram/agenda-management/` [A] — 7 file sửa, 7 PNG xuất lại

Quy tắc chung áp dụng cho **UC31, UC32, UC33, UC34** (Secretary):

| Bước | Tìm | Hành động |
|---|---|---|
| G1 | `participant ":Meeting Session Repository" as MeetingSessionRepository` (dòng 9) | Xoá dòng |
| G2 | trong lời gọi service: `(meetingId, sessionId, ` | Thay bằng `(meetingId, ` |
| G3 | khối 8 dòng bắt đầu `AgendaService->MeetingSessionRepository: Get selected Meeting Session` tới `deactivate MeetingSessionRepository` | Xoá cả khối và một dòng trống sau nó |
| G4 | `AgendaService->MeetingRepository: Get parent meeting` | `AgendaService->MeetingRepository: Get selected meeting` |
| G5 | `MeetingRepository->Database: Retrieve parent meeting` | `MeetingRepository->Database: Retrieve meeting` |
| G6 | `Verify Secretary assignment to parent meeting (BR-65)` | `Verify Secretary assignment to the meeting (BR-65)` |
| G7 | `Get selected documents available to parent meeting (BR-66)` | `Get selected documents available to the meeting (BR-66)` |
| G8 | `MSG67 - The agenda for this meeting session can no longer be modified.` (3 lần mỗi file) | `MSG67 - The agenda for this meeting can no longer be modified.` |
| G9 | `Updated session agenda` / `Show updated session agenda` | `Updated meeting agenda` / `Show updated meeting agenda` |

Thay riêng từng file (dòng gốc):

| File | Dòng | Câu hiện tại | Thay bằng |
|---|---|---|---|
| UC31_CreateAgendaItem.txt | 71 | `AgendaService->AgendaService: Verify session belongs to selected meeting, assigned Secretary,\nPlanned session and parent meeting permits agenda planning;\nPublished or InProgress alone does not block planning (BR-64, BR-65, BR-68, BR-74)` | `AgendaService->AgendaService: Verify assigned Secretary and that the meeting status\npermits agenda planning (BR-65, BR-68, BR-74)` |
| UC31 | 81 | `AgendaService->AgendaItemRepository: Get Agenda Items of selected Meeting Session` | `AgendaService->AgendaItemRepository: Get Agenda Items of the meeting` |
| UC31 | 83 | `AgendaItemRepository->Database: Retrieve session agenda in explicit order` | `AgendaItemRepository->Database: Retrieve meeting agenda in explicit order` |
| UC31 | 85, 87 | `Session Agenda Item list` | `Meeting Agenda Item list` |
| UC31 | 90 | `Create Agenda Item for selected Meeting Session\nwith Pending status …` | `Create Agenda Item for the meeting\nwith Pending status at the end of the agenda\nand associate selected meeting documents (BR-64, BR-67)` |
| UC32_UpdateAgendaItem.txt | 80 | `AgendaService->AgendaService: Verify session and item ownership, Pending Agenda Item,\nassigned Secretary, Planned session and parent meeting permits agenda planning;\nPublished or InProgress alone does not block planning (BR-65, BR-68, BR-74)` | `AgendaService->AgendaService: Verify item belongs to the meeting, Pending Agenda Item,\nassigned Secretary and that the meeting status permits agenda planning (BR-65, BR-68, BR-74)` |
| UC33_DeleteAgendaItem.txt | 58 | (giống UC32 dòng 80) | (giống thay thế UC32 dòng 80) |
| UC34_ReorderAgendaItems.txt | 49 | `Get Agenda Items of selected Meeting Session` | `Get Agenda Items of the meeting` |
| UC34 | 51 | `Retrieve session agenda in explicit order` | `Retrieve meeting agenda in explicit order` |
| UC34 | 53, 55 | `Session Agenda Item list` | `Meeting Agenda Item list` |
| UC34 | 58 | `Validate all reordered items belong to the same Meeting Session\nand retain explicit session ordering (BR-64, BR-67)` | `Validate all reordered items belong to the meeting\nand retain explicit ordering (BR-64, BR-67)` |
| UC34 | 59 | (giống UC31 dòng 71) | `AgendaService->AgendaService: Verify assigned Secretary and that the meeting status\npermits agenda planning (BR-65, BR-68, BR-74)` |
| UC34 | 69 | `Save updated explicit order within selected Meeting Session\nwithout moving items to another session (BR-64, BR-67, BR-68)` | `Save updated explicit order within the meeting (BR-64, BR-67, BR-68)` |
| UC34 | 71 | `Update positions of session Agenda Items` | `Update positions of meeting Agenda Items` |

**UC35_SetCurrentAgendaItem.txt** và **UC36_CompleteAgendaItem.txt** (Chair):

| File | Dòng | Câu hiện tại | Thay bằng |
|---|---|---|---|
| UC35, UC36 | 8 | `participant ":Meeting Session Repository" as MeetingSessionRepository` | `participant ":Meeting Repository" as MeetingRepository` |
| UC35 | 18 | `setCurrentAgendaItem(meetingId, sessionId, agendaItemId, chairId)` | `setCurrentAgendaItem(meetingId, agendaItemId, chairId)` |
| UC36 | 18 | `completeAgendaItem(meetingId, sessionId, agendaItemId, chairId)` | `completeAgendaItem(meetingId, agendaItemId, chairId)` |
| UC35, UC36 | 21–28 | `AgendaService->MeetingSessionRepository: Get selected Meeting Session` / `activate MeetingSessionRepository` / `MeetingSessionRepository->Database: Retrieve Meeting Session` / `activate Database` / `Database-->MeetingSessionRepository: Meeting Session` / `deactivate Database` / `MeetingSessionRepository-->AgendaService: Meeting Session` / `deactivate MeetingSessionRepository` | `AgendaService->MeetingRepository: Get selected meeting` / `activate MeetingRepository` / `MeetingRepository->Database: Retrieve meeting` / `activate Database` / `Database-->MeetingRepository: Meeting` / `deactivate Database` / `MeetingRepository-->AgendaService: Meeting` / `deactivate MeetingRepository` |
| UC35, UC36 | 30 | `Verify Chair assignment to parent meeting (BR-69)` | `Verify Chair assignment to the meeting (BR-69)` |
| UC35 | 48 | `Verify assigned Chair, InProgress Meeting Session,\nsession belongs to selected meeting, item belongs to selected session,\nand item is Pending (BR-64, BR-69, BR-70)` | `Verify assigned Chair, InProgress meeting,\nitem belongs to selected meeting,\nand item is Pending (BR-64, BR-69, BR-70)` |
| UC36 | 48 | (giống UC35 nhưng kết thúc `and item is InProgress (BR-64, BR-69, BR-70)`) | `Verify assigned Chair, InProgress meeting,\nitem belongs to selected meeting,\nand item is InProgress (BR-64, BR-69, BR-70)` |
| UC35 | 58 | `Check for another InProgress Agenda Item\nin the same Meeting Session (BR-71)` | `Check for another InProgress Agenda Item\nin the same meeting (BR-71)` |
| UC35 | 60 | `Find another InProgress Agenda Item in selected session` | `Find another InProgress Agenda Item in the meeting` |
| UC35 | 92 | `to authorized users viewing the same Meeting Session (BR-73)` | `to authorized users viewing the same meeting (BR-73)` |
| UC36 | 75 | `Session has no current Agenda Item until Chair starts another;\nkeep Meeting Session InProgress (BR-72)` | `Meeting has no current Agenda Item until Chair starts another;\nkeep the meeting InProgress (BR-72)` |
| UC36 | 76 | `to authorized users viewing the same Meeting Session (BR-73)` | `to authorized users viewing the same meeting (BR-73)` |

**UC54_ViewMeetingAgenda.txt**:

| Dòng | Câu hiện tại | Thay bằng |
|---|---|---|
| 9 | `participant ":Meeting Session Repository" as MeetingSessionRepository` | Xoá dòng |
| 17 | `getMeetingAgenda(meetingId, sessionId, userId)` | `getMeetingAgenda(meetingId, userId)` |
| 20 | `AgendaService->MeetingRepository: Get parent meeting` | `AgendaService->MeetingRepository: Get selected meeting` |
| 29–36 (+ dòng trống 37) | khối `AgendaService->MeetingSessionRepository: Get selected Meeting Session` … `deactivate MeetingSessionRepository` | Xoá |
| 38 | `Verify user may view the meeting and selected session (BR-29)` | `Verify user may view the meeting (BR-29)` |
| 43 | `alt Meeting or selected session is not accessible - EX-1` | `alt Meeting is not accessible - EX-1` |
| 50 | `else Meeting and selected session are accessible` | `else Meeting is accessible` |
| 51 | `Get session agenda in defined order (BR-64, BR-73)` | `Get meeting agenda in defined order (BR-64, BR-73)` |

`agenda-management/README.md`: không đổi. Xuất lại 7 PNG: `node scripts/export-diagrams.js docs/06-sequencediagram/agenda-management`.

### 4.5 `docs/06-sequencediagram/meeting-participant-management/README.md` [A] — 1 thay đổi

| Dòng | Câu hiện tại | Thay bằng |
|---|---|---|
| 10–11 | `- Organizer công bố từ \`Approved\` khi có Planned Meeting Session hợp lệ và đủ Chair/Secretary` / `  hiện hành có tài khoản hoạt động. Sequence UC-21 cũng được đồng bộ điều kiện này.` | `- Organizer công bố từ \`Approved\` khi đủ Chair/Secretary hiện hành có tài khoản hoạt động; không có điều kiện về lịch phiên (cuộc họp tự mang lịch — ADR-0013). Sequence UC-21 cũng được đồng bộ điều kiện này.` |

Các sequence UC37–UC42 `.txt`: không chạm Meeting Session — không đổi.

### 4.6 Không cần sửa

`auth/`, `role-management/`, `user-management/` (UC07 `:Session Service` là phiên đăng nhập — việc đổi tên `:Token Service` đã có trong `07-classdiagram/README.md` A3, không thuộc audit này), `notification-management/`.

### 4.7 Sơ đồ biểu quyết

Hiện **chưa có** sequence nào cho UC-70…UC-76 nên D2 không phải sửa sequence. Khi vẽ mới: UC-72 tiêu đề `title UC-72 - Open Vote`, guard "meeting is InProgress"; không có nhánh quorum/deadline.

---

## 5. `docs/07-classdiagram/` [A]

### 5.1 Xoá

- `docs/07-classdiagram/feature-05-meeting-session-management-class-diagram.drawio.png` — `git rm`.

### 5.2 `feature-04-meeting-management-class-diagram.drawio.png` (PNG có nhúng XML, mở bằng draw.io) — 9 thay đổi

| # | Cell id | Hiện tại | Hành động |
|---|---|---|---|
| 1 | `sr` | `<<repository>> MeetingSessionRepository` (`findValidPlanned`, `hasValidPlannedSession`) | Xoá cell |
| 2 | `session` | `<<entity>> MeetingSession` | Xoá cell |
| 3 | `e5` | `svc → sr` "publication readiness" | Xoá edge |
| 4 | `e10` | `sr → session` "reads" | Xoá edge |
| 5 | `e13` | `meeting → session` "1 schedules 0..*" | Xoá edge |
| 6 | `meeting` | dòng `+ sessions: MeetingSession[]` | Thay bằng 3 dòng: `+ plannedStartTime / plannedEndTime: DateTimeOffset`, `+ actualStartedAt / actualEndedAt: DateTimeOffset?`, `+ location / onlineLink: string?` |
| 7 | `svc` | `+ validatePublicationReadiness(meetingId)` | Giữ; thêm dòng `+ rescheduleMeeting(meetingId, schedule)` |
| 8 | `ctrl` | `+ updateMeeting(id, input, actorId)` | Giữ; thêm dòng `+ rescheduleMeeting(id, schedule, actorId)` (hoặc ghi chú "UC-16 AF qua updateMeeting" nếu BE gộp vào PUT) |
| 9 | `page` | `+ submitCreate / update` | Đổi thành `+ submitCreate / update / reschedule` |

Xuất lại PNG có nhúng XML, cùng tên file.

### 5.3 `feature-06-agenda-management-class-diagram.drawio` + `.png` — 10 thay đổi

| # | Cell id | Hiện tại | Hành động |
|---|---|---|---|
| 1 | `sessionRepo` | `<<repository>> MeetingSessionRepository` | Xoá |
| 2 | `session` | `<<entity>> MeetingSession` | Xoá |
| 3 | `sessionStatus` | `<<enumeration>> MeetingSessionStatus` | Xoá |
| 4 | `a5`, `a10`, `a14`, `a15`, `a18` | edges tới/từ session | Xoá cả 5 |
| 5 | (mới) | — | Thêm edge `meeting → agenda`, nhãn `1 contains 0..*` |
| 6 | `boundary` | `+ reorderAgendaItems(sessionId, itemIds)`, `+ viewMeetingAgenda(sessionId)` | `+ reorderAgendaItems(meetingId, itemIds)`, `+ viewMeetingAgenda(meetingId)` |
| 7 | `controller` | `+ reorderAgendaItems(sessionId, itemIds)`, `+ getMeetingAgenda(meetingId, sessionId)` | `+ reorderAgendaItems(meetingId, itemIds)`, `+ getMeetingAgenda(meetingId)` |
| 8 | `service` | `+ reorderAgendaItems(sessionId, itemIds)`, `+ getMeetingAgenda(meetingId, sessionId)` | `+ reorderAgendaItems(meetingId, itemIds)`, `+ getMeetingAgenda(meetingId)` |
| 9 | `agendaRepo` | `+ findOrderedBySession(sessionId)` | `+ findOrderedByMeeting(meetingId)` |
| 10 | `agenda` | `- meetingSessionId: UUID` | `- meetingId: UUID` |

Thêm vào entity `meeting` (tùy chọn, cho khớp FE-04): `- plannedStartTime / plannedEndTime: DateTimeOffset`. Xuất lại `feature-06-agenda-management-class-diagram.png`.

### 5.4 `feature-07-meeting-participant-management-class-diagram.drawio` — 0 thay đổi bắt buộc

Không có MeetingSession. Tùy chọn: thêm `- plannedStartTime / plannedEndTime` vào entity `meeting` cho đồng bộ.

### 5.5 `docs/07-classdiagram/README.md` — 1 thay đổi

Chèn sau dòng 6 (gạch FE-07):

```markdown
- FE-04 — Meeting Management ([PNG có nhúng drawio](feature-04-meeting-management-class-diagram.drawio.png)) — UC-15–21, UC-43–44; Meeting tự mang lịch, gồm dời lịch sau Published.
- FE-05 — Meeting Session Management: **không còn dùng** từ 09/10/2026 (ADR-0013); bản vẽ đã xoá.
```

Phần A–F của README nói `:Session Service` (phiên đăng nhập) — không đổi.

---

## 6. `docs/assets/`

### 6.1 `docs/assets/use-case-diagrams/` [A] — 6 `.drawio` sửa, 6 PNG + 6 PDF xuất lại

Không có association actor → UC-26/27/28/29/30/53; chỉ có quan hệ `«extend»`.

| File | Xoá cell (oval) | Xoá edge | Sửa edge | Đổi nhãn |
|---|---|---|---|---|
| `uc-chair.drawio` | `uc-29` (UC-29 Start Meeting Session), `uc-30` (UC-30 End Meeting Session), `uc-53` | `rel-29-53-extend`, `rel-30-53-extend`, `rel-53-17-extend` | `rel-54-53-extend`: `target="uc-53"` → `target="uc-17"`, đổi id thành `rel-54-17-extend` | `uc-72`: `UC-72 / Open / Publish Vote` → `UC-72 / Open Vote`; `uc-76` theo PO-Q2 (xem dưới) |
| `uc-secretary.drawio` | `uc-26`, `uc-27`, `uc-28`, `uc-53` | `rel-26-53-extend`, `rel-27-53-extend`, `rel-28-53-extend`, `rel-53-17-extend` | `rel-54-53-extend` → target `uc-17`, id `rel-54-17-extend` | `uc-76` theo PO-Q2 |
| `uc-meeting-organizer.drawio` | `uc-53` | `rel-53-17-extend` | `rel-54-53-extend` → target `uc-17` | `uc-76` theo PO-Q2 |
| `uc-meeting-approver.drawio` | `uc-53` | `rel-53-17-extend` | `rel-54-53-extend` → target `uc-17` | `uc-76` theo PO-Q2 |
| `uc-participant.drawio` | `uc-53` | `rel-53-17-extend` | `rel-54-53-extend` → target `uc-17` | `uc-76` theo PO-Q2 |
| `uc-system-admin.drawio` | `uc-53` | `rel-53-17-extend` | `rel-54-53-extend` → target `uc-17` | `uc-76` theo PO-Q2 |
| `uc-guest.drawio` | — | — | — | — (không có UC liên quan) |

UC-76 theo PO-Q2:
- Nếu PO chọn **"Record Vote Conclusion"**: trong `uc-chair.drawio` đổi nhãn `uc-76` thành `UC-76 / Record Vote Conclusion` (giữ `rel-76-70-extend`); trong 5 file còn lại (`secretary`, `organizer`, `approver`, `participant`, `system-admin`) xoá cell `uc-76` và edge `rel-76-70-extend` (xem kết quả đã nằm trong UC-74).
- Nếu PO chọn **bỏ**: xoá `uc-76` và `rel-76-70-extend` trong cả 6 file.
- Chưa có quyết định: giữ nguyên `uc-76`.

Sau khi xoá oval: kéo các oval còn lại lấp chỗ trống (trong khung "system boundary"), không đổi vị trí actor. Xuất `.png` và `.pdf` cùng tên.

`docs/assets/use-case-diagrams/README.md` — 1 thay đổi: nối vào cuối dòng 3:
` Cập nhật 09/10/2026: bỏ UC-26–30 và UC-53 (không còn dùng — ADR-0013), UC-54 \`<<extend>>\` UC-17, UC-72 đổi tên "Open Vote", UC-76 chờ PO.`

### 6.2 `docs/assets/diagrams/meeting-management/` [A] — 2 file `.mmd` sửa, 2 PNG xuất lại

| File | Dòng | Câu hiện tại | Thay bằng |
|---|---|---|---|
| `PublishMeeting.mmd` | 41 | `    Note over Service,DB: Meeting Session là tùy chọn và không phải điều kiện Publish theo BR-56.` | `    Note over Service,DB: Publish không có điều kiện về lịch; lịch dự kiến nằm trên Meeting (ADR-0013).` |
| `SubmitMeetingForApproval.mmd` | 84 | `… Agenda và Meeting Session không bắt buộc khi submit.` | `… Agenda không bắt buộc khi submit.` |

`ReviewMeetingApproval.mmd`: không đổi (chữ "phiên đăng nhập"). Xuất PNG: `npx @mermaid-js/mermaid-cli -i <file>.mmd -o <file>.png` (hoặc công cụ nhóm đang dùng).

### 6.3 `docs/assets/erd-hien-tai/` [B] — không sửa tay

`trustcabinet-erd-hien-tai.drawio/.png/.pdf` và `01-tong-quan.png` là ảnh schema **hiện tại** sinh từ database; bản đang có đã cũ (vẫn có `meetings.starts_at/ends_at`, chưa có `meeting_sessions`). Sinh lại sau khi migration bỏ Meeting Session chạy. Không file Markdown nào trỏ tới thư mục này.

---

## 7. `docs/00-overview/`, `docs/04-meetings/`, `docs/README.md`, `README.md`, `AGENTS.md`, skills

### 7.1 `docs/00-overview/README.md` [A] — 1 thay đổi

| Dòng | Câu hiện tại | Thay bằng |
|---|---|---|
| 9 | `- \`03-thuat-ngu.md\` — từ điển thuật ngữ (phiên họp, nghị sự, biên bản, biểu quyết…)` | `- \`03-thuat-ngu.md\` — từ điển thuật ngữ (cuộc họp, mục nghị sự, biên bản, biểu quyết…)` |

### 7.2 `docs/00-overview/04-so-sanh-ai-ecabinet.md` [A] — 1 thay đổi

Chèn sau dòng 21 (`- **Từ vựng nghiệp vụ và vòng đời cuộc họp** — …`):

```markdown
- **Một cuộc họp là một buổi họp** — chữ "phiên" trên giao diện AI-eCabinet là agenda item; biểu quyết diễn ra trong cuộc họp, chủ trì ghi kết luận ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md), [ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md))
```

(Ngoài phạm vi audit nhưng thấy khi đọc: dòng 34 ghi "WebRTC + SignalR" — đã thay bằng LiveKit theo ADR-0010.)

### 7.3 `docs/04-meetings/` [A] — 1 file mới (khuyến nghị)

Theo quy ước của chính thư mục: tạo `docs/04-meetings/2026-10-09-po-chot-bo-meeting-session.md` gồm: ngày 2026-10-09; người tham dự (PO + nhóm — điền tên thật); quyết định D1, D2, D3 (chép từ `docs/09-bussinessflow/README.md` mục "Thay đổi 09/10/2026"); câu hỏi còn mở PO-Q1…PO-Q6; việc được giao (BA: Report 1/3, Inventory, UC Spec, BR; BE: migration; FE: bỏ tab Phiên họp, lịch tuần). `docs/04-meetings/README.md` không đổi.

### 7.4 `docs/README.md` [A] — 1 thay đổi (thiếu sẵn, không do D1–D3)

Chèn sau dòng 26 (`| [08-reports/](08-reports/) | … |`):

`| [09-bussinessflow/](09-bussinessflow/) | Swimlane Business Flow BF-01 → BF-04 (\`.drawio\` và PNG) |`

### 7.5 `README.md` (gốc) — 4 thay đổi

| # | Pha | Dòng | Câu hiện tại | Thay bằng |
|---|---|---|---|---|
| 1 | [A] | 145 | `- (S) Biểu quyết bất đồng bộ (lấy ý kiến trước)` | **Xoá dòng** |
| 2 | [A] | 150 | `- (M) Biểu quyết trực tiếp, hiển thị kết quả` | `- (M) Biểu quyết trong cuộc họp, hiển thị kết quả, chủ toạ ghi kết luận` |
| 3 | [A] | 151 | `- (M) Ghi âm phiên họp hoặc nhập file có sẵn` | `- (M) Thư ký tải lên file ghi âm cuộc họp` |
| 4 | [A] | sau 185 | (cuối mục "Không làm") | Chèn gạch: `- Biểu quyết trước hoặc sau cuộc họp (lấy ý kiến có hạn chót), quorum và nghị quyết bất biến riêng — biểu quyết chỉ diễn ra trong cuộc họp, chủ toạ ghi kết luận Thông qua / Không thông qua / Hoãn ([ADR-0014](docs/05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md)).` |
| 5 | [B] | 9, 135 | `cơ sở dữ liệu 28 bảng` / `28 bảng — xem …` | `27 bảng` (sau khi drop `meeting_sessions`; đếm lại `DbSet<` trong `TrustCabinetDbContext.cs` — hiện là 28) |

Dòng 142 `(M) Tạo lịch họp, gửi thư mời, xác nhận tham dự` giữ nguyên.

### 7.6 `AGENTS.md` (tiếng Anh) — 2 thay đổi

| # | Pha | Dòng | Hành động |
|---|---|---|---|
| 1 | [A] | sau 241 (cuối gạch "One organisation, no org units") | Chèn gạch mới (khối 7.6-a) |
| 2 | [B] | 13 | `a 27-table EF Core model` — đếm lại sau migration (README gốc ghi 28, AGENTS ghi 27, code hiện có 28 `DbSet`; sau khi drop `meeting_sessions` là 27). Sửa README và AGENTS cho cùng một số. |

Khối 7.6-a:

```markdown
- **One meeting is one sitting.** There is no `MeetingSession` and no meeting-session use case
  (UC-26–30 and UC-53 are retired; their numbers are never reused). A meeting carries its own
  planned schedule, mode, location/online link and lifecycle; after `Published` the organizer may
  only reschedule (time, location, link). Voting happens only while the meeting is `InProgress`: no
  pre- or post-meeting or time-boxed votes, no quorum, no separate Resolution — the Chair records
  Passed / Not passed / Deferred. See [ADR-0013](docs/05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)
  and [ADR-0014](docs/05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md). The `meeting_sessions`
  table still in the code is removed by a new migration; do not build on it.
```

### 7.7 `.claude/skills/frontend-ui-design/SKILL.md` (tiếng Anh) [A] — 2 thay đổi

| # | Dòng | Câu hiện tại | Thay bằng |
|---|---|---|---|
| 1 | 128–129 | `- Events everyone must notice (a vote opens, someone is invited to speak, recording starts, a` / `  meeting session starts or ends) are published on the hub so the room can show them.` | `- Events everyone must notice (a vote opens, someone is invited to speak, recording starts, the` / `  meeting starts or ends) are published on the hub so the room can show them.` |
| 2 | 130–141 | gạch `- A meeting can have several **meeting sessions** (Report 3). …` tới `` `MeetingSession` with `CallSession`, which is one run of the LiveKit room. `` | Thay bằng khối 7.7-a |

Khối 7.7-a:

```markdown
- A meeting is **one sitting** — there are no meeting sessions
  ([ADR-0013](../../../docs/05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)). Split actions by the
  meeting's status, not by a session:
  - **Planning actions** (agenda create/update/delete/reorder) run from the detail page while the
    meeting's status allows planning; the server decides and refuses otherwise.
  - **Live actions** in the room (current agenda item, attendance, speaking, voting) need the
    meeting to be `InProgress` and are refused otherwise.
  The panel takes only `meetingId`. Reload on meeting started/ended events. `CallSession` is one run
  of the LiveKit room, not a meeting.
```

### 7.8 Không cần sửa

`docs/DEVELOPMENT_WORKFLOW.md` (chữ "working session" là nghĩa thường), `.claude/skills/verify-ui-in-browser/SKILL.md` (session = phiên trình duyệt), `docs/04-meetings/README.md`.

---

## 8. Ngoài phạm vi audit này nhưng là phụ thuộc (để agent/BA khác xử lý)

- `docs/01-requirements/06-business-rules.md`: BR-25, BR-30 (lọc theo lịch Meeting), BR-54–BR-63 (Session — "không còn dùng"), BR-64, BR-65, BR-67, BR-68, BR-69, BR-71, BR-72, BR-73, BR-74 (đổi "Meeting Session" → "Meeting"), BR-75, BR-76 (đổi phạm vi sang Reschedule Meeting), BR-77 (không còn dùng). Sequence trong mục 4 vẫn dẫn BR-62/64/65/68/69/71–76 với nghĩa mới.
- `docs/01-requirements/00-master-use-case-inventory.md`, `05-use-case-specifications.md`, `docs/08-reports/Report-1/2/3`: FE-05, FE-06, FE-12, FE-13, FE-16, GAP-05.
- `docs/09-bussinessflow/README.md` mục "chưa đồng bộ vào docs": đánh dấu xong các mục đã áp dụng.
- Jira: SCRUM-68 (API phiên họp), SCRUM-70, SCRUM-24 (lấy ý kiến trước họp), SCRUM-28 — ghi comment theo DEVELOPMENT_WORKFLOW §2.1.
- `AGENTS.md` nhắc `frontend/src/pages/scopeData.ts` nhưng **file không tồn tại** — cần sửa AGENTS hoặc tạo lại file (lệch có sẵn).

---

## 9. Code — để sau (chỉ liệt kê file + symbol; làm qua migration mới, không sửa migration cũ)

Backend — Domain
- `backend/TrustCabinet.Domain/Entities/MeetingSession.cs` — `MeetingSession` (xoá)
- `backend/TrustCabinet.Domain/Enums/MeetingSessionStatus.cs` — `MeetingSessionStatus` (xoá)
- `backend/TrustCabinet.Domain/Meetings/MeetingSessionRules.cs` — `MeetingSessionRules` (xoá)
- `backend/TrustCabinet.Domain/Entities/Meeting.cs` — `Sessions` (xoá); thêm `PlannedStartTime`, `PlannedEndTime`, `ActualStartedAt`, `ActualEndedAt`, `OnlineLink`
- `backend/TrustCabinet.Domain/Meetings/MeetingContracts.cs` — `MeetingInput`, `ValidatedMeetingInput`, `MeetingDetails`
- `backend/TrustCabinet.Domain/Meetings/MeetingListContracts.cs` — `MeetingListQuery`, `MeetingListItem`
- `backend/TrustCabinet.Domain/Meetings/MeetingRules.cs` — validate lịch
- `backend/TrustCabinet.Domain/Meetings/MeetingSubmissionReadinessRules.cs` — readiness gồm lịch
- `backend/TrustCabinet.Domain/Entities/VotingSession.cs` — `Kind`, `OpensAt`, `ClosesAt`, `Conclusion`, `EligibleCount` (comment)
- `backend/TrustCabinet.Domain/Enums/VotingEnums.cs` — `VotingKind` (`Live`, `Asynchronous`)
- `backend/TrustCabinet.Domain/Enums/AgendaEnums.cs` — `AttendanceStatus` (comment "quorum")
- `backend/TrustCabinet.Domain/Entities/MeetingParticipant.cs` — `Attendance` (comment "quorum")

Backend — Infrastructure / Web
- `backend/TrustCabinet.Infrastructure/TrustCabinetDbContext.cs` — `MeetingSessions`
- `backend/TrustCabinet.Infrastructure/Configurations/MeetingSessionConfiguration.cs` — `MeetingSessionConfiguration` (xoá)
- `backend/TrustCabinet.Infrastructure/Configurations/MeetingConfiguration.cs` — cột lịch mới (file chưa đọc, suy theo quy ước)
- `backend/TrustCabinet.Infrastructure/Meetings/MeetingRepository.cs` — lọc `from`/`to` qua `Sessions`/`MeetingSessionStatus`
- `backend/TrustCabinet.Infrastructure/Meetings/MeetingService.cs` — create/update/reschedule, publish
- `backend/TrustCabinet.Infrastructure/Notifications/NotificationRepository.cs` — `MeetingSessions`, `MeetingSessionStatus` (nhắc "sắp bắt đầu")
- `backend/TrustCabinet.Infrastructure/Seed/DevelopmentDataSeeder.cs` — `MeetingSession` demo
- `backend/TrustCabinet.Infrastructure/Migrations/` — **migration mới** (vd. `MeetingScheduleBackOnMeeting`) + `TrustCabinetDbContextModelSnapshot.cs`; không sửa `20261002070951_MeetingSessionsForPublication`, `20261004095457_MoveMeetingScheduleToSessions`
- `backend/TrustCabinet.Web/Endpoints/MeetingEndpoints.cs` — request/response DTO, `Msg138…` hằng mới
- `backend/TrustCabinet.Web/Endpoints/MeetingListQueryParser.cs` — `sortBy` (`plannedStartTime`), `from`/`to`

Frontend
- `frontend/src/api/meetingSessions.ts` — `listMeetingSessions` (xoá)
- `frontend/src/types/meetingSession.ts` — `MeetingSession`, `MeetingSessionStatus` (xoá)
- `frontend/src/types/meeting.ts` — `Meeting`, `MeetingListItem`, `MeetingSortBy`, `MeetingRequest`, `EDITABLE_STATUSES`
- `frontend/src/api/meetings.ts` — `createMeeting`, `updateMeeting`, `listMeetings`
- `frontend/src/features/meetings/MeetingForm.tsx`, `CreateMeetingPage.tsx`, `EditMeetingPage.tsx` — trường lịch, form dời lịch
- `frontend/src/features/meetings/MeetingsPage.tsx` — nhãn "Có phiên họp diễn ra từ/đến ngày", cột thời gian
- `frontend/src/features/meetings/listParams.ts` — `ListState`, `toQuery`, sort
- `frontend/src/features/meetings/detail/MeetingDetailPage.tsx` — tab `sessions` "Phiên họp", `reloadSession`
- `frontend/src/features/calendar/useWeekSchedule.ts` — `listMeetingSessions`
- `frontend/src/features/calendar/CalendarPage.tsx` — thông báo "backend chưa có API phiên họp", `Sessions`

Tests
- `tests/TrustCabinet.Web.Tests/Meetings/MeetingSessionRepositoryTests.cs` — `MeetingSessionRepositoryTests` (xoá/viết lại)
- `tests/TrustCabinet.Web.Tests/Meetings/MeetingSessionMigrationTests.cs` — `MeetingSessionMigrationTests` (giữ cho migration cũ hoặc thay bằng test migration mới)
- `tests/TrustCabinet.Web.Tests/Meetings/MeetingSessionRulesTests.cs` — `MeetingSessionRulesTests` (xoá)
- `tests/TrustCabinet.Web.Tests/Meetings/MeetingEndpointsTests.cs` — `AddPlannedSession`, `Publish_without_a_meeting_session_returns_MSG…`
- `tests/TrustCabinet.Web.Tests/Meetings/MeetingWorkflowServiceTests.cs` — `PlannedSession`, `Publish_does_not_require_a_meeting_session`
- `tests/TrustCabinet.Web.Tests/Meetings/MeetingRepositoryTests.cs` — `MeetingSessions`, `noSessions`
- `tests/TrustCabinet.Web.Tests/Meetings/MeetingSubmissionReadinessTests.cs` — `MeetingSession`
- `tests/TrustCabinet.Web.Tests/Meetings/MeetingTestDoubles.cs` — `MeetingSession`, `Sessions`
- `tests/TrustCabinet.Web.Tests/Notifications/NotificationServiceTests.cs`, `NotificationRepositoryPostgresTests.cs` — `MeetingSession`
- `tests/TrustCabinet.Web.Tests/DatabaseModelTests.cs` — `meeting_sessions`, `MeetingSession`
- `tests/TrustCabinet.Web.Tests/MessageCodeCatalogueTests.cs` — `Meeting_workflow_preserves_main_code_reservations` (InlineData MSG48–MSG77: vẫn chạy được nếu giữ cột UC; cân nhắc thêm test "mã ngừng dùng không xuất hiện trong code")

---

## 10. Câu hỏi mở (PO / nhóm)

- **PO-Q1** Cancel Meeting: có thêm UC hủy cuộc họp không? (mặc định: không; giữ chữ "hủy họp" ở ERD dòng 444 cho tới khi chốt)
- **PO-Q2** UC-76: đổi thành "Record Vote Conclusion" hay bỏ? (mặc định: giữ oval UC-76 nguyên trạng trên use case diagram; BF-03 đã có bước Chair ghi kết luận)
- **PO-Q3** Ai được dời lịch sau Published: chỉ Organizer hay cả Chair? (mặc định: chỉ Organizer)
- **PO-Q4** `OnlineLink`: có lưu link ngoài không, khi phòng họp là LiveKit trong ứng dụng? (mặc định: có, tùy chọn, tối đa 1.000 ký tự)
- **PO-Q5** Agenda được sửa ở trạng thái nào? AI-eCabinet chỉ cho sửa ở `Draft`/`Rejected` (khoá khi gửi duyệt); BR-74 cũ cho tới khi phiên bắt đầu. (mặc định trong sequence: "meeting status permits agenda planning", để BR quyết)
- **PO-Q6** `PlannedEndTime` bắt buộc? (mặc định: bắt buộc; AI-eCabinet cho `ends_at` tùy chọn)

Lệch có sẵn phát hiện khi audit (không do D1–D3): ADR-0010/kế hoạch dùng `MSG65` cho "phòng chưa mở" trong khi catalogue cấp `MSG65` cho UC-36 và `MSG117` cho phòng không khả dụng; index ADR ghi 0010/0011 "Đề xuất" nhưng file ghi "Đã chấp nhận"; `docs/README.md` thiếu `09-bussinessflow/`; README gốc 28 bảng vs AGENTS 27 bảng; `frontend/src/pages/scopeData.ts` không tồn tại; câu SQL mẫu ở `03-co-so-du-lieu.md` dòng 162 dùng cột `starts_at` đã bị drop.
