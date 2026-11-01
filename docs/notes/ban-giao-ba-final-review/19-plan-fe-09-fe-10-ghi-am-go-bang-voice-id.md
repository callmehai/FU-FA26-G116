# Plan: tổ chức lại FE-09 / FE-10 theo code ghi âm, gỡ băng, Voice ID — BA Final Review

**Người lập:** KhanhPG (BA), 30/10/2026. **Người thực hiện:** agent (Gemini), từng lượt một.
**Ticket:** SCRUM-29, SCRUM-31, SCRUM-35, SCRUM-109 (và SCRUM-102, SCRUM-103 — đặc tả UC-43–51).
**Nguồn yêu cầu cuối:** [note 16](16-ghi-am-voice-id-28-29-10-2026.md) (thay §3–§5 note 11 và phần lưu trữ note 03).
Stack PR #184 → #193 **đã merge vào `main`** (ADR-0040 → ADR-0042 có trên `origin/main`), nên làm được ngay.

**Phạm vi plan này:** UC Specification + Business Rules + System Messages trong Report 3, inventory, Sequence Diagram,
Class Diagram của FE-09 và FE-10. **Ngoài phạm vi** (ghi vào §9, không làm): Report 3 §III màn hình, Entity Descriptions,
Permission Matrix, context diagram, UC diagram, state machine, ERD drawio.

---

## 0. Agent đọc trước khi làm (bắt buộc)

1. [`AGENTS.md`](../../../AGENTS.md) — đặc biệt mục *Reports — hard rule* và *Language rules*.
2. [`docs/08-reports/README.md`](../../08-reports/README.md) — sửa DOCX, **không** sửa `Report-3_….md`,
   `05-use-case-specifications.md`, `06-business-rules.md` bằng tay.
3. [`docs/01-requirements/README.md`](../../01-requirements/README.md) §2–§4 — quy ước AF/EX, BR, MSG, checklist.
4. [`docs/06-sequencediagram/README.md`](../../06-sequencediagram/README.md) — quy ước sequence (một request Actor → Boundary,
   thứ tự lifeline, nhãn `MSGnn – "…"` ở mọi tầng trả về).
5. [`docs/07-classdiagram/README.md`](../../07-classdiagram/README.md) §2–§5 — quy trình DCD, Gates A–F; mẫu bố cục FE-11.
6. [`DRAWIO_EDITING_RULES.md`](../../../DRAWIO_EDITING_RULES.md) + [drawio-skill](../../../.agents/skills/drawio-skill/SKILL.md).
7. Nguồn as-built (đọc, không sửa):
   - API: [`03-api/12-ban-ghi-cuoc-hop.md`](../../03-api/12-ban-ghi-cuoc-hop.md), [`03-api/13-nhan-dien-giong-noi.md`](../../03-api/13-nhan-dien-giong-noi.md)
   - MSG: [`03-api/00-danh-muc-ma-thong-bao.md`](../../03-api/00-danh-muc-ma-thong-bao.md) (dải MSG290–MSG323, MSG351–MSG367)
   - ADR: 0026, 0027, 0029, 0037–0042 trong [`05-decisions/`](../../05-decisions/)
   - Code: `backend/TrustCabinet.Web/Endpoints/{Recording,SpeechClip,Transcript,VoiceId}Endpoints.cs`,
     `backend/TrustCabinet.Domain/{Recordings,Transcription,VoiceId}/*.cs`, `Domain/Entities/{Recording,SpeechClip,Transcript*,Voice*}.cs`,
     `frontend/src/features/{recordings,speech,voice}/`

**Quy tắc viết Report 3** (BA đã chốt): chỉ ngôn ngữ nghiệp vụ, tiếng Anh. Không tên nhà cung cấp/công nghệ (IndexedDB, WAV,
HMAC, worker, Tailscale, gipformer…), không endpoint, HTTP code, bảng/cột, khoá cấu hình. Gọi dịch vụ ngoài là
"Speech Recognition Service" (chạy trong hệ thống, không phải bên thứ ba).

---

## 1. Vì sao phải tổ chức lại

| Khía cạnh | FE-09/FE-10 hiện trong Report 3 | Code as-built |
|---|---|---|
| Nguồn ghi | Thư ký tải tệp lên sau khi `Ended` | Suy ra từ hình thức họp (ADR-0040): `InPerson` mic phòng, `Online` mic từng người, `Hybrid` cả hai; tải tệp vẫn còn |
| Mic phòng | Không có | Thư ký bật/tắt từng lượt, nối thành **một** bản ghi, khôi phục khi đóng tab, xoá lượt, tự lưu lượt bỏ dở sau 12 giờ |
| Mic từng người | Không có | Nút mic là công tắc duy nhất; câu giữ trên máy, gửi trễ tới 7 ngày; "Ghi âm của tôi"; lọt tiếng; loại câu |
| Gỡ băng | UC-45/46 placeholder | Tự xếp hàng sau khi lưu, chạy lại, sửa chữ, xuất `.txt` |
| Voice ID | "chưa đánh số" | Đồng ý riêng (thông báo 1.1), ≤ 5 giọng mẫu, nghe lại mẫu, rút đồng ý; gợi ý tên chỉ Thư ký thấy, nhiều giọng một người |
| Tóm tắt (UC-47–51) | Placeholder | **Không có code** (bảng `meeting_summaries` có, không endpoint) |

Kết luận: FE-09 không còn là "upload & thư viện media" mà là **ghi âm cuộc họp**; FE-10 là **gỡ băng + nhận diện người nói**.
Tóm tắt tách hẳn ra **FE-11 Meeting Summary** (D3), vẫn để chờ; FE-11 → FE-15 cũ dời thành FE-12 → FE-16.

---

## 2. Quyết định tổ chức (mặc định — agent làm theo, BA xác nhận khi review)

