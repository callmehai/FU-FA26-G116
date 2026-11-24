# Luồng nghiệp vụ chính (Main Business Flows)

> Diễn giải **4 Main Business Flow** BF-01 → BF-04 của TrustCabinet, bản PO duyệt 09/10/2026. Sơ đồ swimlane gốc ở
> [09-bussinessflow/](../09-bussinessflow/README.md); Report 3 §I.2 dùng cùng 4 luồng này.
> **Liên kết:** [Vòng đời cuộc họp](01-vong-doi-cuoc-hop.md) · [ERD](02-erd.md) ·
> [Master UC Inventory](../01-requirements/00-master-use-case-inventory.md) · [Đặc tả Use Case](../01-requirements/05-use-case-specifications.md) ·
> [Business Rules](../01-requirements/06-business-rules.md).
> **Nguyên tắc:** chỉ giữ mốc nghiệp vụ, điểm chuyển giao giữa actor, thay đổi trạng thái và điểm quyết định quan trọng;
> không biến mọi Use Case thành một ô. Số UC theo đợt đánh số lại 24/11/2026 (UC-01 → UC-81, [bảng 10](../01-requirements/10-bang-anh-xa-uc-dot-1.md)).

---

# 1. Tổng quan

```text
BF-01 System Access & Administration
   → người dùng có tài khoản, vai trò, quyền phù hợp
BF-02 Meeting Preparation, Approval & Participant Coordination
   → Draft → WaitingApproval → Approved | Rejected → Published (lời mời đã gửi)
BF-03 Meeting Execution & Decision Making
   → Published → InProgress → Ended (agenda, biểu quyết, kết luận đã ghi)
BF-04 Post-Meeting Processing & Formal Records
   → Audio → Transcript → Tóm tắt → Biên bản → Duyệt và ban hành
```

Ba quyết định PO 09/10/2026 định hình các luồng này:

1. **Không có Meeting Session.** Một cuộc họp là một buổi họp; lịch, agenda, biểu quyết, ghi âm, biên bản gắn thẳng vào
   cuộc họp ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)).
2. **Biểu quyết chỉ trong cuộc họp.** Không vote trước / sau họp, không quorum, không đối tượng Resolution. Chair ghi kết
   luận *Passed / NotPassed / Deferred* sau khi đóng phiếu ([ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md)).
   Vì vậy không còn "Shared Voting Sub-flow" — biểu quyết là một nhánh trong vòng agenda của BF-03.
3. **Thư ký tự upload audio.** Không có vai trò Audio Uploader.

---

# 2. BF-01 – System Access & Administration

**Trigger:** một nhân sự nội bộ cần được cấp quyền truy cập, hoặc cần đổi quyền đang có.
**Kết thúc:** người dùng có tài khoản hợp lệ với vai trò và quyền phù hợp, truy cập đúng các chức năng được phép.

| Swimlane | Việc chính |
|---|---|
| System Admin | Tạo / quản lý vai trò tuỳ chỉnh (UC-11), cấu hình quyền (UC-10), tạo / cập nhật tài khoản (UC-06), gán vai trò (UC-08) |
| User | Mở TrustCabinet, chọn đăng nhập Google hoặc email + mật khẩu, dùng các chức năng được phép |
| TrustCabinet | Lưu thay đổi và ghi audit log (BR-009–BR-011); kiểm tra thông tin đăng nhập, liên kết tài khoản, trạng thái active; quá số lần cho phép trong một phút thì báo lỗi MSG414 |
| OAuth Provider | Xác minh tài khoản Google và trả danh tính (UC-02) |

**Luồng:**

1. System Admin chuẩn bị vai trò, quyền và tài khoản; mỗi thay đổi được lưu và ghi audit log.
2. Người dùng mở TrustCabinet và chọn cách đăng nhập.
   - **Email + mật khẩu (UC-01):** sai thông tin → báo lỗi (MSG01, không lộ email có tồn tại hay không — BR-004); sai quá
     số lần cho phép trong một phút → hệ thống báo lỗi MSG414, thử lại sau ít phút.
   - **Google (UC-02):** Google xác minh tài khoản; hệ thống kiểm tra email đã liên kết tài khoản nội bộ (BR-003, không tự
     tạo tài khoản — BR-003).
3. Tài khoản bị vô hiệu hoá → báo lỗi (BR-001). Hợp lệ → đăng nhập và nạp quyền theo vai trò (BR-002).
4. Đặt lại mật khẩu (UC-07): Admin gửi link một lần, hiệu lực có hạn (UC-07); vô hiệu hoá tài khoản (UC-06c) chấm dứt mọi
   phiên đăng nhập đang có (BR-001).

---

# 3. BF-02 – Meeting Preparation, Approval & Participant Coordination

