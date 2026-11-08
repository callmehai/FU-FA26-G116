# Đặc tả sơ bộ SCRUM-137 / SCRUM-142 (chia sẻ lịch và tìm giờ họp)

**Ngày:** 07/11/2026 · **Người viết:** BE · **Trạng thái:** sơ bộ, đủ để BE làm theo

| Jira | Tên | Trạng thái lúc viết |
|---|---|---|
| [SCRUM-142](https://sep490g116.atlassian.net/browse/SCRUM-142) | Chia sẻ lịch làm việc — ủy quyền xem bận/rảnh (eCab US #352) | To Do |
| [SCRUM-137](https://sep490g116.atlassian.net/browse/SCRUM-137) | Tìm giờ họp: tìm khung giờ mọi người cùng rảnh (eCab US #352) | To Do, phụ thuộc SCRUM-142 |

Đây là bước *BE: basic UC Specification + Sequence Diagram* của
[DEVELOPMENT_WORKFLOW.md](../DEVELOPMENT_WORKFLOW.md). Note này chưa phải Report 3: UC, BR ở đây là
**đề xuất**. BA đưa vào DOCX ở `08-reports/source/` lúc BA Final Review, rồi sinh lại
`05-use-case-specifications.md` và `06-business-rules.md`. Không chép tay sang hai file đó.

- Mã `MSG435`–`MSG455` đã **dành sẵn** trong [danh mục](../03-api/00-danh-muc-ma-thong-bao.md) cùng commit với note này
  (đợt đầu `MSG424`–`MSG436` tách nghĩa một-mã-một-tình-huống thành 20 mã rồi dời số lên `MSG435`–`MSG454`
  vì PR #279 đã nhận `MSG424`–`MSG434`; thêm mã mới `MSG455`; xem §5 — các mã chỉ ở trạng thái Dành sẵn
  trên nhánh này nên được đổi nghĩa, dòng nào đổi ghi rõ ở đó).
- Số BR-414 → BR-430 là số đề xuất (BR-414 → BR-429 nối tiếp BR-413 của PR #279, thêm BR-430 mới).
  BA giữ hoặc đổi số khi đưa vào Report 3.
- Số UC mới UC-84/UC-85 gom theo mẫu Report 3 (UC-06, UC-12, UC-28): UC-84 *Manage Calendar Sharing*
  (UC-84a xin xem, UC-84b chủ lịch tự chia sẻ, UC-84c trả lời, UC-84d thay đổi, UC-84e rút/ngừng) và
  UC-85 *Find Meeting Time* (UC-85a tìm giờ, UC-85b tạo nháp từ khung, UC-85c xem lịch), nối tiếp UC-83
  của PR #279. Số UC-86..UC-90 trả lại, không dùng. Không liên quan tới UC-83 → UC-87 **cũ** ở bảng đổi
  số ngày 10/10/2026 (đã đổi thành UC-64 → UC-69), giống cách UC-78 → UC-81 đã làm.
- **Số hiệu dựa trên PR #279 merge trước** — #279 (đặc tả sơ bộ SCRUM-33) đã nhận UC-83, BR-404 → BR-413
  và `MSG424`–`MSG434`. Nếu nhánh này merge trước, #279 dời số.

Nguồn nghiệp vụ: [note 36](ban-giao-ba-final-review/36-chuan-bi-dac-ta-chia-se-lich-va-tim-gio-hop.md) (§3 eCab,
§4 quyết định PO D1–D11); D12–D16 ở mục dưới. Quyết định PO 07/11 về FE-16 (chia sẻ lịch, Tìm giờ họp, không có trợ lý AI)
ghi ở [ADR-0058](../05-decisions/0058-chia-se-lich-va-tim-gio-hop.md) (PR #284), thay nửa câu "Tìm giờ họp" của ADR-0043. Comment Jira `10449` (SCRUM-137: chỉ bận/rảnh), `10460`–`10462` (SCRUM-142).

## Quyết định mới sau note 36

**D12 (PO, 07/11/2026 — đóng Q5 của note 36 §7):** khung giờ trùng một cuộc họp Mật của người được mời
**vẫn cho tạo nháp**. Bước kiểm lại khi Tạo nháp dùng đúng dữ liệu đã chia sẻ như lúc tìm (không tính họp Mật);
không có mã lỗi, cảnh báo, thông báo hay audit nào khác đi chỉ vì có họp Mật. Màn tìm giờ luôn hiện một câu cố
định (cho mọi khung) rằng chỉ tính lịch được chia sẻ và người được mời vẫn có thể bận. Người được mời từ chối
qua UC-29 như lời mời thường. Lý do: từ chối tạo — dù báo bằng câu chung — cũng cho phép dò lịch Mật bằng cách
thử từng khung. Tạo nháp: người đầu tiên làm Chair, còn lại Member, người tìm làm Secretary; cuộc họp ở Draft,
đi luồng duyệt bình thường; tạo cuộc họp hiện **không** kiểm trùng lịch người dự — giữ nguyên.

**D13 (PO, 07/11/2026 — đóng Q3):** không có trạng thái "tạm ngưng". Khi bên xem không còn là Meeting Organizer,
hoặc một trong hai tài khoản bị khoá, chia sẻ **dừng hẳn** (Stopped) ngay lúc đó, báo cả hai bên. Có lại quyền
hay mở khoá **không** làm chia sẻ tự chạy lại: muốn xem lại phải gửi yêu cầu chia sẻ mới (UC-84a/UC-84b). Lý do:
chủ lịch đồng ý cho một người đang giữ vai trò tạo họp; quyền mất rồi có lại là một lần đồng ý mới.

**D14 (BA, 07/11/2026 — đóng Q5):** tối đa **10 người ngoài người tìm**. Ô "Tính cả tôi" thêm lịch của chính
người tìm, không chiếm chỗ trong 10 người.

**D15 (BA theo eCab, 07/11/2026 — đóng Q6):** khoảng cách với cuộc họp khác áp **cả trước và sau** mỗi giờ bận.
Giờ bắt đầu được đề xuất sau đó còn làm tròn theo bước. Ví dụ: họp xong 09:10, cách 30 phút, làm tròn 30 phút →
khung sớm nhất 10:00 (không phải 09:40). Tên tham số theo eCab: **"Cách cuộc họp khác"** (đệm) và
**"Làm tròn giờ bắt đầu"** (bước).

**D16 (BA, 08/11/2026 — review PR #282):** khoảng ngày chia sẻ hợp lệ (BR-431): ngày bắt đầu không sau ngày
kết thúc, ngày kết thúc từ hôm nay trở đi, dài tối đa **365 ngày** tính cả hai đầu. Chia sẻ mới bắt đầu từ hôm nay;
đổi khoảng của chia sẻ Đang hiệu lực được giữ ngày bắt đầu cũ; khoảng chủ lịch rút ngắn khi chấp nhận phải nằm
trong khoảng được xin. Khác eCab (cho phép "không giới hạn", note 36 §3): BR-425 cần ngày kết thúc để tự hết hạn,
và giới hạn một năm để một chia sẻ bị quên không mở lịch mãi — giờ bận của một người là dữ liệu cá nhân
(Luật 91/2025/QH15). Con số 365 chờ PO xác nhận (Q10).

## 1. Hiện trạng code (07/11/2026)

| Phần | Đã có | Chưa có |
|---|---|---|
| Chia sẻ lịch | Không có gì: chưa bảng, chưa endpoint, chưa UC/BR | Bảng chia sẻ, endpoint, FE, UC/BR Report 3 |
| Tìm giờ họp | Không có gì: chưa endpoint, chưa UC/BR | Endpoint tìm, FE hai tab, UC/BR Report 3 |
| Dữ liệu bận/rảnh | `GET /api/meetings` chỉ trả cuộc họp người gọi được xem (đã lọc quyền), không dùng lại được để xem lịch người khác | Endpoint lịch bận/rảnh |
| Quyền | `Capability.MeetingCreator`, seed cấp cho Chủ toạ và Thư ký; gác endpoint tạo họp bằng `RequireCapability` | Không thêm capability mới (D6) |
| Mức mật | 3 mức `Normal` / `Internal` / `Secret` (ADR-0057) | Mốc chia sẻ (mới, thuộc bảng chia sẻ) |
| Vòng đời họp | `ActualEndedAt` ghi khi kết thúc; `Cancelled` là trạng thái cuối (BR-299) | — |
| Tạo họp | `POST /api/meetings` tạo nháp (MSG29), không kiểm trùng lịch người dự | Kiểm lại khung theo dữ liệu chia sẻ (mới, BR-428) |
| Audit + thông báo | `AuditLogEntry` append-only; `Notification` theo `TargetType`/`TargetId` + `DeduplicationKey` | Loại thông báo cho chia sẻ lịch (mới) |

## 2. Chỗ lệch khỏi ticket, cần ghi lên Jira

| # | Ticket | Đặc tả này | Lý do |
|---|---|---|---|
| D1 | SCRUM-137 mô tả: "TrustCabinet không có ủy quyền xem lịch (delegation) như eCab nên bỏ phần đó" và "Tab Lịch bận chung ... để sau, không bắt buộc" | Có SCRUM-142 (chia sẻ lịch); SCRUM-137 gồm cả hai tab Tìm khung giờ + Lịch được chia sẻ | PO chốt D1, D2 (note 36 §4); comment `10461` đã ghi, mô tả ticket chưa sửa |
| D2 | SCRUM-142 mô tả mục "PO tick" vẫn để ba phương án mức xem (A/B/C) | Chỉ 2 mốc: Mốc 1 = Normal, Mốc 2 = Normal + Internal; Secret không bao giờ chia sẻ | PO chốt lại 07/11 sau khi bỏ mức Hạn chế (comment `10462`, ADR-0057); AC8 theo bản 2 mốc |
| D3 | SCRUM-142 mô tả: "Người xin là Meeting Organizer (code gác bằng MeetingCreator)" | Người xin là Meeting Organizer; không tạo vai trò mới, không gác bằng tên vai trò | Meeting Organizer là actor Report 3, ứng với capability MeetingCreator |
| D4 | Note 36 §2: chừa `MSG434`–`MSG439`, mã tiếp theo `MSG440` | Dành `MSG435`–`MSG455`, mã tiếp theo `MSG456` | Số trong note 36 viết trước khi #271 merge; PR #279 đã nhận `MSG424`–`MSG434` nên dải này dời lên `MSG435`, thêm mã mới `MSG455` |
| D5 | Note 36 §5: UC-84 → UC-88 | UC-84 gom (UC-84a → UC-84e) + UC-85 gom (UC-85a → UC-85c) | PR #279 nhận UC-83; hai UC gom theo mẫu Report 3 (UC-06, UC-12, UC-28); bảng đổi số cũ → mới ở §3 |

## 3. Use Case đề xuất

Số UC-86..UC-90 trả lại, không dùng. Bảng đổi từ số cũ (07/11/2026) sang số mới:

| UC cũ | UC mới |
|---|---|
| UC-84 Request Calendar Share | UC-84a |
| UC-85 Share Calendar Directly | UC-84b |
| UC-87 Respond to Calendar Share | UC-84c |
| UC-86 Normal Flow + AF-1 (xin nâng/gia hạn, chủ lịch hạ mốc/đổi ngày) | UC-84d Change Calendar Share |
| UC-86 AF-2 + AF-3 (rút yêu cầu/thay đổi, ngừng chia sẻ) | UC-84e Withdraw or End Calendar Share |
| UC-86 AF-4 (hệ thống tự dừng/hết hạn) | Bỏ — đã có BR-424/BR-425 và dòng UC-06c/08/11/12c ở bảng dưới |
| UC-88 Find Meeting Time Slots | UC-85a |
| UC-89 Create Draft Meeting from Slot | UC-85b |
| UC-90 View Shared Calendar | UC-85c |

### UC-84 – Manage Calendar Sharing (Quản lý chia sẻ lịch) — SCRUM-142

Thuộc FE-16.

_UC gom — đặc tả nằm ở các UC con UC-84a, UC-84b… bên dưới._

#### UC-84a – Request Calendar Share (Yêu cầu xem lịch)

Thuộc FE-16, `<<extend>>` UC-85a (xin chia sẻ từ người "Chưa được chia sẻ lịch" trên màn tìm giờ).
Sequence Diagram: [UC84a_RequestCalendarShare.txt](../06-sequencediagram/calendar-sharing/UC84a_RequestCalendarShare.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer (người sẽ tạo cuộc họp) |
| **Secondary Actors** | User — chủ lịch (mọi tài khoản nội bộ đang hoạt động, trừ chính người xin) |
| **Description** | Allows a Meeting Organizer to ask another user to share working hours as busy/free, so that free slots can be found without asking each person. The owner answers in UC-84c. (BR-414, BR-423) |
| **Preconditions** | ● PRE-1: The actor is signed in as a Meeting Organizer. (BR-430)<br>● PRE-2: No share of the owner's calendar to the actor is Pending (Chờ đồng ý) or Effective (Đang hiệu lực). (BR-423) |
| **Postconditions** | ● POST-1: A Pending share exists and the owner is notified. (BR-423, BR-426)<br>● POST-2: No meeting content of the owner is disclosed either way. (BR-427)<br>● POST-3: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |
| **Normal Flow** | Request Calendar Share<br>1. The actor opens the Chia sẻ lịch page (or starts from someone shown “Chưa được chia sẻ lịch” on the UC-85a page) and chooses the owner (the list excludes the actor), a level (Mốc 1 or Mốc 2) and a date range.<br>2. The system checks that the actor is still a Meeting Organizer (else EX-1), that the owner is an active internal account different from the actor (else EX-4), that no Pending or Effective share of the owner's calendar to the actor exists (else EX-2) and that the range is valid (else EX-3). (BR-414, BR-423, BR-430, BR-431)<br>3. The system saves the Pending share, audits and notifies the owner. MSG435 – “Calendar share request sent.” The use case ends; the answer is UC-84c. |
| **Alternative Flows** | N/A — the owner's answer (accept, possibly at a lower level or a shorter range, or decline) is UC-84c. |
| **Exception Flows** | EX-1 – The actor has no right to create meetings<br>1. At Step 2, the actor is no longer a Meeting Organizer. (BR-430)<br>2. Nothing is saved. The system displays MSG448 – “Only a Meeting Organizer can do this.”<br>3. The use case ends.<br>EX-2 – A share already exists<br>1. At Step 2, a Pending or Effective share of the owner's calendar to the actor already exists. (BR-423)<br>2. Nothing is saved. The system displays MSG449 – “There is already a pending or effective share with this person.”<br>3. The use case ends.<br>EX-3 – The range is invalid<br>1. At Step 2, the date range is invalid. (BR-431)<br>2. Nothing is saved. The system displays MSG443 – “The date range is invalid.”<br>3. The use case ends.<br>EX-4 – The owner cannot be reached<br>1. At Step 2, the chosen owner is no longer an active internal account (unknown user or locked account).<br>2. Nothing is saved and nothing about any account is disclosed; the system refreshes the list and that person is no longer listed.<br>3. The use case ends.<br>EX-5 – The share cannot be saved<br>1. At Step 3, the system cannot save the share.<br>2. Everything stays as it was and nobody is notified. The system displays MSG447 – “Unable to save the calendar sharing data. Please try again.”<br>3. The use case ends. |

#### UC-84b – Share Calendar Directly (Chủ lịch tự chia sẻ)

Thuộc FE-16. Sequence Diagram:
[UC84b_ShareCalendarDirectly.txt](../06-sequencediagram/calendar-sharing/UC84b_ShareCalendarDirectly.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | User — chủ lịch (mọi tài khoản nội bộ đang hoạt động) |
| **Secondary Actors** | Meeting Organizer — bên nhận |
| **Description** | Allows a calendar owner to share working hours directly with a Meeting Organizer, always at Mốc 1. The recipient answers in UC-84c; the recipient asks for Mốc 2 through UC-84d after accepting. (BR-414, BR-423) |
| **Preconditions** | ● PRE-1: The actor is signed in with an active internal account. (BR-430)<br>● PRE-2: No share of the actor's calendar to the recipient is Pending (Chờ đồng ý) or Effective (Đang hiệu lực). (BR-423) |
| **Postconditions** | ● POST-1: A Pending share at Mốc 1 exists and the recipient is notified. (BR-414, BR-426)<br>● POST-2: No meeting content of the owner is disclosed either way. (BR-427) |
| **Normal Flow** | Share Calendar Directly<br>1. The actor opens the Chia sẻ lịch page and chooses a recipient who is a Meeting Organizer and a date range; the level is fixed at Mốc 1.<br>2. The system checks the recipient (active account, still a Meeting Organizer — else EX-1), that no Pending or Effective share of the actor's calendar to the recipient exists (else EX-2) and that the range is valid (else EX-3). (BR-414, BR-423, BR-431)<br>3. The system saves the Pending share at Mốc 1, audits and notifies the recipient. MSG441 – “Calendar shared. Waiting for acceptance.” The use case ends; the answer is UC-84c. |
| **Alternative Flows** | N/A — the recipient's answer is UC-84c. |
| **Exception Flows** | EX-1 – The recipient cannot receive a share<br>1. At Step 2, the recipient is not a Meeting Organizer or is not active.<br>2. Nothing is saved. The system displays MSG455 – “This person cannot receive a calendar share.”<br>3. The use case ends.<br>EX-2 – A share already exists<br>1. At Step 2, a Pending or Effective share of the actor's calendar to the recipient already exists. (BR-423)<br>2. Nothing is saved. The system displays MSG449 – “There is already a pending or effective share with this person.”<br>3. The use case ends.<br>EX-3 – The range is invalid<br>1. At Step 2, the date range is invalid. (BR-431)<br>2. Nothing is saved. The system displays MSG443 – “The date range is invalid.”<br>3. The use case ends.<br>EX-4 – The share cannot be saved<br>1. At Step 3, the system cannot save the share.<br>2. Everything stays as it was and nobody is notified. The system displays MSG447 – “Unable to save the calendar sharing data. Please try again.”<br>3. The use case ends. |

#### UC-84c – Respond to Calendar Share (Trả lời chia sẻ lịch)

Thuộc FE-16. Dùng chung một sequence cho câu trả lời của yêu cầu UC-84a, chia sẻ UC-84b và thay đổi đang chờ
của UC-84d (mẫu gọi API ở §6).
Sequence Diagram:
[UC84c_RespondToCalendarShare.txt](../06-sequencediagram/calendar-sharing/UC84c_RespondToCalendarShare.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | User — bên được hỏi (chủ lịch, với yêu cầu UC-84a hoặc thay đổi UC-84d; Meeting Organizer bên nhận, với chia sẻ UC-84b) |
| **Secondary Actors** | Bên gửi (nhận thông báo về câu trả lời) |
| **Description** | Allows the asked person to accept or decline a calendar share request, a direct share or a pending change. Accepting a UC-84a request may lower the level or shorten the range. (BR-414, BR-423, BR-426) |
| **Preconditions** | ● PRE-1: The actor is the asked side of a Pending (Chờ đồng ý) share or pending change.<br>● PRE-2: The share or change is still Pending. (BR-423) |
| **Postconditions** | ● POST-1: The share is Effective (Đang hiệu lực) with the chosen level/range, or closed, or kept as it was for a declined change; the sender is notified in every case. (BR-426)<br>● POST-2: No meeting content of the owner is disclosed either way. (BR-427)<br>● POST-3: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |
| **Normal Flow** | Respond to Calendar Share<br>1. The actor opens the notification (or the Chia sẻ lịch page) and sees the Pending request: who sent it, the level and the date range.<br>2. The system checks that the actor is the asked side (else EX-1), that the share is still Pending (else EX-2). A share whose viewer lost the Meeting Organizer right was already stopped at that moment and is no longer Pending (BR-424). (BR-423)<br>3. On accept the system marks the share Effective — for a UC-84a request the owner may choose a lower level or a shorter range (BR-414) — audits and notifies the sender. MSG436 – “Calendar share accepted.”<br>4. On decline the system closes the share, audits and notifies the sender. MSG437 – “Calendar share request declined.” |
| **Alternative Flows** | AF-1 – Answer a pending change (UC-84d)<br>1. At Step 3, the pending item is a change (raise to Mốc 2 or extend the range) instead of a new share.<br>2. On approval the system applies the new level/range, audits and notifies the viewer. MSG442 – “Calendar share updated.”<br>3. On decline everything stays as it was, both sides are notified. MSG452 – “Calendar share change declined.” |
| **Exception Flows** | EX-1 – The actor is not the asked side<br>1. At Step 2, the actor is not the asked side of this share.<br>2. Nothing changes and nothing about the share or account is disclosed.<br>3. The use case ends.<br>EX-2 – Nothing to answer<br>1. At Step 2, the share or change is no longer Pending. (BR-423)<br>2. Nothing changes. The system displays MSG450 – “This action is not allowed in the current state of the share.”<br>3. The use case ends.<br>EX-3 – The range is invalid<br>1. At Step 3, the owner shortens the range to an invalid range. (BR-431)<br>2. Nothing changes. The system displays MSG443 – “The date range is invalid.”<br>3. The flow resumes at Step 3.<br>EX-4 – The answer cannot be saved<br>1. At Step 3 or AF-1 Step 2, the system cannot save the share.<br>2. Everything stays as it was and nobody is notified. The system displays MSG447 – “Unable to save the calendar sharing data. Please try again.”<br>3. The use case ends. |

#### UC-84d – Change Calendar Share (Thay đổi chia sẻ)

Thuộc FE-16. Sequence Diagram:
[UC84d_ChangeCalendarShare.txt](../06-sequencediagram/calendar-sharing/UC84d_ChangeCalendarShare.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer (bên xem) và User — chủ lịch (tuỳ thao tác bên nào làm) |
| **Secondary Actors** | Bên còn lại của chia sẻ (nhận thông báo; trả lời thay đổi đang chờ trong UC-84c) |
| **Description** | Allows the viewer to ask for a higher level or a longer range (answered in UC-84c); the owner lowers the level or changes the dates at once. A stopped or expired share shows its state and reveals no hours. (BR-423, BR-424, BR-425, BR-426) |
| **Preconditions** | ● PRE-1: Both sides are signed in as the two people of the share when they act. (BR-430)<br>● PRE-2: The share is Effective (Đang hiệu lực); a Pending (Chờ đồng ý) share is withdrawn in UC-84e and asked for again instead. (BR-423) |
| **Postconditions** | ● POST-1: The share holds a pending change, or the new level/range; the other side is notified in both cases. (BR-426)<br>● POST-2: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |
| **Normal Flow** | Change Calendar Share<br>1. The viewer opens their share and asks for Mốc 2 (raise) or a longer range (extend); only one pending change at a time.<br>2. The system saves the pending change, audits and notifies the owner. MSG454 – “Calendar share change request sent.” The use case ends; the owner's answer is UC-84c. |
| **Alternative Flows** | AF-1 – The owner lowers the level or changes the dates<br>1. At any time on an Effective share, the owner chooses a lower level or a new date range.<br>2. The system applies it at once, audits and notifies the viewer. MSG442 – “Calendar share updated.” |
| **Exception Flows** | EX-1 – The actor is not one of the two people<br>1. At Step 1, the actor is not one of the two people of this share.<br>2. Nothing changes and nothing about the share or account is disclosed.<br>3. The use case ends.<br>EX-2 – Change is not allowed<br>1. At Step 1 (or AF-1 Step 1), the share is not Effective, or a change is already pending. (BR-423)<br>2. Nothing changes. The system displays MSG450 – “This action is not allowed in the current state of the share.”<br>3. The use case ends.<br>EX-3 – The range is invalid<br>1. At Step 1 (or AF-1 Step 1), the new date range is invalid. (BR-431)<br>2. Nothing changes. The system displays MSG443 – “The date range is invalid.”<br>3. The flow resumes where the actor entered the range.<br>EX-4 – The change cannot be saved<br>1. At Step 2 (or AF-1 Step 2), the system cannot save the change.<br>2. Everything stays as it was and nobody is notified. The system displays MSG447 – “Unable to save the calendar sharing data. Please try again.”<br>3. The use case ends. |

#### UC-84e – Withdraw or End Calendar Share (Rút hoặc ngừng chia sẻ)

Thuộc FE-16. Sequence Diagram:
[UC84e_WithdrawOrEndCalendarShare.txt](../06-sequencediagram/calendar-sharing/UC84e_WithdrawOrEndCalendarShare.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer (bên xem) và User — chủ lịch (tuỳ thao tác bên nào làm) |
| **Secondary Actors** | Bên còn lại của chia sẻ (nhận thông báo) |
| **Description** | Allows the sender to withdraw a Pending share or a pending change; either side ends an Effective share. A stopped or expired share stays listed with its state and reveals no hours. (BR-423, BR-424, BR-425, BR-426) |
| **Preconditions** | ● PRE-1: Both sides are signed in as the two people of the share when they act. (BR-430)<br>● PRE-2: Withdrawing needs a Pending (Chờ đồng ý) share or change; ending needs an Effective (Đang hiệu lực) share. (BR-423) |
| **Postconditions** | ● POST-1: The request is closed (a withdrawn new share leaves nothing; a withdrawn change leaves the share as it was), or the share is ended; the other side is notified in every case. (BR-426)<br>● POST-2: A stopped or expired share reveals no busy hours and never comes back; viewing again needs a new share. (BR-424, BR-425)<br>● POST-3: Shares of other people stay invisible: asking about them discloses nothing about the other share or account. |
| **Normal Flow** | Withdraw Calendar Share<br>1. The side that sent the request or the change withdraws it.<br>2. The system closes the request (a withdrawn new share leaves nothing; a withdrawn change leaves the share as it was), audits and notifies the other side. MSG438 – “Calendar share request withdrawn.” |
| **Alternative Flows** | AF-1 – Either side ends an Effective share (“Ngừng chia sẻ” / “Thôi xem lịch”)<br>1. Either side ends the share.<br>2. The system closes it, audits and notifies the other side. Hours already found are not removed from anyone's screen, but no new lookup uses this share. MSG451 – “Calendar sharing ended.” |
| **Exception Flows** | EX-1 – The actor is not one of the two people<br>1. At Step 1, the actor is not one of the two people of this share.<br>2. Nothing changes and nothing about the share or account is disclosed.<br>3. The use case ends.<br>EX-2 – Nothing to withdraw or end<br>1. At Step 1 (or AF-1 Step 1), there is nothing Pending to withdraw, or the share is not Effective to end. (BR-423)<br>2. Nothing changes. The system displays MSG450 – “This action is not allowed in the current state of the share.”<br>3. The use case ends.<br>EX-3 – The withdrawal or ending cannot be saved<br>1. At Step 2 (or AF-1 Step 2), the system cannot save the change.<br>2. Everything stays as it was and nobody is notified. The system displays MSG447 – “Unable to save the calendar sharing data. Please try again.”<br>3. The use case ends. |

### UC-85 – Find Meeting Time (Tìm giờ họp) — SCRUM-137

Thuộc FE-16 (tab Tìm khung giờ + tab Lịch được chia sẻ).

_UC gom — đặc tả nằm ở các UC con UC-85a, UC-85b… bên dưới._

#### UC-85a – Find Meeting Time Slots (Tìm khung giờ)

Thuộc FE-16 (tab Tìm khung giờ). Từ người hiện “Chưa được chia sẻ lịch” có thể bắt đầu UC-84a.
Sequence Diagram:
[UC85a_FindMeetingTimeSlots.txt](../06-sequencediagram/calendar-sharing/UC85a_FindMeetingTimeSlots.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer (sẽ là Secretary của cuộc họp tạo ra ở UC-85b) |
| **Secondary Actors** | User — những người được mời (mỗi người một màu bận/rảnh; người chưa chia sẻ hiện “Chưa được chia sẻ lịch”) |
| **Description** | Allows a Meeting Organizer to find slots where everyone invited is free, using only shared calendars. From anyone shown “Chưa được chia sẻ lịch”, the finder may start UC-84a. (BR-414 → BR-422, BR-427, BR-428) |
| **Preconditions** | ● PRE-1: The actor is signed in as a Meeting Organizer. (BR-430) |
| **Postconditions** | ● POST-1: The actor sees common slots by day, or — when none fits everyone — the slots with the most people free and who is busy. Attendee names come from the actor's own invite list; no meeting titles, locations, attendee lists or sensitivity levels are shown. (BR-416, BR-427)<br>● POST-2: Days outside a share and people who never shared show “unknown” / “Chưa được chia sẻ lịch”, never free. (BR-415, BR-416) |
| **Normal Flow** | Find Meeting Time Slots<br>1. The actor opens the Tìm giờ họp page (tab Tìm khung giờ) and enters: invitees (at most 10 people besides the actor; “Tính cả tôi” also counts the actor's own calendar), duration, date range, weekdays, working hours, lunch break, “Cách cuộc họp khác” (buffer) and “Làm tròn giờ bắt đầu” (step); defaults follow BR-421/BR-422. The page always shows one fixed sentence: only shared calendars are counted and invitees may still be busy. (BR-428)<br>2. The system checks the parameters (BR-421, BR-422).<br>3. The system loads the Effective shares of each invitee's calendar to the actor (BR-423) and those invitees' meetings; it keeps only meetings at shared levels, counts the actor's own meetings only when “Tính cả tôi” is ticked (then at every level), marks busy with the buffer on both sides of every busy period and then rounds start times up to the step, skips past start times and treats out-of-range days as unknown. (BR-414 → BR-420, BR-422)<br>4. The system lists the common slots by day. |
| **Alternative Flows** | AF-1 – No slot fits everyone<br>1. At Step 4, no slot fits all invitees.<br>2. The system shows the slots with the most people free, with who is busy, per day. MSG445 – “No time slot fits everyone. Showing the slots with the most people free.” |
| **Exception Flows** | EX-1 – Search parameters are invalid<br>1. At Step 2, the parameters break BR-421/BR-422 (too many people, bad duration, range over 31 days, bad hours/buffer/step).<br>2. Nothing is searched. The system displays MSG444 – “The meeting-time search is invalid. Check the highlighted fields.”<br>3. The flow resumes at Step 1 of the Main Flow.<br>EX-2 – The actor has no right to create meetings<br>1. At Step 2, the actor is no longer a Meeting Organizer.<br>2. Nothing is searched. The system displays MSG448 – “Only a Meeting Organizer can do this.”<br>3. The use case ends.<br>EX-3 – The search cannot be read<br>1. At Step 3, the system cannot read the shares or the meetings.<br>2. Nothing is shown. The system displays MSG453 – “Unable to load the calendar sharing data. Please try again.”<br>3. The use case ends. |

#### UC-85b – Create Draft Meeting from Slot (Tạo nháp từ khung giờ)

Thuộc FE-16, `<<extend>>` UC-85a (từ một khung đã tìm). Tạo nháp là tạo cuộc họp Draft rồi đi luồng duyệt thường (UC-14).
Sequence Diagram:
[UC85b_CreateDraftMeetingFromSlot.txt](../06-sequencediagram/calendar-sharing/UC85b_CreateDraftMeetingFromSlot.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer (người tìm ở UC-85a, sẽ là Secretary của cuộc họp tạo ra) |
| **Secondary Actors** | User — những người được mời trong khung đã chọn |
| **Description** | Allows a Meeting Organizer to create a Draft meeting from a slot found in UC-85a. A slot that overlaps an undisclosed Secret meeting still allows a draft; the recheck before saving uses the same shared data. (BR-428, BR-429) |
| **Preconditions** | ● PRE-1: The actor is signed in as a Meeting Organizer. (BR-430)<br>● PRE-2: The actor has just found the slot in UC-85a. |
| **Postconditions** | ● POST-1: A chosen slot creates a Draft meeting through UC-14 (MSG29) with the first listed person as Chair, the rest as Member and the actor as Secretary; it follows the normal approval flow. (BR-429)<br>● POST-2: No signal — no distinct error, warning, notification or audit — differs only because a Secret meeting overlaps. (BR-428) |
| **Normal Flow** | Create Draft Meeting from Slot<br>1. The actor chooses a slot; the system opens the meeting dialog prefilled with the slot and the invitees.<br>2. The actor confirms; the system rechecks the slot against the same shared data as the UC-85a search — never against undisclosed meetings. (BR-428)<br>3. The system creates the meeting in Draft through UC-14: the first listed invitee becomes Chair, the rest become Member, the actor becomes Secretary. MSG29 – “Tạo cuộc họp thành công.” The meeting follows the normal approval flow. (BR-429) |
| **Alternative Flows** | N/A |
| **Exception Flows** | EX-1 – The slot is no longer free<br>1. At Step 2, the slot no longer fits the shared data (someone's shared calendar changed).<br>2. No meeting is created. The system displays MSG446 – “This time slot is no longer free. Please choose another slot.” The message is the same whether or not an undisclosed meeting overlaps. (BR-428)<br>3. The flow returns to the UC-85a slot list.<br>EX-2 – The actor has no right to create meetings<br>1. At Step 2, the actor is no longer a Meeting Organizer.<br>2. No meeting is created. The system displays MSG448 – “Only a Meeting Organizer can do this.”<br>3. The use case ends.<br>EX-3 – The slot cannot be rechecked<br>1. At Step 2, the system cannot read the shares or the meetings.<br>2. No meeting is created. The system displays MSG453 – “Unable to load the calendar sharing data. Please try again.”<br>3. The use case ends.<br>EX-4 – The draft cannot be saved<br>1. At Step 3, the system cannot save the draft.<br>2. No meeting is created. The system displays MSG35 – “Không thể tạo cuộc họp. Vui lòng thử lại.”<br>3. The use case ends. |

#### UC-85c – View Shared Calendar (Xem lịch được chia sẻ)

Thuộc FE-16 (tab Lịch được chia sẻ). Sequence Diagram:
[UC85c_ViewSharedCalendar.txt](../06-sequencediagram/calendar-sharing/UC85c_ViewSharedCalendar.txt)

| Mục | Nội dung |
|---|---|
| **Primary Actors** | Meeting Organizer |
| **Secondary Actors** | User — những người đã chia sẻ lịch với actor (mỗi người một màu) |
| **Description** | Allows a Meeting Organizer to see a weekly calendar of busy hours from people who shared with them. Overlapping hours sit side by side; no meeting titles are ever shown. People whose share stopped or expired stay listed with their state label and contribute no hours. (BR-414, BR-415, BR-417, BR-424, BR-425, BR-427) |
| **Preconditions** | ● PRE-1: The actor is signed in as a Meeting Organizer. (BR-430) |
| **Postconditions** | ● POST-1: The actor sees only busy blocks of Effective (Đang hiệu lực) shares at shared levels, one colour per sharer; nothing else. (BR-427)<br>● POST-2: Stopped (Đã dừng) and expired (Hết hạn) shares stay listed with their state label and reveal no hours; they never come back. (BR-424, BR-425) |
| **Normal Flow** | View Shared Calendar<br>1. The actor opens the Tìm giờ họp page (tab Lịch được chia sẻ) and chooses a week.<br>2. The system loads the actor's shares (any status) and the sharers' meetings at shared levels only, Monday to Friday, 08:00–17:00. (BR-414, BR-417)<br>3. The system shows the week: busy blocks only, one colour per sharer, overlaps side by side, no titles, locations, attendees or levels. People whose share stopped or expired appear with their state label (“Đã dừng” / “Hết hạn”) and contribute no hours. Opening such a person shows MSG439 / MSG440 as the detail message. (BR-424, BR-425, BR-427) |
| **Alternative Flows** | AF-1 – Nobody has shared yet<br>1. At Step 2, the actor has no share in any status.<br>2. The system shows an empty week pointing to UC-84a (ask) and UC-84b (receive). The flow ends successfully. |
| **Exception Flows** | EX-1 – The week cannot be loaded<br>1. At Step 2, the system cannot read the shares or the meetings.<br>2. Nothing is shown. The system displays MSG453 – “Unable to load the calendar sharing data. Please try again.”<br>3. The use case ends. |

### Thay đổi ở các UC đã đặc tả

| UC | Thay đổi |
|---|---|
| UC-14 Create Meeting | AF mới: tạo nháp từ khung giờ tìm được (UC-85b) — điền sẵn giờ và danh sách; kiểm lại khung theo dữ liệu chia sẻ (BR-428) thay vì kiểm trùng lịch (không kiểm — BR-429) |
| UC-29 Respond to Meeting Invitation | Người được mời từ chối cuộc họp tạo từ khung tìm được như lời mời thường (D12); không có gì khác đi |
| UC-06c (vô hiệu hoá tài khoản) | Vô hiệu hoá một trong hai bên của chia sẻ thì dừng các chia sẻ liên quan ngay lúc đó (BR-424), ghi audit và báo hai bên (BR-426) |
| UC-08 (gán vai trò), UC-11 (cấu hình quyền vai trò) | Thao tác làm bên xem mất quyền tạo cuộc họp thì dừng các chia sẻ bên đó đang xem ngay lúc đó (BR-424), ghi audit và báo hai bên (BR-426); có lại quyền không làm chia sẻ tự chạy lại |
| UC-12c (xoá vai trò) | Chỉ thêm POST-4: xoá vai trò không làm chia sẻ đã dừng chạy lại. Vai trò chỉ xoá được khi không tài khoản nào giữ, nên bản thân việc xoá không dừng chia sẻ nào; gỡ quyền tạo cuộc họp khỏi một người nằm ở UC-08 |

## 4. Business Rules đề xuất

| BR | Rule Statement | UC | Nguồn |
|---|---|---|---|
| `BR-414` | Calendar sharing has exactly two levels: Level 1 covers Normal meetings, Level 2 covers Normal and Internal meetings. A Secret meeting is never shared, not even as busy. Find-a-time may therefore propose a slot that overlaps a Secret meeting; the meeting can still be created and that invitee declines the invitation (BR-428, UC-29). | UC-84a, UC-84b, UC-84c, UC-84d, UC-85a, UC-85c | SCRUM-142 / D7, ADR-0057, ADR-0058 — 2 mốc theo eCab, họp Mật không bao giờ chia sẻ; Tìm giờ có thể đề xuất trùng họp Mật, người đó tự từ chối |
| `BR-415` | A day outside the share's date range is "unknown": it is neither free nor busy and is never proposed as a slot. | UC-85a, UC-85c | SCRUM-142 / eCab — ngày ngoài khoảng là "chưa biết" |
| `BR-416` | A person who has not shared with the viewer shows “Not shared” and is counted as neither free nor busy. The finder's own calendar is counted only when the finder ticks “Tính cả tôi”, and then at every level (BR-418, BR-421). | UC-85a | SCRUM-137 / comment 10449 — người chưa chia sẻ khác rảnh; lịch người tìm chỉ tính khi tick |
| `BR-417` | A meeting above the shared level counts as free for the viewer (for example an Internal meeting at Level 1). | UC-85a, UC-85c | SCRUM-142 / D7 — mức chưa được chia sẻ hiện là rảnh |
| `BR-418` | The finder's own meetings count at every level, including Secret. | UC-85a | SCRUM-137 — lịch của chính người tìm tính mọi mức |
| `BR-419` | Only a current Chair, Secretary or Member assignment counts as busy. A person who only created the meeting or only approves it never counts as busy. A person who declined the invitation never counts as busy. | UC-85a, UC-85c | SCRUM-142 / D9 — chỉ Chủ toạ/Thư ký/Thành viên tính bận; người từ chối và nhờ dự thay (Q9) |
| `BR-420` | A meeting counts as busy while it is Chờ duyệt, Đã duyệt, Đã công bố, Đang họp or Đã kết thúc (until its actual end time). A meeting in Nháp, Bị từ chối or Đã huỷ never counts as busy. | UC-85a, UC-85c | SCRUM-142 — trạng thái tính bận (chờ PO xác nhận Q9) |
| `BR-421` | A search covers at most 10 people besides the finder; the finder may also count their own calendar, which does not take one of the 10 places. Duration is 15 minutes to 8 hours (default 2 hours) and the date range at most 31 days. | UC-85a | SCRUM-137 / D8, D14 — số eCab, 10 người ngoài người tìm |
| `BR-422` | Slots fall Monday to Friday, 08:00–17:00 excluding the 12:00–13:00 lunch break, and never start in the past. A slot keeps a gap of 0–4 hours (default 30 minutes) both before and after every busy period. Start times are then rounded up to a step of 5 minutes to 2 hours (default 30 minutes): a meeting ending 09:10 with a 30-minute gap and a 30-minute step gives 10:00 as the earliest start. | UC-85a | SCRUM-137 / D8, D15 — giờ làm việc, cách cuộc họp khác (hai phía), làm tròn giờ bắt đầu |
| `BR-423` | Each ordered pair of a calendar owner and a viewer has at most one Pending (Chờ đồng ý) or Effective (Đang hiệu lực) share, and nobody shares with themselves. The direction counts: while A views B's calendar, B may still ask for or receive a share of A's calendar. | UC-84a, UC-84b, UC-84c, UC-84d | SCRUM-142 / eCab — một chia sẻ Chờ HOẶC Hiệu lực mỗi cặp **có chiều** (chủ lịch → người xem) |
| `BR-424` | A share stops for good (Stopped — Đã dừng) as soon as the viewer is no longer a Meeting Organizer or either account is locked. It stays listed as stopped and reveals no hours. Becoming a Meeting Organizer again or unlocking the account does not restart it; viewing again needs a new share. | UC-84c, UC-85c | SCRUM-142 / D13 — PO chọn dừng hẳn, khác eCab (eCab tạm ngưng rồi tự chạy lại) |
| `BR-425` | A share closes itself after its end date (Expired — Hết hạn). Viewing the hours again needs a new share. | UC-85c | SCRUM-142 / eCab — hết hạn tự đóng |
| `BR-426` | Every share step — request, accept, decline, change, withdraw, end, stop and expiry — is audited and notifies the other side; a stop or an expiry notifies both sides. | UC-84a, UC-84b, UC-84c, UC-84d, UC-84e | SCRUM-142 / eCab — mọi bước ghi audit + thông báo bên kia |
| `BR-427` | Shared views and search results show only busy, free, unknown or not-shared. They never show a meeting title, location, attendee list or sensitivity level. Whose hours are busy may be shown only from the finder's own invite list. | UC-84a → UC-85c | SCRUM-137 / comment 10449 — chỉ bận/rảnh |
| `BR-428` | A slot that overlaps an undisclosed Secret meeting still allows a draft. The recheck before saving uses the same shared data as the search and produces no distinct error, warning, notification or audit because a Secret meeting overlaps. The find page always shows one fixed sentence that only shared calendars are counted and invitees may still be busy. | UC-85a, UC-85b | SCRUM-137 / D12 — khung trùng Mật vẫn cho tạo nháp (câu cố định thuộc UC-85a) |
| `BR-429` | A meeting created from a slot starts in Draft: the first listed invitee becomes Chair, the rest become Member and the finder becomes Secretary. It follows the normal approval flow. Creating a meeting does not check attendee conflicts. | UC-85b | SCRUM-137 / D12 — vai trò mặc định, luồng duyệt thường, không kiểm trùng |
| `BR-430` | Only a Meeting Organizer may request a share, receive a direct share, view shared calendars, find slots or create a draft from a slot; any active internal account may own a calendar and share it. | UC-84a → UC-85c (PRE) | SCRUM-142 / D5, D6 — ai được xin/nhận/xem/tìm, ai là chủ lịch |
| `BR-431` | A share's date range is valid when the start date is not after the end date, the end date is today or later, and the range covers at most 365 days counting both ends. A new share (UC-84a, UC-84b) starts today or later; a changed range of an Effective share (UC-84d) may keep its original start date. A range the owner shortens when accepting (UC-84c) must lie within the requested range. | UC-84a, UC-84b, UC-84c, UC-84d | SCRUM-142 / D16 (BA) — khoảng ngày chia sẻ hợp lệ |

## 5. Mã thông báo (dành sẵn)

Một mã = một tình huống. Dải số dời lên `MSG435`–`MSG455` vì PR #279 đã nhận `MSG424`–`MSG434`
(các mã chưa lên `main` nên số cũ không còn giá trị, bảng không ghi lại).
Thêm mã mới `MSG455`; mã tiếp theo `MSG456`.

| Mã | HTTP dự kiến | UC | Thông điệp đặc tả | Câu tiếng Việt cho UI |
|---|---|---|---|---|
| `MSG435` | 201 | UC-84a Main Flow | *Calendar share request sent.* | `Đã gửi yêu cầu xem lịch.` |
| `MSG436` | 200 | UC-84c Normal Flow | *Calendar share accepted.* | `Đã chấp nhận chia sẻ lịch.` |
| `MSG437` | 200 | UC-84c Normal Flow | *Calendar share request declined.* | `Đã từ chối yêu cầu chia sẻ.` |
| `MSG452` | 200 | UC-84c AF-1 | *Calendar share change declined.* | `Đã từ chối thay đổi chia sẻ.` |
| `MSG438` | 200 | UC-84e (Withdraw) | *Calendar share request withdrawn.* | `Đã rút yêu cầu chia sẻ lịch.` |
| `MSG451` | 200 | UC-84e (End) | *Calendar sharing ended.* | `Đã ngừng chia sẻ lịch.` |
| `MSG439` | — (nhãn/chi tiết) | UC-84 (danh sách), UC-85c Normal Flow | *This calendar share was stopped because the viewer is no longer a Meeting Organizer or an account was locked. Ask for a new share.* | `Chia sẻ lịch đã dừng vì người xem không còn quyền tạo cuộc họp hoặc tài khoản bị khoá. Hãy xin chia sẻ mới.` |
| `MSG440` | — (nhãn/chi tiết) | UC-84 (danh sách), UC-85c Normal Flow | *This calendar share has expired. Ask for a new share.* | `Chia sẻ lịch này đã hết hạn. Hãy xin chia sẻ mới.` |
| `MSG441` | 201 | UC-84b Main Flow | *Calendar shared. Waiting for acceptance.* | `Đã chia sẻ lịch. Đang chờ bên nhận đồng ý.` |
| `MSG442` | 200 | UC-84d AF-1, UC-84c AF-1 | *Calendar share updated.* | `Đã cập nhật chia sẻ lịch.` |
| `MSG454` | 201 | UC-84d Main Flow | *Calendar share change request sent.* | `Đã gửi yêu cầu thay đổi chia sẻ.` |
| `MSG443` | 400 | UC-84a EX-3, UC-84b EX-3, UC-84c EX-3, UC-84d EX-3 | *The date range is invalid.* | `Khoảng ngày không đúng.` |
| `MSG455` | 400 | UC-84b EX-1 | *This person cannot receive a calendar share.* | `Người này không nhận được chia sẻ lịch.` |
| `MSG448` | 403 | UC-84a EX-1, UC-85a EX-2, UC-85b EX-2 | *Only a Meeting Organizer can do this.* | `Bạn cần quyền tạo cuộc họp để thực hiện thao tác này.` |
| `MSG449` | 409 | UC-84a EX-2, UC-84b EX-2 | *There is already a pending or effective share with this person.* | `Đã có chia sẻ chờ hoặc hiệu lực với người này.` |
| `MSG450` | 409 | UC-84c EX-2, UC-84d EX-2, UC-84e EX-2 | *This action is not allowed in the current state of the share.* | `Không thể thực hiện ở trạng thái hiện tại của chia sẻ.` |
| `MSG444` | 400 | UC-85a EX-1 | *The meeting-time search is invalid. Check the highlighted fields.* | `Thông tin tìm giờ họp không hợp lệ. Kiểm tra các trường được đánh dấu.` |
| `MSG445` | 200 | UC-85a AF-1 | *No time slot fits everyone. Showing the slots with the most people free.* | `Không có khung giờ nào hợp với tất cả. Đang hiện các khung nhiều người dự được nhất.` |
| `MSG446` | 409 | UC-85b EX-1 | *This time slot is no longer free. Please choose another slot.* | `Khung giờ này không còn trống. Hãy chọn khung khác.` |
| `MSG453` | 500 | UC-85a EX-3, UC-85b EX-3, UC-85c EX-1 | *Unable to load the calendar sharing data. Please try again.* | `Không tải được dữ liệu chia sẻ lịch. Vui lòng thử lại.` |
| `MSG447` | 503 | UC-84a EX-5, UC-84b EX-4, UC-84c EX-4, UC-84d EX-4, UC-84e EX-3 | *Unable to save the calendar sharing data. Please try again.* | `Không lưu được dữ liệu chia sẻ lịch. Vui lòng thử lại.` |

Tạo nháp thành công dùng lại `MSG29` của UC-14 (`201 Tạo cuộc họp thành công`), không cấp mã mới.
Người lạ hỏi tới chia sẻ của người khác không được tiết lộ gì về chia sẻ hay tài khoản đó (bảng 403/404 của AGENTS.md), không có mã.

## 6. API dự kiến (cho BE, chưa phải contract)

Contract chính thức viết vào `docs/03-api/` trong PR code. Mọi endpoint kiểm tra lại quyền ở server.
Actor **Meeting Organizer** trong UC/BR là người có capability `MeetingCreator`: gác bằng `MeetingCreator` cho bên xin/xem/tìm (D6, BR-430); chủ lịch chỉ cần đăng nhập. Chia sẻ của người khác
không được xem → `404` không body.

| Method | Đường dẫn | Ghi chú |
|---|---|---|
| `POST` | `/api/calendar/shares` | UC-84a: body `{ ownerId, level, startDate, endDate }`. `201 MSG435`; mất quyền → `403 MSG448`; trùng chia sẻ → `409 MSG449`; khoảng sai → `400 MSG443`; chủ lịch không tồn tại → `404` không body |
| `POST` | `/api/calendar/shares/{id}/accept` | UC-84c: bên được hỏi chấp nhận, body tuỳ chọn `{ level, startDate, endDate }` khi chủ lịch chấp nhận thấp hơn (chỉ yêu cầu UC-84a). `200 MSG436` (yêu cầu/chia sẻ mới hiệu lực) hoặc `200 MSG442` (duyệt thay đổi của UC-84d) |
| `POST` | `/api/calendar/shares/{id}/decline` | UC-84c: từ chối yêu cầu/chia sẻ (`200 MSG437`) hoặc từ chối thay đổi (`200 MSG452`); người lạ gọi → `404` không body |
| `POST` | `/api/calendar/shares/direct` | UC-84b: chủ lịch tự chia sẻ, body `{ viewerId, startDate, endDate }` (luôn Mốc 1). `201 MSG441`; bên nhận không nhận được chia sẻ → `400 MSG455`; trùng/khoảng sai → `MSG449`/`MSG443` |
| `PATCH` | `/api/calendar/shares/{id}` | UC-84d AF-1: chủ lịch hạ mốc/đổi khoảng ngày. `200 MSG442`; sai trạng thái → `409 MSG450`; khoảng sai → `400 MSG443` |
| `POST` | `/api/calendar/shares/{id}/withdraw` | UC-84e: bên gửi rút yêu cầu/thay đổi đang chờ. `200 MSG438`; sai trạng thái → `409 MSG450` |
| `POST` | `/api/calendar/shares/{id}/end` | UC-84e AF-1: một trong hai bên ngừng chia sẻ. `200 MSG451`; sai trạng thái → `409 MSG450` |
| `GET` | `/api/calendar/shares` | UC-84: hai danh sách: tôi chia sẻ cho ai / ai chia sẻ cho tôi (kèm trạng thái Pending — Chờ đồng ý / Effective — Đang hiệu lực / Stopped — Đã dừng / Expired — Hết hạn / Closed; nhãn Stopped/Expired là `MSG439`/`MSG440`). `200`, không mã |
| `GET` | `/api/calendar/shared-week` | UC-85c: query `{ userIds, week }`. `200`: khối bận ở mốc được chia sẻ, không tên cuộc họp; người dừng/hết hạn hiện nhãn trạng thái (`MSG439`/`MSG440` khi bấm vào người đó), giờ bị loại khỏi tuần. Không đọc được → `500 MSG453` |
| `POST` | `/api/calendar/slots` | UC-85a: body `{ userIds, includeMe, durationMinutes, fromDate, toDate, weekdays, workStart, workEnd, lunchStart, lunchEnd, bufferMinutes, stepMinutes }`. `200` khung chung theo ngày, hoặc `200 MSG445` kèm khung nhiều người dự nhất. Tham số sai → `400 MSG444`; mất quyền → `403 MSG448`; không đọc được → `500 MSG453` |
| `POST` | `/api/meetings` (dùng lại UC-14) | UC-85b: tạo nháp từ khung giờ, body điền sẵn giờ + danh sách. `201 MSG29`; kiểm lại khung theo đúng dữ liệu đã chia sẻ, không còn hợp → `409 MSG446`; không đọc được → `500 MSG453`; không lưu được → `500 MSG35` |

- Hết hạn tự đóng bằng job nền (mốc nửa đêm, múi giờ máy chủ — chốt ở §7 Q4). Dừng (D13) là trạng thái **lưu
  lại**, không suy ra lúc đọc: chuyển sang `Stopped` ngay trong thao tác gỡ vai trò làm mất `MeetingCreator` và
  thao tác khoá tài khoản, kèm audit + thông báo hai bên; không có đường nào đưa `Stopped` về `Effective`.
- Thông báo hai chiều dùng hạ tầng `Notification` sẵn có (`TargetType`/`TargetId` + `DeduplicationKey`);
  audit dùng `AuditLogEntry` append-only. Nội dung chi tiết do BE đặt khi viết contract.
- Dữ liệu: đề xuất một bảng chia sẻ mới (tên và cột do BE đặt khi làm, không sửa ERD trong note này) —
  gợi ý: `calendar_shares(id, owner_user_id, viewer_user_id, level [L1/L2], start_date, end_date,
  status [Pending/Effective/Declined/Withdrawn/Ended/Stopped/Expired], requested_by, decided_by,
  decided_at, created_at, updated_at)`, unique một chia sẻ Pending/Effective mỗi cặp **có chiều** `(owner_user_id, viewer_user_id)` (BR-423):
  A xem lịch B không chặn B xin xem lịch A; dòng
  Stopped/Expired không chặn yêu cầu mới. BE cập nhật [02-erd](../02-design/02-erd.md) cùng migration trong PR code.

## 7. Câu hỏi cần PO / BA chốt

| # | Câu hỏi | Đề xuất trong note |
|---|---|---|
| Q4 | Hết hạn tự đóng chạy lúc nào (nửa đêm, múi giờ nào) và có báo cho hai bên trước khi đóng không? | Job nửa đêm múi giờ máy chủ, báo cả hai bên khi đóng (BR-426) |
| Q7 | Câu cố định trên màn tìm giờ (BR-428) dùng đúng chữ nào? | `Chỉ tính lịch đã được chia sẻ. Người được mời vẫn có thể bận.` — BA chốt chữ cuối |
| Q8 | Khung nhiều người dự nhất (UC-85a AF-1) được hiện tên ai bận — chỉ tên trong danh sách mời của người tìm, đúng không? | Đúng; không bao giờ hiện tên/địa điểm/người dự của cuộc họp chủ lịch (BR-427) |
| Q9 | Danh sách trạng thái tính bận ở BR-420 đã đủ chưa: Chờ duyệt / Đã duyệt / Đã công bố / Đang họp / Đã kết thúc tính bận (đến giờ kết thúc thật); Nháp / Bị từ chối / Đã huỷ không tính? | Chờ PO xác nhận |
| Q10 | Khoảng ngày chia sẻ dài tối đa bao nhiêu (BR-431, D16)? eCab cho phép không giới hạn | 365 ngày tính cả hai đầu; chia sẻ mới bắt đầu từ hôm nay |

## 8. Việc còn lại

- [ ] BE: contract trong `docs/03-api/`, bảng `calendar_shares` + migration + cập nhật `02-erd.md`, code,
      test; đổi `MSG435`–`MSG455` từ *Dành sẵn* sang *BE* khi có code. Không dùng literal `"MSGnn"` trong code
      (MessageCodeCatalogueTests).
- [x] BA đưa UC-84/UC-85 gom (UC-84a → UC-84e, UC-85a → UC-85c), BR-414 → BR-430, `MSG435`–`MSG455` vào DOCX Report 3
      (xong 07/11/2026 trên nhánh này: sync `--report 3` OK, `check_docx.py` OK không thêm cảnh báo,
      `MessageCodeCatalogueTests` + `SkillMirrorTests` 74/74 pass; UC-82/UC-83, BR-397 → BR-413, `MSG408`–`MSG434`
      để trống cho SCRUM-30/SCRUM-33; UC-86 → UC-90 không dùng).
- [ ] BA Final Review (phần còn lại): cập nhật inventory, UC diagram, màn FE-16 (§I.5.1 + §III) sau khi FE có màn
      chạy thật (quy trình note 06); sửa mô tả SCRUM-137/SCRUM-142 theo D1–D3 (§2) và chốt Q4, Q7, Q8, Q9.
- [ ] Ticket màn FE-16 tạo riêng (note 36 §6); cập nhật `scopeData`/landing ở PR màn hình, không ở đây.
- [ ] Comment cần đăng lên Jira sau khi note này merge (xem §2 + D12–D16, không sửa ticket trong task này):
  - SCRUM-142: D12 (khung trùng Mật vẫn cho tạo nháp, kiểm lại theo dữ liệu đã chia sẻ, câu cố định trên màn);
    D2 (mô tả còn để 3 phương án mức xem); D3 (diễn đạt "Meeting Organizer" trong mô tả); UC-84 gom
    (UC-84a xin xem, UC-84b chủ lịch tự chia sẻ, UC-84c trả lời, UC-84d thay đổi, UC-84e rút/ngừng),
    BR-414 → BR-427 + BR-430, BR-431 (phần chia sẻ), `MSG435`–`MSG443` + `MSG448`–`MSG452` + `MSG454`–`MSG455` dành sẵn
    (đổi số từ `MSG424`–`MSG443` vì PR #279; mã mới `MSG455` cho bên nhận không nhận được chia sẻ;
    UC-84c EX-1 tách người lạ (không tiết lộ gì) khỏi sai trạng thái; UC-84c không tự dừng chia sẻ: bên xem mất quyền thì chia sẻ đã dừng ngay lúc đó, BR-424/D13; `MSG439` chỉ cho danh sách và UC-85c).
  - SCRUM-137: D12 (như trên) + D1 (mô tả còn dòng "không có ủy quyền xem lịch" và "tab Lịch bận chung để sau");
    UC-85 gom (UC-85a tìm giờ, UC-85b tạo nháp `<<extend>>` UC-85a, UC-85c xem lịch),
    BR-414 → BR-422 + BR-427 → BR-430 (phần tìm giờ), `MSG444`–`MSG447` + `MSG453` dành sẵn
    (đổi số từ `MSG433`–`MSG436` + `MSG442`; UC-85b không lưu được dùng lại `MSG35` của UC-14;
    dừng/hết hạn thành nhãn, không còn 409/410 cho cả tuần; Q9 chờ PO xác nhận trạng thái tính bận).