| # | Quyết định | Lý do |
|---|---|---|
| D1 | **Giữ số UC-43 → UC-46**, đổi nội dung và tách UC con chữ cái (như FE-07). Không đánh lại số. | Jira, sequence, Report 3 đã dẫn chiếu UC-43–46 |
| D2 | Hành vi mới có **actor khác** (người nói, mọi người dùng) cấp số mới từ **UC-80** | Inventory: số kế tiếp là UC-80. Actor khác ⇒ UC riêng |
| D3 | **Tóm tắt (UC-47 → UC-51) sẽ thành FE-11 Meeting Summary**, FE-11 → FE-15 cũ dời +1 (bảng §3a). **Hoãn** (BA 30/10/2026): đợt này UC-47–51 vẫn nằm dưới FE-10, giữ Placeholder | Ưu tiên chuẩn đặc tả trước; đổi số FE làm sau trong Lượt 0 |
| D4 | Đổi tên feature: FE-09 **Meeting Recording**; FE-10 **Transcription & Speaker Identification** (khi chạy Lượt 0). Đợt này FE-10 tạm tên **Transcription, Speaker Identification & Meeting Summary** | Tên phải nói đúng phạm vi |
| D5 | Việc hệ thống tự chạy (xoá sau 180 ngày, tự lưu lượt sau 12 giờ, tự xếp hàng gỡ băng) là **BR + AF**, không thành UC riêng | Giống BR-301 tự huỷ cuộc họp nằm trong UC-75 |
| D6 | **Guest không được ghi theo micro và không đọc được bản ghi.** Đã kiểm code: mọi route `speech-clips` và `recordings` chặn bằng `RequireActiveUser` (chỉ tài khoản nội bộ). Lời Guest chỉ vào bản ghi qua mic phòng (họp Hybrid). **NEEDS REVIEW PO**: họp Online có Guest thì lời Guest không có trong bản ghi | Code `SpeechClipEndpoints.cs`, `RecordingEndpoints.cs`; quyền Guest chờ SCRUM-59 |
| D7 | Gỡ băng nền (hàng chờ) có **một sequence hệ thống** riêng, ngoại lệ quy ước "một request Actor" (như UC-04) | Đây là luồng chính tạo ra transcript |
| D8 | Khi chạy Lượt 0: đánh lại số FE **chỉ ở tài liệu sống**; không sửa ADR, migration, biên bản họp, note cũ, wireframe v4–v9 | ADR và migration bất biến |
| D9 | **Ghi lời qua mic của mình không phải UC riêng.** Người dự không chủ động "ghi âm": họ bật mic (UC-39a) và hệ thống tự gửi từng câu. Viết thành phần của **UC-39a** (AF, EX, BR) + một sequence riêng cho luồng gửi câu | Kiểm "UC = actor tương tác để đạt mục tiêu": mục tiêu của actor là nói trong cuộc họp, ghi là tác dụng phụ |

---

## 3. Danh sách UC (chuẩn cho mọi artifact)

Kiểm từng UC: có **thao tác của actor** trên màn hình thật và **endpoint thật** phía sau. Cột "Code" là bằng chứng
(đối chiếu 30/10/2026 trên `main` 9442794).

### FE-09 — Meeting Recording *(Ghi âm cuộc họp)*

| UC | Tên (EN) | Actor chính | Quan hệ | Thao tác của actor → Code | MSG dùng |
|---|---|---|---|---|---|
| UC-43 | View & Play Meeting Recordings | — | gom 43a–43c | — | — |
| UC-43a | View Meeting Recordings | Chair, Secretary, Participant hiện hành | `<<extend>>` UC-16 | Mở tab Bản ghi → `GET …/speech-clips`, `GET …/recordings` | MSG297, MSG299 |
| UC-43b | Play Meeting Recording | như 43a | `<<extend>>` UC-43a | Bấm nghe bản ghi phòng hoặc một câu → `GET /api/recordings/{id}/playback`, `GET /api/speech-clips/{id}/playback` | MSG297 |
| UC-43c | Download Meeting Recording | Chair, Secretary | `<<extend>>` UC-43a | Bấm tải tệp → `GET /api/recordings/{id}/download` | MSG355, MSG356 |
| UC-44 | Record Room Audio | — | gom 44a–44e | — | — |
| UC-44a | Start Room Recording | Secretary | `<<extend>>` UC-43a | Bật mic phòng (thẻ ghi âm nổi) → `POST …/recordings/takes`, `PUT …/parts/{n}` | MSG291, MSG293, MSG295, MSG358–MSG361, MSG298 |
| UC-44b | Stop and Save Room Recording | Secretary; System (tự lưu) | `<<extend>>` UC-43a | Tắt mic phòng / "Lưu lượt đó" / "Lưu phần đã ghi" → `POST /api/recordings/{id}/complete` | MSG363, MSG365, MSG362, MSG295, MSG298 |
| UC-44c | Upload Meeting Audio File | Secretary | `<<extend>>` UC-43a | Chọn tệp, xác nhận đã thông báo → `POST …/recordings` | MSG290–MSG295, MSG298, MSG358 |
| UC-44d | Delete Recording Take | Secretary | `<<extend>>` UC-43a | Xoá một lượt → `DELETE /api/recordings/{id}/takes/{n}` | MSG366, MSG367, MSG295 |
| UC-44e | Erase Meeting Recording | Secretary; System (hết hạn) | `<<extend>>` UC-43a | Xoá bản ghi → `DELETE /api/recordings/{id}` | MSG296, MSG364, MSG295, MSG298 |
| UC-80 | Manage My Recorded Speech | Chair, Secretary, Participant hiện hành (không Guest, D6) | `<<extend>>` UC-37, UC-43a | Mở "Ghi âm của tôi" (trong phòng hoặc tab Bản ghi): xem câu chờ gửi và đã lưu, nghe, xoá câu của mình → `GET …/speech-clips`, `DELETE /api/speech-clips/{id}` | MSG322, MSG323, MSG304 |

Ghi chú nội dung bắt buộc:
- **UC-43a**: một màn có hai khối (code `MeetingDetailPage` tab `recordings`: `SpeechPanel` trên, `RecordingsPanel` dưới):
  bản ghi theo micro (dòng thời gian câu có chữ, người nói, dấu "Lẫn tiếng"; câu đã loại hiện trống với người không phải Chủ tọa/Thư ký)
  và danh sách bản ghi phòng (trạng thái *Đang ghi / Có sẵn / Đã xoá*, nguồn mic phòng/tải lên, hạn xoá, các lượt, trạng thái gỡ băng).
  Họp Online không có khối tải tệp/bật mic phòng. Chip "Đang ghi âm" cho mọi người. Người tạo/người duyệt không có trong thành phần: không thấy.
- **UC-44a**: hỏi xác nhận "đã thông báo, không ai phản đối" + mục đích **chỉ trước lượt đầu**; một lượt mở mỗi cuộc họp;
  phần chưa gửi giữ trên máy. AF: mở lại tab đã đóng → *Ghi tiếp* (tiếp UC-44a) hoặc *Lưu phần đã ghi* (UC-44b). Không có chữ chạy khi đang ghi.
- **UC-44b**: lượt nối vào bản ghi phòng duy nhất, cả bản gỡ băng lại (include UC-45c). AF: Thư ký "Lưu lượt đó" cho lượt còn mở ở máy khác;
  AF: lượt không có tiếng → bỏ (MSG365); AF hệ thống: lượt không nhận tiếng 12 giờ thì tự lưu.