**Trigger:** Meeting Organizer cần tổ chức một cuộc họp mới.
**Kết thúc:** cuộc họp `Published`, lời mời đã gửi, người được mời phản hồi được, cuộc họp sẵn sàng diễn ra.

| Swimlane | Việc chính |
|---|---|
| Meeting Organizer | Tạo cuộc họp, gán/phục hồi Thư ký khi không có trong giai đoạn chuẩn bị, chọn người duyệt, gửi duyệt, sửa khi bị từ chối, công bố, dời lịch, huỷ cuộc họp |
| Secretary | Quản lý thành phần và mời Guest, gán vai trò (Chair, thành viên), chuẩn bị agenda, thêm tài liệu, bổ sung vai trò còn thiếu |
| TrustCabinet | Lưu nháp; chuyển trạng thái và báo người liên quan; kiểm tra đủ Chair và Secretary; tạo lời mời; lưu phản hồi; lưu lịch mới |
| Meeting Approver | Xem xét và duyệt / từ chối |
| Participant / Guest | Mở lời mời (Guest xác minh email), chấp nhận / từ chối |
| Email Service | Gửi thư mời, gửi thông báo đổi lịch |

**Luồng:**

1. **Tạo (UC-13):** Organizer nhập tiêu đề, lịch (`PlannedStartTime` ở tương lai, `PlannedEndTime` sau giờ bắt đầu),
   hình thức, địa điểm, link tuỳ chọn, độ mật (BR-013, UC-13). Hệ thống lưu `Draft` (BR-013).
2. **Gán Thư ký đầu tiên (UC-30a):** khi cuộc họp chưa có Thư ký, chỉ Organizer được thêm người đầu tiên và người đó phải là
   Secretary. Từ đó Thư ký quản lý danh sách (BR-036, BR-037).
3. **Thư ký chuẩn bị:** thêm thành viên nội bộ và mời Guest theo tên + email (UC-30a, BR-038, BR-049–BR-051, BR-039),
   gán vai trò Chair / Member (UC-30b, tối đa một Chair — BR-040; Thư ký chỉ thay Chair trước khi công bố — BR-041, BR-042),
   soạn agenda (UC-23, BR-058, BR-059, BR-064, BR-065), thêm tài liệu (UC-36), chuẩn bị nháp biểu quyết và tài liệu của nó
   khi cuộc họp `Approved` / `Published` (UC-63, UC-68, BR-086, BR-087).
4. **Gửi duyệt (UC-17):** Organizer chọn người duyệt và gửi; cuộc họp chuyển `WaitingApproval`, người duyệt được báo
   (UC-17, BR-018, BR-019). Thông tin chung bị khoá; danh sách thành phần chỉ đọc; agenda vẫn sửa được (BR-018, BR-058, BR-059).
5. **Duyệt (UC-18):** người duyệt được phân công xem cuộc họp.
   - **Từ chối:** bắt buộc lý do → `Rejected`, báo Organizer (BR-020). Organizer sửa (UC-14) và gửi lại từ bước 4.
   - **Duyệt:** → `Approved`, báo Organizer.
6. **Công bố (UC-19):** hệ thống kiểm tra đủ đúng một Chair và ít nhất một Secretary đang hoạt động (BR-021).
   Thiếu → Thư ký bổ sung vai trò còn thiếu rồi công bố lại. Đủ → `Published`, tạo lời mời cho thành viên và Guest;
   email gửi bất đồng bộ, lỗi gửi không làm mất trạng thái `Published` (BR-021, BR-031, BR-032).
7. **Phản hồi lời mời (UC-31):** thành viên nội bộ mở lời mời và chọn *Accepted* / *Declined* (BR-046, không có
   uỷ quyền). Guest mở link, xác minh email bằng mã một lần (UC-04, BR-052–BR-054) rồi phản hồi. Phản hồi đổi được tới khi
   cuộc họp kết thúc.
8. **Dời lịch sau công bố (UC-14 — *Reschedule*):** Organizer chỉ đổi giờ, địa điểm, link (BR-015), giờ bắt đầu mới phải
   ở tương lai (BR-015). Hệ thống lưu lịch mới và báo lại toàn bộ người được mời; lỗi gửi không huỷ thay đổi (UC-14).
9. **Huỷ cuộc họp (UC-22):** bất cứ lúc nào trước khi bắt đầu (`Draft` → `Published`), Organizer huỷ với lý do bắt buộc;
   cuộc họp thành `Cancelled`, chỉ đọc. Nếu đã công bố, người được mời được báo kèm lý do (BR-028, UC-22). Cuộc họp
   `Published` chưa bắt đầu 24 giờ sau giờ kết thúc dự kiến thì hệ thống tự huỷ (BR-029).

---

