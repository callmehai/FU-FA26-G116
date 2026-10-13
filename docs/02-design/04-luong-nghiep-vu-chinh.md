# Luồng nghiệp vụ chính (Main Business Flows)

> Diễn giải **4 Main Business Flow** BF-01 → BF-04 của TrustCabinet, bản PO duyệt 09/10/2026. Sơ đồ swimlane gốc ở
> [09-bussinessflow/](../09-bussinessflow/README.md); Report 3 §I.2 dùng cùng 4 luồng này.
> **Liên kết:** [Vòng đời cuộc họp](01-vong-doi-cuoc-hop.md) · [ERD](02-erd.md) ·
> [Master UC Inventory](../01-requirements/00-master-use-case-inventory.md) · [Đặc tả Use Case](../01-requirements/05-use-case-specifications.md) ·
> [Business Rules](../01-requirements/06-business-rules.md).
> **Nguyên tắc:** chỉ giữ mốc nghiệp vụ, điểm chuyển giao giữa actor, thay đổi trạng thái và điểm quyết định quan trọng;
> không biến mọi Use Case thành một ô. Số UC theo baseline 10/10/2026 (UC-01 → UC-70).

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
   → Audio → Transcript → Tóm tắt → Biên bản → Ký → Niêm phong
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
| System Admin | Tạo / quản lý vai trò tuỳ chỉnh (UC-12), cấu hình quyền (UC-11), tạo / cập nhật tài khoản (UC-06), gán vai trò (UC-08), mở khoá tài khoản (UC-09) |
| Internal User | Mở TrustCabinet, chọn đăng nhập Google hoặc email + mật khẩu, dùng các chức năng được phép |
| TrustCabinet | Lưu thay đổi và ghi audit log (BR-16); kiểm tra thông tin đăng nhập, liên kết tài khoản, trạng thái active; khoá tài khoản khi sai quá số lần |
| Google Identity Provider | Xác minh tài khoản Google và trả danh tính (UC-02) |

**Luồng:**

1. System Admin chuẩn bị vai trò, quyền và tài khoản; mỗi thay đổi được lưu và ghi audit log.
2. Người dùng mở TrustCabinet và chọn cách đăng nhập.
   - **Email + mật khẩu (UC-01):** sai thông tin → báo lỗi (MSG01, không lộ email có tồn tại hay không — BR-02); sai quá
     số lần cho phép → hệ thống khoá tài khoản, Admin mở khoá bằng UC-09.
   - **Google (UC-02):** Google xác minh tài khoản; hệ thống kiểm tra email đã liên kết tài khoản nội bộ (BR-06, không tự
     tạo tài khoản — BR-07).
3. Tài khoản bị vô hiệu hoá → báo lỗi (BR-03). Hợp lệ → đăng nhập và nạp quyền theo vai trò (BR-08).
4. Đặt lại mật khẩu (UC-07): Admin gửi link một lần, hiệu lực có hạn (BR-20); vô hiệu hoá tài khoản (UC-06c) chấm dứt mọi
   phiên đăng nhập đang có (BR-18).

---

# 3. BF-02 – Meeting Preparation, Approval & Participant Coordination

**Trigger:** Meeting Organizer cần tổ chức một cuộc họp mới.
**Kết thúc:** cuộc họp `Published`, lời mời đã gửi, người được mời phản hồi được, cuộc họp sẵn sàng diễn ra.

| Swimlane | Việc chính |
|---|---|
| Meeting Organizer | Tạo cuộc họp, gán/phục hồi Thư ký khi không có trong giai đoạn chuẩn bị, chọn người duyệt, gửi duyệt, sửa khi bị từ chối, công bố, dời lịch |
| Secretary | Quản lý thành phần và mời Guest, gán vai trò (Chair, thành viên), chuẩn bị agenda, thêm tài liệu, bổ sung vai trò còn thiếu |
| TrustCabinet | Lưu nháp; chuyển trạng thái và báo người liên quan; kiểm tra đủ Chair và Secretary; tạo lời mời; lưu phản hồi; lưu lịch mới |
| Meeting Approver | Xem xét và duyệt / từ chối |
| Participant / Guest | Mở lời mời (Guest xác minh email), chấp nhận / từ chối |
| Email Service | Gửi thư mời, gửi thông báo đổi lịch |