- **UC-44c**: chỉ `InPerson`/`Hybrid`, khi `InProgress` hoặc `Ended` (**không còn** "chỉ sau Ended"); định dạng mp3, m4a, aac, wav, webm, ogg.
- **UC-44d**: cắt tiếng + câu của lượt; giữ chữ đã sửa và tên đã gán của lượt khác; còn một lượt thì phải xoá cả bản (MSG367).
- **UC-44e**: xoá âm thanh + bản gỡ băng, giữ dấu vết "đã có bản ghi, xoá ngày …, bởi ai/hết hạn". AF hệ thống: hết 180 ngày.
- **UC-80**: câu đang chờ gửi (còn trên máy) và câu đã lưu; nghe lại; người nói rút câu của mình (xoá cả chữ và âm thanh, ghi nhật ký).
- **UC-39a (FE-08, sửa thêm — D9)**: giữ đặc tả đã có, thêm AF "Speech is recorded sentence by sentence while the microphone is on"
  (câu im lặng không lưu; câu lọt tiếng được đánh dấu, không ẩn), EX "Speech Recognition Service unavailable — sentences stay on the
  device and are sent later" (MSG312), EX "The Meeting cannot be recorded now" (MSG320); dẫn BR-352, BR-354, BR-356.
  UC-37 bước 3 và POST-4 UC-39a trỏ "UC-39a, UC-80" thay vì "FE-09".

### FE-10 — Transcription, Speaker Identification & Meeting Summary *(Gỡ băng, nhận diện người nói và tóm tắt)*

| UC | Tên (EN) | Actor chính | Quan hệ | Thao tác của actor → Code | MSG dùng |
|---|---|---|---|---|---|
| UC-45 | View Meeting Transcript | — | gom 45a–45c | — | — |
| UC-45a | View Recording Transcript | Chair, Secretary, Participant hiện hành | `<<extend>>` UC-43a | Mở bản gỡ băng của một bản ghi phòng → `GET /api/recordings/{id}/transcript` | MSG299 |
| UC-45b | Export Meeting Transcript | Chair, Secretary | `<<extend>>` UC-43a | Tải `.txt` của bản ghi phòng hoặc bản ghi theo micro → `GET …/transcript.txt`, `GET …/speech-clips.txt` | MSG355, MSG356, MSG357 |
| UC-45c | Transcribe Meeting Recording | Secretary; System; Speech Recognition Service | `<<extend>>` UC-45a; `<<include>>` từ UC-44b, UC-44c | Bấm chuyển chữ / chuyển chữ lại → `POST /api/recordings/{id}/transcribe` | MSG300, MSG301, MSG304 |
| UC-46 | Edit Transcript & Speaker Attribution | — | gom 46a–46d | — | — |
| UC-46a | Edit Transcript Text | Secretary | `<<extend>>` UC-43a, UC-45a | Sửa chữ một đoạn hoặc một câu → `PUT /api/transcript-segments/{id}`, `PUT /api/speech-clips/{id}/text` | MSG302, MSG303, MSG304, MSG295 |
| UC-46b | Assign Speaker | Secretary | `<<extend>>` UC-45a | Gán / bỏ gán tên một giọng (có gợi ý Voice ID) → `PUT /api/recordings/{id}/speakers/{label}` | MSG316, MSG317, MSG295 |
| UC-46c | Exclude or Restore a Sentence | Chair, Secretary | `<<extend>>` UC-43a | Loại câu (có lý do) / đưa lại → `PUT /api/speech-clips/{id}/exclusion` | MSG351–MSG354 |
| UC-46d | Delete a Sentence | Secretary | `<<extend>>` UC-43a | Xoá câu của người khác trên dòng thời gian → `DELETE /api/speech-clips/{id}` | MSG322, MSG323, MSG304 |
| UC-47 → UC-51 | (giữ tên cũ) | — | — | Không có code | **Placeholder, chưa triển khai** (D3) |
| UC-81 | Manage My Voice ID | — | gom 81a–81f | — | — |
| UC-81a | View My Voice ID | Internal user | Độc lập (menu "Giọng nói của tôi") | Mở trang → `GET /api/me/voice` | MSG314 |
| UC-81b | Give Voice ID Consent | Internal user | `<<extend>>` UC-81a | Đọc thông báo, tick đồng ý / "Đồng ý điều khoản mới" → `POST /api/me/voice/consent` | MSG305, MSG313, MSG315 |
| UC-81c | Record Voice Sample | Internal user; Speech Recognition Service | `<<extend>>` UC-81a | Đọc đoạn mẫu vào mic, lưu → `POST /api/me/voice/samples` | MSG307–MSG310, MSG312, MSG292, MSG313, MSG315 |
| UC-81d | Play Voice Sample | Internal user | `<<extend>>` UC-81a | Bấm nghe mẫu của mình → `GET /api/me/voice/samples/{id}/audio` | — |
| UC-81e | Delete Voice Sample | Internal user | `<<extend>>` UC-81a | Xoá một mẫu → `DELETE /api/me/voice/samples/{id}` | MSG311, MSG315 |
| UC-81f | Withdraw Voice ID Consent | Internal user | `<<extend>>` UC-81a | Rút đồng ý, xác nhận → `DELETE /api/me/voice/consent` | MSG306, MSG315 |

Ghi chú nội dung bắt buộc:
- **UC-45a**: trạng thái *Đang chờ / Đang chuyển / Xong / Lỗi*; đoạn có giờ, tên người nói (*Người nói N* hoặc tên đã gán),
  vạch phân lượt; chữ do người sửa phân biệt với chữ máy. Gợi ý tên **chỉ Thư ký thấy**. Bản ghi theo micro không có UC "xem gỡ băng"
  riêng: chữ của nó hiện ngay ở UC-43a.
- **UC-45b**: chỉ khi bản gỡ băng đã xong; cuộc họp Mật thì không ai tải; câu đã loại không có trong tệp; ghi nhật ký.
- **UC-45c**: Thư ký yêu cầu lần đầu / khi lỗi / **làm lại bản đã xong** (hỏi xác nhận vì thay hết chữ đã sửa và tên đã gán).
  AF hệ thống: tự xếp hàng sau mỗi lần lưu bản ghi. Âm thanh không rời khỏi hệ thống. Có dấu câu và chữ hoa.
- **UC-46b**: gán một lần áp dụng mọi đoạn của giọng đó; bỏ gán được; người được gán phải đang trong thành phần; AF: "Cùng giọng
  với Người nói N" — gán cả nhóm một lần; hệ thống không tự gán.