# 4. BF-03 – Meeting Execution & Decision Making

**Trigger:** Chair quyết định bắt đầu một cuộc họp `Published`.
**Kết thúc:** cuộc họp `Ended`; kết quả agenda, kết quả biểu quyết và kết luận đã được ghi.

| Swimlane | Việc chính |
|---|---|
| Chair | Bắt đầu họp, bắt đầu một mục agenda bất kỳ đang chờ, mở – đóng biểu quyết, ghi kết luận biểu quyết và kết luận của mục, hoàn thành mục, giao chủ toạ, kết thúc họp |
| Participant / Guest | Vào phòng họp trực tuyến (Online / Hybrid), thảo luận (tài liệu, chat, chia sẻ màn hình), xác nhận đã đọc tài liệu biểu quyết, bỏ phiếu |
| Secretary | Ghi điểm danh chính thức, chuẩn bị nháp biểu quyết, ghi kết luận của mục |
| TrustCabinet | Chuyển `InProgress` và báo người tham dự; kiểm phiếu; lưu trạng thái và thời gian thực tế của mục agenda; chuyển `Ended` và đóng phòng họp |

**Luồng:**

1. **Bắt đầu (UC-20):** Chair bắt đầu; hệ thống kiểm tra đủ Chair và Secretary đang hoạt động (BR-024),
   chuyển `InProgress`, ghi giờ bắt đầu thực tế, báo thông báo trong ứng dụng cho người được mời (UC-20).
2. **Vào phòng họp (UC-43 → UC-48)** nếu cuộc họp Online / Hybrid. Guest vào qua guest session của UC-04. Phòng họp không
   đổi vòng đời cuộc họp.
3. **Điểm danh (UC-33):** Trong lúc cuộc họp `InProgress`, Thư ký lưu *Present / Absent* cho một/nhiều người được chọn trong roster nội bộ và Guest; người không gửi giữ nguyên giá trị đã lưu hoặc Chưa điểm danh. Một submission lưu nguyên khối; không bắt đủ roster. Điểm danh độc lập với phản hồi lời mời và trạng thái đang kết nối phòng họp (BR-043, BR-056, BR-057, UC-33).
4. **Vòng agenda** — lặp cho từng mục:
   1. Chair bắt đầu một mục `Pending` **bất kỳ**, không cần theo thứ tự (UC-24): mục → `InProgress`, ghi giờ bắt đầu;
      tối đa một mục đang chạy (BR-060–BR-062).
   2. Thảo luận với tài liệu, chat, chia sẻ màn hình.
   3. **Cần biểu quyết?** Có → Chair mở biểu quyết từ nháp Chair hoặc Thư ký đã chuẩn bị (UC-63, UC-64; danh sách tài liệu
      chốt khi mở — BR-094, BR-095) → người được bỏ phiếu xác nhận đã đọc từng tài liệu của biểu quyết (UC-42) rồi bỏ phiếu (UC-65;
      Guest đã xác minh được bỏ phiếu như Member) → Chair đóng (UC-66) → hệ thống kiểm phiếu và hiện kết quả (UC-62b) → Chair ghi kết luận
      *Passed / NotPassed / Deferred* (UC-67). Áp dụng BR-090, BR-099, BR-092, BR-093.
   4. Chair hoặc Thư ký ghi kết luận của mục (UC-27, BR-066, BR-067); sửa được tới khi sinh nháp biên bản.
   5. Chair hoàn thành mục (UC-25): mục → `Done`, ghi giờ kết thúc (BR-060–BR-062).
   6. Còn mục → quay lại 4.1.
   - Bất cứ lúc nào khi `Published` / `InProgress`, Chair có thể giao chủ toạ cho thành phần tham dự nội bộ khác (UC-34);
     Chair cũ thành Member (BR-041, BR-042).
5. **Kết thúc (UC-21):** Chair kết thúc bất cứ lúc nào khi InProgress, không kiểm tra Vote Open hay điểm danh đủ/thiếu (BR-026). Vote giữ trạng thái và phiếu đã lưu; Meeting Ended ngừng nhận phiếu. Mục agenda đang chạy (nếu có) được hoàn tất cùng lúc cuộc họp chuyển sang `Ended` và ghi giờ kết thúc thực tế. Các mục còn chờ vẫn giữ nguyên trạng thái; hệ thống đóng quyền vào phòng họp theo quy tắc hiện hành (BR-025, BR-026).

---

# 5. BF-04 – Post-Meeting Processing & Formal Records

**Trigger:** cuộc họp đã `Ended` và cần xử lý hồ sơ sau họp.
**Kết thúc:** biên bản đã được Chủ tọa duyệt và ban hành; hồ sơ cuộc họp tra cứu được theo quyền.