**Luồng:**

1. **Tạo (UC-14):** Organizer nhập tiêu đề, lịch (`PlannedStartTime` ở tương lai, `PlannedEndTime` sau giờ bắt đầu),
   hình thức, địa điểm, link tuỳ chọn, độ mật (BR-24, BR-62, BR-162, BR-163). Hệ thống lưu `Draft` (BR-26).
2. **Gán Thư ký đầu tiên (UC-28a):** khi cuộc họp chưa có Thư ký, chỉ Organizer được thêm người đầu tiên và người đó phải là
   Secretary (BR-78). Từ đó Thư ký quản lý danh sách (BR-79).
3. **Thư ký chuẩn bị:** thêm thành viên nội bộ và mời Guest theo tên + email (UC-28a, BR-81 → BR-88, BR-148 → BR-151),
   gán vai trò Chair / Member (UC-28b, tối đa một Chair — BR-123), soạn agenda (UC-23, BR-64 → BR-68), thêm tài liệu (UC-33).
4. **Gửi duyệt (UC-18):** Organizer chọn người duyệt và gửi; cuộc họp chuyển `WaitingApproval`, người duyệt được báo
   (BR-33 → BR-35). Thông tin chung bị khoá; danh sách thành phần chỉ đọc; agenda vẫn sửa được (BR-34, BR-74).
5. **Duyệt (UC-19):** người duyệt được phân công xem cuộc họp.
   - **Từ chối:** bắt buộc lý do → `Rejected`, báo Organizer (BR-36). Organizer sửa (UC-15) và gửi lại từ bước 4.
   - **Duyệt:** → `Approved`, báo Organizer.
6. **Công bố (UC-20):** hệ thống kiểm tra đủ đúng một Chair và ít nhất một Secretary đang hoạt động (BR-124, BR-125).
   Thiếu → Thư ký bổ sung vai trò còn thiếu rồi công bố lại. Đủ → `Published`, tạo lời mời cho thành viên và Guest;
   email gửi bất đồng bộ, lỗi gửi không làm mất trạng thái `Published` (BR-37, BR-39 → BR-43).
7. **Phản hồi lời mời (UC-29):** thành viên nội bộ mở lời mời và chọn *Accepted* / *Declined* (BR-104 → BR-106, không có
   uỷ quyền). Guest mở link, xác minh email bằng mã một lần (UC-04, BR-152 → BR-156) rồi phản hồi. Phản hồi đổi được tới khi
   cuộc họp kết thúc.
8. **Dời lịch sau công bố (UC-15 — *Reschedule*):** Organizer chỉ đổi giờ, địa điểm, link (BR-158), giờ bắt đầu mới phải
   ở tương lai (BR-75). Hệ thống lưu lịch mới và báo lại toàn bộ người được mời; lỗi gửi không huỷ thay đổi (BR-76).

---

# 4. BF-03 – Meeting Execution & Decision Making

**Trigger:** Chair quyết định bắt đầu một cuộc họp `Published`.
**Kết thúc:** cuộc họp `Ended`; kết quả agenda, kết quả biểu quyết và kết luận đã được ghi.

| Swimlane | Việc chính |
|---|---|
| Chair | Bắt đầu họp, đặt mục agenda hiện tại, tạo – mở – đóng biểu quyết, ghi kết luận, hoàn thành mục, kết thúc họp |
| Participant / Guest | Vào phòng họp trực tuyến (Online / Hybrid), thảo luận (tài liệu, chat, chia sẻ màn hình), bỏ phiếu |
| Secretary | Ghi điểm danh chính thức |
| TrustCabinet | Chuyển `InProgress` và báo người tham dự; kiểm phiếu; lưu trạng thái và thời gian thực tế của mục agenda; chuyển `Ended` và đóng phòng họp |