- **UC-46c**: lý do *Lẫn tiếng* / *Khác*; câu không bị xoá; chỉ Chủ tọa/Thư ký còn thấy chữ và nghe; đưa lại được.
- **UC-46d**: khác UC-46c — xoá hẳn chữ và âm thanh, không đưa lại được; chỉ Thư ký (người nói tự xoá câu mình ở UC-80).
- **UC-81 (Luật 91/2025/QH15, NĐ 356/2025)**: đồng ý **riêng**, không gộp đồng ý ghi âm, ô không đánh sẵn; thông báo nêu mục đích,
  dữ liệu lưu (đặc trưng giọng + bản ghi mẫu mã hoá), bên kiểm soát dữ liệu, quyền của người dùng, là dữ liệu nhạy cảm;
  đồng ý gắn phiên bản thông báo (hiện 1.1) — người đồng ý bản cũ thấy "Đồng ý điều khoản mới"; bản ghi mẫu chỉ chính chủ nghe
  (mẫu ghi dưới đồng ý 1.0 không nghe được); rút đồng ý xoá ngay **mọi giọng mẫu và mọi gợi ý**, tên Thư ký đã gán giữ nguyên.
  Mẫu đạt: ≥ 6 giây có tiếng, ≤ 60 giây, không quá nhỏ, không rè, một người nói; tối đa 5 mẫu.

### FE-11 — Meeting Summary *(Tóm tắt cuộc họp)* — **HOÃN**, làm cùng Lượt 0

| UC | Tên (EN) | Actor chính | Quan hệ | Đặc tả |
|---|---|---|---|---|
| UC-47 | Generate / Regenerate Meeting Summary | Secretary; AI Summary Service | `<<extend>>` UC-45a (đầu vào là bản gỡ băng FE-10) | Placeholder, chưa triển khai |
| UC-48 | View Meeting Summary | Secretary | `<<extend>>` UC-16 | Placeholder, chưa triển khai |
| UC-49 | Edit Meeting Summary | Secretary | `<<extend>>` UC-48 | Placeholder, chưa triển khai |
| UC-50 | Approve Meeting Summary | Secretary | `<<extend>>` UC-48 | Placeholder, chưa triển khai |
| UC-51 | Publish Meeting Summary | Secretary | `<<extend>>` UC-48 | Placeholder, chưa triển khai |

Chỉ đổi feature chứa UC, **không** viết đặc tả, sequence hay class diagram cho FE-11 mới. Giữ actor/quan hệ cũ, riêng UC-47 trỏ
sang UC-45a thay cho UC-45. Actor ngoài *AI / Speech Processing Provider* của UC-47 đổi tên thành *AI Summary Service*
(chưa chọn; gỡ băng thì đã chạy trong hệ thống, tóm tắt thì chưa có quyết định).

### 3a. Bảng đổi số feature (30/10/2026)

| Cũ | Mới | Tên |
|---|---|---|
| FE-09 | FE-09 | Meeting Audio Upload & Media Library → **Meeting Recording** |
| FE-10 | FE-10 | Speaker-Attributed Transcription & Meeting Summary → **Transcription & Speaker Identification** |
| — (UC-47–51 trong FE-10) | **FE-11** | **Meeting Summary** |
| FE-11 | FE-12 | Voting Management |
| FE-12 | FE-13 | Meeting Minutes & Digital Signing |
| FE-13 | FE-14 | Email & In-App Notification |
| FE-14 | FE-15 | Personal Dashboard & Meeting Workspace |
| FE-15 | FE-16 | Reporting & Statistics |

Số UC **không đổi** (UC-52 vẫn là UC-52…). Chỉ số feature đổi.

---

## 3b. Lượt 0 — Dời FE-11…FE-15 thành FE-12…FE-16 — **HOÃN** (BA 30/10/2026: chuẩn đặc tả trước, đợt này không chạy)

Thay theo bảng §3a, **từ số lớn xuống số nhỏ** (FE-15→FE-16, rồi FE-14→FE-15, … FE-11→FE-12) để không thay chồng.
Grep lại `FE-1[1-6]`, `FE1[1-6]`, `feature-1[1-6]` sau mỗi file.

Tài liệu sống — **sửa**:
- [ ] DOCX nguồn trong `docs/08-reports/source/`: Report 1 (bảng feature), Report 2 (WBS/backlog), Report 3 (§I.4.2, tiêu đề §II, §III,
      bảng màn hình, cột Feature/Area của BR §V.1, Change Log). Rồi `sync_reports.py` cho từng report (Report 3 tự sinh lại
      `05-…`, `06-…`, `uc-map.html`). **Không** sửa `Report-*.md` tay.
- [ ] `docs/01-requirements/00-master-use-case-inventory.md`: các bảng FE, thống kê §7 (16 Feature), thêm bảng đổi số §3a cạnh bảng đổi số 10/10/2026.
      `docs/01-requirements/README.md`.
- [ ] Class diagram: `git mv` `feature-11-voting-management-class-diagram.drawio(.png)` → `feature-12-…`,
      `feature-13-notification-management-…` → `feature-14-…`; sửa tiêu đề trong drawio, xuất lại PNG; sửa mọi link trong
      `07-classdiagram/README.md` (§1, §6f).
- [ ] READMEs: `06-sequencediagram/voting-management/README.md`, `06-sequencediagram/README.md`, `03-api/README.md`,
      `08-reports/README.md`, `wireframe/README.md`, `docs/notes/README.md`.
- [ ] `03-api/15-bieu-quyet.md`, `03-api/00-danh-muc-ma-thong-bao.md` (chỉ chữ "FE-11" khi nói biểu quyết, không đổi mã MSG).
- [ ] Wireframe **bản hiện hành** (`TrustCabinet-wireframe-v10.html`, `figma-v10/code.js`) nếu có nhãn FE — kiểm `wireframe/README.md` bản nào là hiện hành.
- [ ] Code comment `backend/TrustCabinet.Domain/Entities/VotingSession.cs` (chỉ comment "FE-11").
- [ ] Landing page scope (AGENTS.md trỏ `frontend/src/pages/scopeData.ts` — file không còn ở đó; tìm nơi landing page lấy danh sách feature).

Tài liệu lịch sử — **không sửa** (D8): `05-decisions/*`, `Migrations/*`, `04-meetings/*`, `docs/notes/NOTE-*.md`,
`docs/notes/ban-giao-ba-final-review/01–18`, wireframe v4–v9, `figma-v5…v9`, `_tools/out/*`.

Kiểm: `dotnet test tests/TrustCabinet.Web.Tests --filter "MessageCodeCatalogueTests|SkillMirrorTests"`; `python scripts/sync_requirement_extracts.py --check`.
Ghi Jira: đổi tên epic/component theo số mới nếu Jira dùng số FE (BA làm).