| Swimlane | Việc chính |
|---|---|
| TrustCabinet | Nhắc Thư ký upload audio; kiểm tra file; lưu vào thư viện media và xếp hàng xử lý; chép lời; khoá và lưu biên bản đã ban hành |
| AI Provider | Sinh tóm tắt; sinh biên bản |
| Secretary | Upload audio, sửa transcript, yêu cầu sinh và sửa tóm tắt, yêu cầu sinh và sửa biên bản, gửi duyệt biên bản |
| Chair | Chủ tọa: duyệt và ban hành biên bản, hoặc trả lại để sửa |
| Authorized User | Tra cứu hồ sơ cuộc họp |

**Luồng:**

1. **Upload audio (UC-52):** hệ thống nhắc Thư ký; Thư ký upload file (MP3 / M4A / WAV) và xác nhận đã có đồng ý ghi âm.
   File không hợp lệ → báo lỗi. Hợp lệ → lưu vào thư viện media (UC-51) và xếp hàng xử lý.
   *Dữ liệu cá nhân theo Nghị định 13/2023/NĐ-CP:* lưu căn cứ đồng ý, mục đích và hạn lưu (`Recording.ConsentObtainedAt`,
   `ConsentPurpose`, `RetainUntil`); xoá theo hạn lưu.
2. **Chép lời:** hệ thống chép lời từ audio. Lỗi → báo lỗi và cho thử lại.
3. **Rà transcript (UC-54, UC-55):** Thư ký sửa transcript.
4. **Tóm tắt (UC-58 → UC-60):** Thư ký yêu cầu sinh tóm tắt; AI sinh; Thư ký rà và sửa. Không có bước duyệt hay công
   bố tóm tắt riêng — tóm tắt đi thẳng vào biên bản.
5. **Biên bản (UC-70, UC-71):** Thư ký yêu cầu sinh biên bản; AI sinh bản nháp từ thông tin cuộc họp, tóm tắt và kết quả
   biểu quyết. Thư ký rà và sửa.
6. **Duyệt và ban hành (UC-72, UC-62 cũ, UC-63 cũ, UC-73, UC-74):** Thư ký gửi duyệt. Chủ tọa duyệt là ban hành luôn
(BR-009–BR-011); không duyệt thì trả lại kèm lý do cho Thư ký sửa rồi gửi lại. Không có bước ký hay niêm phong
(PO 14/11/2026 bỏ ký số kể cả mô phỏng, SCRUM-37 đóng).
7. **Đính chính (UC-75):** sửa sau ban hành thì tạo bản đính chính mới đi lại UC-72, UC-62 cũ, UC-63 cũ, UC-73; bản cũ thành `Superseded`.
8. **Tra cứu (UC-69, UC-79):** người có quyền xem hồ sơ cuộc họp, báo cáo thống kê.

---

# 6. Quan hệ giữa các luồng

```text
BF-01 ──(tài khoản, vai trò)──> BF-02 ──(Published, lời mời)──> BF-03 ──(Ended, kết quả biểu quyết)──> BF-04
                                  ↑ │
                                  └─┘ vòng duyệt: Rejected → sửa → gửi lại
```

- BF-02 có vòng duyệt lặp tới khi `Approved`, và nhánh dời lịch sau khi `Published`.
- BF-03 lặp theo từng mục agenda; biểu quyết là nhánh tuỳ chọn trong vòng lặp.
- BF-04 có hai vòng lặp: chép lời lỗi → thử lại; Chủ tọa trả biên bản → Thư ký sửa → gửi duyệt lại.

# 7. Quy tắc khi vẽ / sửa swimlane

- Mỗi ô là một mốc nghiệp vụ hoặc một quyết định; tên ô tiếng Anh, động từ đứng đầu.
- Ô quyết định chỉ có nhánh *Yes / No*.
- Không vẽ Meeting Session, Resolution, quorum, Audio Uploader, uỷ quyền tham dự — đã bỏ khỏi phạm vi.
- Sửa `.drawio` trong [09-bussinessflow/](../09-bussinessflow/README.md), xuất lại `.drawio.png`, cập nhật file này nếu
  luồng đổi.

## Guest — quyết định PO 13/10/2026

Tên/email do Thư ký mời → UC-04 OTP → Guest tự khai tên hiển thị và chức vụ tùy chọn → Member participation trong đúng Meeting. Toàn bộ tài liệu gắn vào Meeting, comments và biểu quyết áp dụng như Member; quyền bổ sung do Thư ký cấp như Member, không giới hạn hai quyền roster/response. Mời vào cuộc họp Secret hiện cảnh báo trong form bình thường, không thêm xác nhận. [ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md). Chưa điểm danh khác Vắng, không chặn kết thúc và không tự chuyển thành Vắng khi sinh biên bản/báo cáo.