**Luồng:**

1. **Bắt đầu (UC-21):** Chair bắt đầu; hệ thống kiểm tra đủ Chair và Secretary đang hoạt động (BR-127, BR-130, BR-131),
   chuyển `InProgress`, ghi giờ bắt đầu thực tế, báo thông báo trong ứng dụng cho người được mời (BR-164).
2. **Vào phòng họp (UC-37 → UC-42)** nếu cuộc họp Online / Hybrid. Guest vào qua guest session của UC-04. Phòng họp không
   đổi vòng đời cuộc họp.
3. **Điểm danh (UC-31):** Trong lúc cuộc họp `InProgress`, Thư ký lưu *Present / Absent* cho một/nhiều người được chọn trong roster nội bộ và Guest; người không gửi giữ nguyên giá trị đã lưu hoặc Chưa điểm danh. Một submission lưu nguyên khối; không bắt đủ roster. Điểm danh độc lập với phản hồi lời mời và trạng thái đang kết nối phòng họp (BR-116, BR-170–BR-172, BR-178).
4. **Vòng agenda** — lặp cho từng mục:
   1. Chair đặt mục hiện tại (UC-24): mục `Pending` → `InProgress`, ghi giờ bắt đầu; tối đa một mục đang chạy (BR-69 → BR-71).
   2. Thảo luận với tài liệu, chat, chia sẻ màn hình.
   3. **Cần biểu quyết?** Có → Chair tạo và mở biểu quyết (UC-53, UC-54) → Chair, Secretary, Member bỏ phiếu (UC-55;
      Guest đã xác minh được bỏ phiếu như Member) → Chair đóng (UC-56) → hệ thống kiểm phiếu và hiện kết quả (UC-52b) → Chair ghi kết luận
      *Passed / NotPassed / Deferred* (UC-57). Áp dụng BR-160, BR-161, BR-165.
   4. Chair hoàn thành mục (UC-25): mục → `Done`, ghi giờ kết thúc và thời lượng thực tế (BR-70, BR-72).
   5. Còn mục → quay lại 4.1.
5. **Kết thúc (UC-22):** Chair kết thúc bất cứ lúc nào khi InProgress, không kiểm tra Vote Open hay điểm danh đủ/thiếu (BR-159). Vote giữ trạng thái và phiếu đã lưu; Meeting Ended ngừng nhận phiếu. Mục agenda đang chạy (nếu có) được hoàn tất cùng lúc cuộc họp chuyển sang `Ended` và ghi giờ kết thúc thực tế. Các mục còn chờ vẫn giữ nguyên trạng thái; hệ thống đóng quyền vào phòng họp theo quy tắc hiện hành (BR-143, BR-159).

---

# 5. BF-04 – Post-Meeting Processing & Formal Records

**Trigger:** cuộc họp đã `Ended` và cần xử lý hồ sơ sau họp.
**Kết thúc:** biên bản đã ký, hoàn tất và niêm phong; hồ sơ cuộc họp tra cứu được theo quyền.

| Swimlane | Việc chính |
|---|---|
| TrustCabinet | Nhắc Thư ký upload audio; kiểm tra file; lưu vào thư viện media và xếp hàng xử lý; công bố tóm tắt; tổng hợp bản nháp biên bản; gửi yêu cầu ký; huỷ chữ ký cũ khi sửa; lưu chữ ký và lịch sử; khoá và lưu biên bản chính thức |
| AI / Speech Processing Provider | Chép lời và nhận diện người nói; sinh tóm tắt |
| Secretary | Upload audio, sửa transcript và người nói, sinh – duyệt – công bố tóm tắt, sinh và gửi ký biên bản, sửa biên bản, niêm phong |
| Chair / Designated Signers | Xem biên bản, ký hoặc yêu cầu sửa |
| Authorized User | Tra cứu hồ sơ cuộc họp |

**Luồng:**