## 4. Lượt 1 — Inventory và Report 3 §I.4.2

File: `docs/01-requirements/00-master-use-case-inventory.md` (file này **được** sửa tay) và DOCX Report 3.

- [x] Inventory: thay bảng FE-09 và FE-10 bằng §3; đổi tên feature; dòng D3 ("Secretary tự upload audio") cập nhật theo ADR-0040;
      actor *AI / Speech Processing Provider* → *Speech Recognition Service (chạy trong hệ thống, ADR-0026)*; luồng §6
      ("UC-44 Secretary upload audio → …") viết lại theo §3; UC-47–51 **giữ dưới FE-10** (D3 hoãn); bảng thống kê §7 đếm lại
      FE-09/FE-10/Tổng; §8 bỏ dòng "UC Voice ID chưa đánh số"; ghi một dòng ngày 30/10/2026 ở đầu file (thêm UC-80, UC-81,
      UC-46d; UC-43–46 đổi nội dung).
- [x] Report 3 DOCX `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx` §I.4.2: sửa dòng UC-43 → UC-46,
      thêm UC-80 (FE-09), UC-81 (FE-10), đổi tên feature trong cột Feature; UC-47–51 giữ nguyên dòng.
- [x] Change Log Report 3: một dòng 30/10/2026 "Reorganize FE-09/FE-10 to match the as-built recording, transcription and Voice ID;
      add UC-80 Manage My Recorded Speech and UC-81 Manage My Voice ID".

## 5. Lượt 2 — UC Specification (Report 3 §II.9, §II.10)

- [x] Thay placeholder §9 và §10.1–10.2 bằng bảng đặc tả đầy đủ cho mọi UC lá ở §3 (**22 bảng**: FE-09 9 — 43a–c, 44a–e, 80;
      FE-10 13 — 45a–c, 46a–d, 81a–f). Thêm §9.x cho UC-80 và §10.x cho UC-81a–f (sau UC-46, trước UC-47). **Sao chép đúng cấu trúc bảng của UC-39a** (Primary Actors / Secondary Actors / Description /
      Preconditions PRE-n / Postconditions POST-n / Normal Flow / Alternative Flows AF-n / Exception Flows EX-n), cùng style Word.
- [x] Mỗi EX dẫn đúng `MSGnn – "…"` **bản tiếng Anh** (Report 3 §V.2), kết thúc bằng câu chuẩn của README §2.4.
- [x] Mỗi bước kiểm tra dẫn BR ở §6.
- [x] **Sửa UC-39a (§8.3.1, FE-08)** theo ghi chú D9 ở §3: thêm AF ghi theo câu, hai EX (MSG312, MSG320), dẫn BR-352/354/356.
      Sửa chéo: UC-37 bước 3 và UC-39a POST-4 trỏ "UC-39a, UC-80" thay vì "FE-09"; UC-22 (End Meeting) nếu nói "upload sau khi kết thúc" thì sửa.
- [x] §10.3–10.7 (UC-47–51): giữ nguyên chỗ, đổi câu thành "Placeholder only – not implemented in the current release."
- [x] Sync: `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`
      rồi `python scripts/sync_requirement_extracts.py --check`. Rà `git diff` của `05-use-case-specifications.md`: link
      `Sequence Diagram:` của từng UC phải trỏ đúng file ở §7.

## 6. Lượt 3 — Business Rules (§V.1) và System Messages (§V.2)

**BR**: số kế tiếp là **BR-340**. Trước khi thêm, `grep` `06-business-rules.md` để tái dùng BR cùng nghĩa (BR-97, BR-325, BR-329,
BR-331…) và tìm BR cũ về "upload audio after Ended" để **sửa** hoặc đánh dấu ngừng dùng. Một BR = một nguyên tắc, không MSG, không kỹ thuật.
Nháp (đánh số lại liên tục nếu bỏ dòng nào):

| BR | Rule statement (nháp) | UC |
|---|---|---|
| BR-340 | How a Meeting is recorded follows its format: an Online Meeting records each person's own microphone, an In-person Meeting records the room, and a Hybrid Meeting records both. Nobody chooses the method. | UC-39a, UC-44a, UC-44c |
| BR-341 | Only the current Secretary may record the room, upload an audio file, delete a take or erase a recording. | UC-44a–e |
| BR-342 | The room may be recorded only while the Meeting is In Progress; an audio file may be uploaded while it is In Progress or Ended. | UC-44a, UC-44c |
| BR-343 | Before the first room take of a Meeting, and before each upload, the Secretary confirms that attendees were informed and nobody objected; the confirmation time and purpose are kept with the recording. | UC-44a, UC-44c |
| BR-344 | A Meeting has at most one open room take at a time. | UC-44a |
| BR-345 | Each saved take is appended to the Meeting's single room recording, and the whole recording is transcribed again. | UC-44b, UC-45c |
| BR-346 | A room take that receives no audio for 12 hours is saved by the system. | UC-44b |
| BR-347 | Recordings, transcripts and recorded sentences are kept for 180 days after they are saved and are then erased automatically. | UC-39a, UC-44e |
| BR-348 | Erasing a recording removes its audio and transcript but keeps the fact that it existed, when it was erased and by whom. | UC-44e |
| BR-349 | Deleting a take removes its audio and sentences; edited text and assigned names elsewhere in the recording are kept. A recording with a single take is erased as a whole instead. | UC-44d |
| BR-350 | Only the current Chair, Secretary and Participants may list and play a Meeting's recordings. | UC-43a, UC-43b, UC-45a |
| BR-351 | Only the current Chair or Secretary may download a recording or export a transcript, and nobody may for a Secret Meeting. Each download is recorded in the audit log. | UC-43c, UC-45b |
| BR-352 | In the online room, a person's microphone switch is the only recording switch: speech is recorded only while that person's microphone is on. | UC-39a |
| BR-353 | Before joining, the lobby tells the person that the Meeting is recorded for the minutes and erased after 180 days. | UC-37, UC-39a |
| BR-354 | A sentence not yet sent stays on the person's device and is accepted up to 7 days after the Meeting ends, if it was spoken during the Meeting. | UC-39a, UC-80 |
| BR-355 | A person may withdraw their own sentence, and the Secretary may delete any sentence; deleting removes its text and audio. | UC-80, UC-46d |
| BR-356 | A sentence that repeats a neighbour's speech picked up by another microphone is marked, not hidden. | UC-39a, UC-43a |
| BR-357 | Only the current Chair or Secretary may exclude a sentence from the record, with a reason, or restore it. An excluded sentence is not deleted; only they can read and play it. | UC-46c |
| BR-358 | Excluded sentences are left out of an exported transcript. | UC-45b |
| BR-359 | Only the Secretary may edit transcript text; edited text shows who edited it and when. | UC-46a |
| BR-360 | Transcribing a completed transcript again replaces all its text, edits and assigned names. | UC-45c |
| BR-361 | Voice ID requires the person's own separate consent, not combined with consent to recording and never pre-selected. | UC-81b |
| BR-362 | Consent is tied to the notice version the person read; when the notice changes, the person must accept the new version. | UC-81a, UC-81b |
| BR-363 | A person may keep at most five voice samples; a sample must contain 6 to 60 seconds of one person's clear speech. | UC-81c |
| BR-364 | Voice samples are stored protected; only their owner may play them back. | UC-81c, UC-81d |
| BR-365 | Withdrawing consent deletes all the person's voice samples and every speaker suggestion pointing to them at once; names already assigned by the Secretary are kept. | UC-81f |
| BR-366 | Speaker suggestions compare only with consenting people on that Meeting's current roster, are shown only to the Secretary, and never assign a name by themselves. | UC-45a, UC-46b |
| BR-367 | One person may be suggested for several voices; those voices are grouped as the same speaker. | UC-46b |
| BR-368 | A speaker may be assigned only to a person on the Meeting's current roster. | UC-46b |
| BR-369 | Meeting audio and voice samples are processed inside the system and never sent to an outside service. | UC-39a, UC-45c, UC-81c |
| BR-370 | A Guest's own microphone is not recorded and a Guest cannot open the Meeting's recordings; a Guest's speech reaches the record only through the room recording. (**NEEDS REVIEW PO**, D6) | UC-39a, UC-43a |

- [x] Thêm BR vào bảng §V.1 DOCX (cột nguồn: ADR / SCRUM / "PO 29/10/2026").
- [x] **MSG §V.2**: Report 3 chưa có dải này. Thêm (bản tiếng Anh, giữ nghĩa catalogue): MSG290–MSG323, MSG351–MSG367.
      MSG301, MSG320 theo câu **mới** (note 16 §3). MSG324–MSG327, MSG344 ghi *Retired*. **Không cấp mã mới**; nếu thiếu tình huống,
      dừng và hỏi BA (mã tiếp theo trong catalogue: MSG368).
- [x] Sync lại như Lượt 2. Kiểm: `dotnet test tests/TrustCabinet.Web.Tests --filter "MessageCodeCatalogueTests"`.

## 7. Lượt 4 — Sequence Diagram

Hai thư mục mới (mỗi thư mục có `README.md` theo mẫu `voting-management/README.md`):

`docs/06-sequencediagram/meeting-recording/` — FE-09

| File | Request đầu (Actor → Boundary) | Lifeline nội bộ (theo code) |
|---|---|---|
| `UC43a_ViewMeetingRecordings.txt` | Open Recordings tab | `:Recording Controller`, `:Recording Service`, `:Speech Clip Service`, `:Meeting Access Policy`, `:Recording Repository`, `:Speech Clip Repository`, Database |
| `UC43b_PlayMeetingRecording.txt` | Play recording / sentence | + `:Playback Link Signer`, `:Recording Storage` |
| `UC43c_DownloadMeetingRecording.txt` | Download recording | `:Recording Service`, `:Audit Service` |
| `UC44a_StartRoomRecording.txt` | Confirm turn on room mic | `:Room Take Service`, `:Recording Repository`, `:Recording Storage`, `:Speech Change Publisher`; `loop` gửi từng phần |
| `UC44b_StopAndSaveRoomRecording.txt` | Turn off room mic | `:Room Take Service`, `:Recording Storage` (nối), `:Transcript Repository` (xếp hàng), publisher; `ref` UC-45c |
| `UC44c_UploadMeetingAudioFile.txt` | Confirm upload | `:Recording Service`, `:Recording Storage`, `:Transcript Repository` |
| `UC44d_DeleteRecordingTake.txt` | Confirm delete take | `:Room Take Service`, `:Recording Storage` (cắt), `:Transcript Repository`, `:Audit Service` |
| `UC44e_EraseMeetingRecording.txt` | Confirm erase | `:Recording Service`, `:Recording Storage`, `:Audit Service`; ghi chú AF hệ thống hết hạn |
| `UC80_ManageMyRecordedSpeech.txt` | Confirm withdraw sentence | `:Speech Clip Service`, `:Speech Clip Repository`, `:Recording Storage`, `:Audit Service` |

Thêm vào thư mục có sẵn `docs/06-sequencediagram/online-meeting-room/` (D9):

| File | Request đầu | Lifeline nội bộ |
|---|---|---|
| `UC39a_RecordSpeechWhileMicrophoneOn.txt` | Speak a sentence with the microphone on (browser sends it) | `:Speech Clip Controller`, `:Speech Clip Service`, `:Speech Recognition Service` (external), `:Speech Clip Repository`, `:Recording Storage`, publisher; `UC39a_ManageMicrophone.txt` cũ giữ nguyên, thêm `ref` sang file mới |

`docs/06-sequencediagram/transcription-voice-id/` — FE-10

| File | Request đầu | Lifeline nội bộ |
|---|---|---|
| `UC45a_ViewRecordingTranscript.txt` | Open transcript of a room recording | `:Transcript Controller`, `:Transcription Service`, `:Transcript Repository` |
| `UC45b_ExportMeetingTranscript.txt` | Export transcript | `:Transcription Service` / `:Speech Clip Service`, `:Audit Service` |
| `UC45c_TranscribeMeetingRecording.txt` | Confirm transcribe again | `:Transcription Service`, `:Transcript Repository` |
| `UC45c_ProcessTranscriptionQueue.txt` | **System** (timer) — ngoại lệ D7 | `:Transcription Worker`, `:Transcription Service`, `:Speech Recognition Service`, `:Speaker Matcher`, `:Voice ID Repository`, publisher |
| `UC46a_EditTranscriptText.txt` | Save edited text | `:Transcription Service` / `:Speech Clip Service`, `:Audit Service` |
| `UC46b_AssignSpeaker.txt` | Assign speaker | `:Transcription Service`, `:Meeting Participant Repository`, `:Transcript Repository`, `:Audit Service` |
| `UC46c_ExcludeOrRestoreSentence.txt` | Confirm exclude | `:Speech Clip Service`, `:Speech Clip Repository`, `:Audit Service` |
| `UC46d_DeleteSentence.txt` | Confirm delete sentence | `:Speech Clip Service`, `:Speech Clip Repository`, `:Recording Storage`, `:Audit Service` |
| `UC81a_ViewMyVoiceId.txt` | Open My voice page | `:Voice ID Controller`, `:Voice ID Service`, `:Voice ID Repository` |
| `UC81b_GiveVoiceIdConsent.txt` | Agree to notice | `:Voice ID Service`, `:Voice ID Repository`, `:Audit Service` |
| `UC81c_RecordVoiceSample.txt` | Save voice sample | + `:Voice Embedder`, `:Voiceprint Protector` |
| `UC81d_PlayVoiceSample.txt` | Play a sample | `:Voice ID Service`, `:Voiceprint Protector` |
| `UC81e_DeleteVoiceSample.txt` | Confirm delete sample | `:Voice ID Service`, `:Voice ID Repository`, `:Audit Service` |
| `UC81f_WithdrawVoiceIdConsent.txt` | Confirm withdraw | `:Voice ID Service`, `:Voice ID Repository` (xoá mẫu + gợi ý trong một lần), `:Audit Service` |