1. **Upload audio (UC-44):** hệ thống nhắc Thư ký; Thư ký upload file (MP3 / M4A / WAV) và xác nhận đã có đồng ý ghi âm.
   File không hợp lệ → báo lỗi. Hợp lệ → lưu vào thư viện media (UC-43) và xếp hàng xử lý.
   *Dữ liệu cá nhân theo Nghị định 13/2023/NĐ-CP:* lưu căn cứ đồng ý, mục đích và hạn lưu (`Recording.ConsentObtainedAt`,
   `ConsentPurpose`, `RetainUntil`); xoá theo hạn lưu.
2. **Xử lý giọng nói:** AI chép lời và gán người nói. Lỗi → báo lỗi và cho thử lại.
3. **Rà transcript (UC-45, UC-46):** Thư ký sửa lời và gán người nói chưa xác định.
4. **Tóm tắt (UC-47 → UC-51):** Thư ký sinh / sinh lại tóm tắt, sửa, duyệt, rồi **công bố là bước riêng**; hệ thống báo
   người tham dự.
5. **Biên bản (UC-59, UC-60):** hệ thống tổng hợp bản nháp từ thông tin cuộc họp, điểm danh, tóm tắt đã duyệt, kết quả và
   kết luận biểu quyết (BR-161). Thư ký rà và sửa.
6. **Ký (UC-61 → UC-63, UC-65):** Thư ký gửi ký; hệ thống gửi yêu cầu tới người ký được chỉ định.
   - Biên bản chấp nhận được → ký; hệ thống lưu chữ ký và lịch sử.
   - Không chấp nhận → yêu cầu sửa; Thư ký sửa, hệ thống **huỷ các chữ ký đã có** trên phiên bản cũ và gửi ký lại (BR-166).
7. **Niêm phong (UC-64):** đủ chữ ký → Thư ký hoàn tất; hệ thống khoá và lưu biên bản chính thức. Chỉ Thư ký được niêm
   phong (BR-166).
8. **Tra cứu (UC-58, UC-63, UC-70):** người có quyền xem hồ sơ cuộc họp, lịch sử ký, báo cáo thống kê.

---

# 6. Quan hệ giữa các luồng

```text
BF-01 ──(tài khoản, vai trò)──> BF-02 ──(Published, lời mời)──> BF-03 ──(Ended, kết quả biểu quyết)──> BF-04
                                  ↑ │
                                  └─┘ vòng duyệt: Rejected → sửa → gửi lại
```

- BF-02 có vòng duyệt lặp tới khi `Approved`, và nhánh dời lịch sau khi `Published`.
- BF-03 lặp theo từng mục agenda; biểu quyết là nhánh tuỳ chọn trong vòng lặp.
- BF-04 có hai vòng lặp: xử lý audio lỗi → thử lại; biên bản bị yêu cầu sửa → vòng ký mới.

# 7. Quy tắc khi vẽ / sửa swimlane

- Mỗi ô là một mốc nghiệp vụ hoặc một quyết định; tên ô tiếng Anh, động từ đứng đầu.
- Ô quyết định chỉ có nhánh *Yes / No*.
- Không vẽ Meeting Session, Resolution, quorum, Audio Uploader, uỷ quyền tham dự — đã bỏ khỏi phạm vi.
- Sửa `.drawio` trong [09-bussinessflow/](../09-bussinessflow/README.md), xuất lại `.drawio.png`, cập nhật file này nếu
  luồng đổi.

## Guest — quyết định PO 13/10/2026

Tên/email do Thư ký mời → UC-04 OTP → Guest tự khai tên hiển thị và chức vụ tùy chọn → Member participation trong đúng Meeting. Toàn bộ tài liệu gắn vào Meeting, comments và biểu quyết áp dụng như Member; quyền bổ sung do Thư ký cấp như Member, không giới hạn hai quyền roster/response. Mời Confidential/Secret hiện cảnh báo trong form bình thường, không thêm xác nhận. [ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md). Chưa điểm danh khác Vắng, không chặn kết thúc và không tự chuyển thành Vắng khi sinh biên bản/báo cáo.