- [x] Viết `.txt` theo cú pháp sequencediagram.org, đúng mẫu `UC56_CloseVote.txt`: `title`, khai báo lifeline, một request đầu,
      `alt`/`else` cho mỗi EX theo thứ tự UC, nhãn `MSGnn – "…"` (tiếng Anh) trả về ở Service → Controller → Page → Actor,
      dẫn BR trong nhãn bước kiểm tra.
- [x] Tên message nghiệp vụ, không HTTP/SQL; tên operation trên mũi tên Controller → Service lấy từ interface thật
      (bỏ hậu tố `Async`): ví dụ `beginTake(meetingId, actorId, start)`, `completeTake(recordingId, actorId)`,
      `setExclusion(clipId, actorId, excluded, reason)`, `assignSpeaker(recordingId, label, userId, actorId)`.
- [x] Xuất PNG: `node scripts/export-diagrams.js docs/06-sequencediagram/meeting-recording` và `.../transcription-voice-id`.
- [x] Thêm tab mỗi UC vào `TrustCabinet_Sequence_Diagrams.drawio` (giữ tab khác nguyên vẹn) và PNG trong `drawio-png/`
      (`python scripts/sync_sequence_drawio_png.py` / `txt_to_sequence_drawio_png.py` — đọc `drawio-png/README.md` trước).
- [x] Cập nhật bảng mục lục cuối `06-sequencediagram/README.md`.

## 8. Lượt 5 — Class Diagram (as-built)

Hai file mới trong `docs/07-classdiagram/`, cùng style FE-11:
`feature-09-meeting-recording-class-diagram.drawio` (+ `.drawio.png`) và
`feature-10-transcription-voice-id-class-diagram.drawio` (+ `.drawio.png`).

Diagram tuyên bố **as-built** ⇒ chữ ký lấy từ code (bỏ `Async`, `CancellationToken`).

| Lớp | FE-09 | FE-10 |
|---|---|---|
| `<<boundary>>` | `RecordingsPanel`, `RecorderCard`, `SpeechPanel` / `SpeechTimeline`, `MyClips` (UC-39a: `RoomView`) | `TranscriptView`, `VoicePage` |
| `<<control>>` | `RecordingController`, `SpeechClipController` (Minimal API `RecordingEndpoints`, `SpeechClipEndpoints`) | `TranscriptController`, `VoiceIdController` |
| `<<service>>` / `<<interface>>` | `IRecordingService` (list, canUpload, add, playback, download, erase, eraseExpired), `IRoomTakeService` (begin, append, complete, completeIdle, deleteTake), `ISpeechClipService` (list, canCapture, add, edit, delete, setExclusion, export, playback, eraseExpired) | `ITranscriptionService` (get, retry, export, editSegment, assignSpeaker, processNext, requeueInterrupted), `IVoiceIdService` (get, grant, withdraw, addSample, deleteSample, sampleAudio), `SpeakerMatcher` |
| `<<repository>>` | `IRecordingRepository`, `SpeechClip` repository (đọc tên thật trong Infrastructure) | `ITranscriptRepository`, `IVoiceIdRepository` |
| Hạ tầng / external | `IRecordingStorage`, `IRecordingPlaybackSigner`, `ISpeechClipAudioSigner`, `ISpeechChangePublisher`, `IAuditService` | `ISpeechEngine` (Speech Recognition Service), `IVoiceEmbedder`, `IVoiceprintProtector` |
| `<<entity>>` | `Recording` (+ `RecordingTake` value), `SpeechClip` (+ `SpeechClipSegment`), `Meeting`, `User` | `Transcript`, `TranscriptSegment`, `TranscriptSpeaker`, `VoiceConsent`, `VoiceSample`, `Recording`, `User` |
| `<<enumeration>>` | `RecordingSource`, `RecordingStatus`, `SpeechClipExclusion` | `TranscriptStatus` |

Quan hệ (multiplicity theo ERD/config EF): `Meeting 1 — 0..* Recording`; `Recording 1 ◆— 0..1 Transcript`;
`Transcript 1 ◆— 0..* TranscriptSegment`, `1 ◆— 0..* TranscriptSpeaker`; `Meeting 1 — 0..* SpeechClip`; `User 1 — 0..* SpeechClip` (speaker);
`User 1 — 0..* VoiceConsent`; `VoiceConsent 1 — 0..* VoiceSample`; `TranscriptSpeaker 0..* — 0..1 User` (suggested, assigned).

- [x] Ghi **SOURCE_CONFLICT / NEEDS REVIEW** trong `07-classdiagram/README.md` mục mới §6g:
      `RecordingSource.CallRoom` và `RecordingStatus.Failed` còn trong enum nhưng ADR-0040 đã bỏ ghi cả cuộc gọi;
      `MeetingSummary` có entity, không có service (không vẽ, ghi lý do); Guest không được ghi theo micro (D6, `RequireActiveUser`).
- [x] Bảng "Sequence (message tới receiver) → Class → operation" cho mọi file ở §7 (mẫu §6f).
- [x] Chạy validator drawio-skill, render, mở xem PNG, `git diff --check`. Ghi giới hạn kiểm tra (roundtrip PNG) như §6f.
- [x] Thêm hai dòng FE-09, FE-10 vào bảng mục lục §1 của README.

## 9. Ngoài phạm vi — chỉ liệt kê, không làm trong plan này

- Report 3 §III.9–10 (màn hình: tab Bản ghi, thẻ mic phòng, Ghi âm của tôi, Giọng nói của tôi) — làm qua wireframe ([plan 06](06-plan-dac-ta-man-hinh.md)).
- Report 3 §I.3.2 Entity Descriptions: thêm `SpeechClip`, `VoiceConsent`, `VoiceSample`, `TranscriptSpeaker`; sửa "No voiceprint is stored";
  `MeetingSummary` giữ. §I.4.4 Permission Matrix: dòng Recording / Transcript / Voice ID.
- Report 3 §I.1 và §III.17.6 (API-06 "AI / Speech Processing Provider – planned"), context diagram, UC diagram (thêm UC-80, UC-81, UC-46d), BF-04, state machine Transcript.
- **Lượt 0 (D3, §3a, §3b) — hoãn.** Đổi số FE là thay đổi phạm vi feature ở Report 1/2: BA báo PO trước khi chạy.
- Note cũ lỗi thời theo [note 16 §4](16-ghi-am-voice-id-28-29-10-2026.md#4-note-cũ-đã-lỗi-thời).

## 10. Thứ tự và cách chạy agent

Mỗi lượt một session mới; mở đầu bằng "đọc `docs/notes/ban-giao-ba-final-review/19-…md` §0 và lượt N".
Thứ tự: **Lượt 1 → 2 → 3 → 4 → 5** (Lượt 0 hoãn) (sequence cần UC/BR/MSG chốt; class cần sequence). Lượt 2 và 3 có thể gộp nếu agent đủ context.
Mỗi lượt kết thúc bằng: `git status`, `git diff --stat`, liệt kê mục còn `NEEDS REVIEW`. **Không commit** — BA review rồi tự commit.

## 11. Definition of Done

- [ ] Inventory, Report 3 §I.4.2/§II/§V.1/§V.2 có đủ UC ở §3; snapshot và hai bản trích sinh lại bằng script, `--check` sạch.
- [ ] Không còn "upload only after Ended", "AI / Speech Processing Provider", "chỉ lưu vector" trong phạm vi FE-09/10.
- [ ] 24 sequence `.txt` (FE-09: 9 + `UC39a_RecordSpeechWhileMicrophoneOn`; FE-10: 14) + PNG + tab drawio; mỗi EX của UC có nhánh `alt` tương ứng với đúng MSG.
- [ ] 2 class diagram as-built + PNG, README §6g có traceability và discrepancy log.
- [ ] `dotnet test tests/TrustCabinet.Web.Tests --filter "MessageCodeCatalogueTests|SkillMirrorTests"` xanh.
- [ ] Comment Jira SCRUM-29, 31, 35, 109 (và 102, 103) tóm tắt kết quả — BA làm, agent chỉ soạn nháp.

---

## 12. Review lượt 1–3 (30/10/2026) — việc phải sửa

Nguồn đúng: code trên `main` + §3 của plan này. Sequence (Lượt 4) phải theo đặc tả **đã sửa** bên dưới.

### A. Sai sự thật
1. **UC-39a**: chưa có EX mới. Thêm EX "Speech Recognition Service unavailable — sentences stay on the device and are sent
   later" (MSG312) và EX "The Meeting cannot be recorded now (In-person, not started, or ended more than 7 days ago)" (MSG320),
   đánh số tiếp sau EX-3 cũ (EX-4, EX-5). AF mới đổi **AF-3 → AF-2**, viết theo mẫu (bắt đầu "At Step 2…", kết thúc
   "The flow resumes…"), ghi rõ không áp dụng cho Guest (BR-370). Trong §V.2: MSG312 → UC-39a/EX-4, MSG320 → UC-39a/EX-5;
   MSG318 (thành công), MSG319, MSG321 phải xuất hiện trong AF-2/EX tương ứng, hoặc trỏ đúng chỗ thật.
2. **UC-81e**: bỏ "recalculates the composite voiceprint" và POST-2. Không có hồ sơ gộp: mỗi mẫu một vector, gợi ý lấy cosine cao nhất.
3. **UC-43a AF-2, UC-46c POST-2**: câu đã loại **không bị ẩn** với người khác — họ thấy một câu trống (biết đã từng có câu), không đọc, không nghe được.
4. **UC-44b AF-1**: MSG365 là khi lượt **không nhận được tiếng nào**, không phải "chỉ có im lặng".
5. **UC-44a EX-2 (MSG293)**: đổi thành AF — gần tới giới hạn dung lượng một lượt, hệ thống tự lưu lượt đó và mở lượt mới, việc ghi không gián đoạn. UC-44b thêm AF "bản ghi phòng đã đầy thì lượt này thành bản ghi mới".
6. **UC-81a**: mở từ mục **"My voice" ở thanh bên (sidebar)**, không phải profile menu.
7. **§V.2**: MSG362 trỏ UC-44b/EX-1 (không phải UC-44a/Main Flow). MSG361 (phần ghi gửi sai) — thêm EX ở UC-44a hoặc ghi "System-level, not shown in UC".
8. **Inventory**: câu cắt dở "…UC-43–UC-46 đổi nội dung và tách các UC" — viết trọn.

### B. Thiếu
- **UC-45c**: EX "chuyển chữ thất bại → trạng thái Failed kèm lý do, Thư ký chạy lại"; AF "bản ghi chưa có bản gỡ băng → Thư ký chạy lần đầu".
- **UC-45a**: hiển thị trạng thái *Pending / Processing / Completed / Failed*; bỏ PRE-2 "has an existing transcript".
- **UC-81b**: AF "đồng ý điều khoản mới — gia hạn trên đồng ý cũ, giọng mẫu giữ nguyên"; thông báo phải nêu **bên kiểm soát dữ liệu**
  và **là dữ liệu cá nhân nhạy cảm**; không viết cứng "1.1" (dùng "the current notice version").
- **UC-44a AF-1**: "Save recorded part" → UC-44b.
- **BR-370**: nguồn ghi "NEEDS REVIEW PO — D6"; không khẳng định là quyết định đã chốt.

### C. Ngôn ngữ kỹ thuật trong Report 3 (xoá/viết lại bằng nghiệp vụ)
"signed playback URL", "WAV 16 kHz mono", "streams audio parts", "broadcasts a change event", "background retention worker",
"byte range", "transcript rows", "Status to Deleted", "sentence row", "database", "editor ID", "worker queue",
"speaker diarization / clusters", "voice embedding vector", "WAV/WebM", "payload is malformed", "endpoint removed" (MSG nghỉ).
Cột Area của BR-340–BR-370 dùng vùng nghiệp vụ như BR cũ: *Meeting Recording*, *Transcription*, *Voice ID*, *Meeting Room*
(không "Recording Service", "Background Worker", "Infrastructure Architecture"). Mọi AF có câu kết chuẩn.

### D. Nhỏ
Inventory: `<<extend>>` có backtick như các dòng khác; tên VI UC-43 "Xem & nghe bản ghi cuộc họp"; UC-47 actor chỉ *AI Summary Service*.
