# Luồng nghiệp vụ chính (Main Business Flows)

> Diễn giải **4 Main Business Flow** BF-01 → BF-04 của TrustCabinet, theo sơ đồ rà lại ngày 24/11/2026 cho khớp hệ thống
> đã làm. Sơ đồ swimlane gốc ở [09-bussinessflow/](../09-bussinessflow/README.md); Report 3 §I.2 dùng cùng 4 sơ đồ này.
> **Liên kết:** [Vòng đời cuộc họp](01-vong-doi-cuoc-hop.md) · [ERD](02-erd.md) ·
> [Master UC Inventory](../01-requirements/00-master-use-case-inventory.md) · [Đặc tả Use Case](../01-requirements/05-use-case-specifications.md) ·
> [Business Rules](../01-requirements/06-business-rules.md).
> **Nguyên tắc:** chỉ giữ mốc nghiệp vụ, điểm chuyển giao giữa actor, thay đổi trạng thái và điểm quyết định quan trọng;
> không biến mọi Use Case thành một ô.
> **Mã UC và BR** theo Report 3 sau đợt đánh số lại ngày 24/11/2026 (UC-01 → UC-81, BR-001 → BR-154,
> [ADR-0079](../05-decisions/0079-cho-danh-so-lai-br-uc-co-bang-anh-xa.md)); mã cũ tra ở bảng ánh xạ
> [UC](../01-requirements/10-bang-anh-xa-uc-dot-1.md) và [BR](../01-requirements/09-bang-anh-xa-br-dot-1.md).
> **Tên actor** theo Report 3: *Member* là người có quyền tạo cuộc họp (trước đây gọi là Meeting Organizer), *Approver*
> là người duyệt. Đừng nhầm với vai trò *Member* (Thành viên) trong một cuộc họp.

---

# 1. Tổng quan

```text
BF-01 System Access & Administration
   → người dùng có tài khoản, vai trò, quyền phù hợp
BF-02 Meeting Preparation, Approval & Participant Coordination
   → (tìm giờ họp chung) → Draft → WaitingApproval → Approved | Rejected → Published (lời mời đã gửi)
   → hoặc Cancelled trước khi họp
BF-03 Meeting Execution & Decision Making
   → Published → InProgress → Ended (agenda, biểu quyết, kết luận, bản ghi đã lưu)
BF-04 Post-Meeting Processing & Formal Records
   → Bản ghi → Bản gỡ băng → Dự thảo AI (tóm tắt) → Biên bản theo mẫu → Duyệt và ban hành
```

Các quyết định định hình các luồng này:

1. **Không có Meeting Session.** Một cuộc họp là một buổi họp; lịch, agenda, biểu quyết, ghi âm, biên bản gắn thẳng vào
   cuộc họp ([ADR-0013](../05-decisions/0013-mot-cuoc-hop-la-mot-buoi-hop.md)).
2. **Biểu quyết chỉ trong cuộc họp.** Không vote trước / sau họp, không quorum, không đối tượng Resolution. Chair ghi kết
   luận *Passed / NotPassed / Deferred* sau khi đóng phiếu ([ADR-0014](../05-decisions/0014-bieu-quyet-chi-trong-cuoc-hop.md)).
   Biểu quyết là một nhánh trong vòng agenda của BF-03.
3. **Ghi âm diễn ra trong cuộc họp.** Thư ký bật mic phòng (họp trực tiếp, kết hợp); họp trực tuyến ghi theo micro từng
   người. Tải tệp audio sau họp chỉ còn cho họp trực tiếp (BR-107, BR-108). Không có vai trò Audio Uploader.
4. **Biên bản theo một mẫu cố định, AI chỉ viết dự thảo.** AI viết dự thảo ba mục (tóm tắt); biên bản do hệ thống điền
   vào Mẫu 1.4 từ hồ sơ cuộc họp và dự thảo ([ADR-0054](../05-decisions/0054-ai-du-thao-bien-ban-chay-tai-cho.md), BR-130).
   Không ký số: Chủ toạ duyệt là ban hành (PO 14/11/2026, SCRUM-37 đóng).

---

# 2. BF-01 – System Access & Administration

**Trigger:** một nhân sự nội bộ cần được cấp quyền truy cập, hoặc cần đổi quyền đang có.
**Kết thúc:** người dùng có tài khoản hợp lệ với vai trò và quyền phù hợp, truy cập đúng các chức năng được phép.

| Swimlane | Việc chính |
|---|---|
| System Admin | Tạo / quản lý vai trò tuỳ chỉnh (UC-11), cấu hình quyền (UC-10), tạo / cập nhật tài khoản (UC-06), gán vai trò (UC-08) |
| User | Mở TrustCabinet, chọn đăng nhập Google hoặc email + mật khẩu, dùng các chức năng được phép |
| TrustCabinet | Lưu thay đổi và ghi audit log (BR-009); kiểm tra thông tin đăng nhập, liên kết tài khoản, trạng thái active; quá số lần cho phép trong một phút thì báo lỗi MSG414 |
| OAuth Provider | Xác minh tài khoản Google và trả danh tính (UC-02) |

**Luồng:**

1. System Admin chuẩn bị vai trò, quyền và tài khoản; mỗi thay đổi được lưu và ghi audit log (BR-007, BR-009).
2. Người dùng mở TrustCabinet và chọn cách đăng nhập.
   - **Email + mật khẩu (UC-01):** sai thông tin → báo lỗi (MSG01, không lộ email có tồn tại hay không — BR-004); sai quá
     số lần cho phép trong một phút → hệ thống báo lỗi MSG414, thử lại sau ít phút. Không khoá tài khoản.
   - **Google (UC-02):** Google xác minh tài khoản; hệ thống chỉ cho vào khi email khớp một tài khoản nội bộ, không tự tạo
     tài khoản (BR-003).
3. Tài khoản bị vô hiệu hoá → báo lỗi (BR-001). Hợp lệ → đăng nhập và nạp quyền theo vai trò; đổi quyền có hiệu lực từ thao
   tác kế tiếp, không phải đăng nhập lại (BR-002, BR-008).
4. Đặt lại mật khẩu (UC-07): Admin gửi link dùng một lần, có hạn. Vô hiệu hoá tài khoản (UC-06c) chấm dứt mọi phiên đăng
   nhập đang có (BR-001).

---

# 3. BF-02 – Meeting Preparation, Approval & Participant Coordination

**Trigger:** một Member cần tổ chức một cuộc họp mới.
**Kết thúc:** cuộc họp `Published`, lời mời đã gửi, người được mời phản hồi được, cuộc họp sẵn sàng diễn ra; hoặc cuộc họp
bị huỷ trước khi diễn ra.

| Swimlane | Việc chính |
|---|---|
| Member (Meeting Creator) | Tìm giờ họp chung hoặc tạo cuộc họp, chọn người duyệt, gửi duyệt, sửa khi bị từ chối, công bố, dời lịch, huỷ cuộc họp |
| Chair / Secretary | Quản lý thành phần và mời Guest, gán vai trò, chuẩn bị agenda, thêm tài liệu, bổ sung vai trò còn thiếu khi công bố |
| TrustCabinet | Tính khung giờ rảnh; lưu nháp; chuyển trạng thái và báo người liên quan; kiểm tra đủ Chair và Secretary; tạo lời mời; lưu phản hồi; lưu lịch mới; huỷ cuộc họp |
| Approver | Xem xét và duyệt / từ chối |
| Participant / Guest | Mở lời mời (Guest xác minh email), chấp nhận / từ chối |
| Resend Email | Gửi thư mời, thư đổi lịch, thư huỷ |

**Luồng:**

1. **Tìm giờ họp chung (UC-81)** — nhánh tuỳ chọn ở đầu luồng. Member nhập người mời (tối đa 10 người), thời lượng, khoảng
   ngày và giờ làm việc. Hệ thống chỉ đọc lịch những người đã chia sẻ với Member, ở đúng mức chia sẻ (BR-140 → BR-147):
   - có khung hợp với tất cả → hiện các khung chung còn trống;
   - không có → hiện các khung nhiều người rảnh nhất, kèm ai bận (MSG445).

   Member chọn một khung và xác nhận; hệ thống tạo cuộc họp `Draft` với người mời đầu tiên làm Chair, những người còn lại
   là Member, người tìm là Secretary. Khung chạm cuộc họp Mật vẫn tạo được (BR-148).
2. **Tạo cuộc họp (UC-13)** — khi không tìm giờ. Member nhập tiêu đề, lịch (giờ bắt đầu ở tương lai, giờ kết thúc sau giờ
   bắt đầu), hình thức, địa điểm, link tuỳ chọn, độ mật, và chọn thành phần với **đúng một Chair**, Secretary tuỳ chọn
   (BR-013, BR-038 → BR-040). Hệ thống lưu `Draft`. Không còn bước gán Thư ký đầu tiên riêng.
3. **Chair / Secretary chuẩn bị:** thêm thành viên nội bộ và mời Guest theo tên + email (UC-30; BR-036, BR-037,
   BR-049 → BR-051), gán hoặc đổi vai trò Chair / Secretary / Member (Secretary đổi Chair trước khi công bố — BR-041),
   soạn agenda (UC-23; BR-058, BR-059), thêm tài liệu (UC-36; BR-069 → BR-072). Nháp biểu quyết và tài liệu của nó chuẩn bị
   được từ `Approved` (UC-63, UC-68; BR-086, BR-094).
4. **Gửi duyệt (UC-17):** Member chọn người duyệt và gửi; cuộc họp chuyển `WaitingApproval`, Approver được báo. Thông tin
   chung bị khoá, danh sách thành phần chỉ đọc (BR-018, BR-019, BR-037).
5. **Duyệt (UC-18):** Approver được phân công xem cuộc họp (BR-020).
   - **Từ chối:** bắt buộc lý do → `Rejected`, **chỉ người tạo cuộc họp** được báo. Member sửa (UC-14) và gửi lại từ bước 4.
   - **Duyệt:** → `Approved`, **chỉ người tạo cuộc họp** được báo.
6. **Công bố (UC-19):** chỉ người tạo cuộc họp công bố; hệ thống kiểm tra đúng một Chair và ít nhất một Secretary đang
   hoạt động (BR-021). Thiếu → Chair / Secretary bổ sung vai trò rồi công bố lại. Đủ → `Published`, tạo lời mời cho thành
   viên và Guest; email gửi bất đồng bộ, lỗi gửi không làm mất trạng thái `Published` (BR-031, BR-032).
7. **Phản hồi lời mời (UC-31):** người được mời chọn *Accepted* / *Declined* **một lần**, không đổi được; Chair không được
   hỏi. Từ chối chỉ ẩn cuộc họp khỏi lịch của người đó (BR-046 → BR-048). Guest mở link, xác minh email bằng mã một lần
   (UC-04; BR-052 → BR-054) rồi phản hồi.
8. **Dời lịch sau công bố (UC-14 — *Reschedule*):** Member chỉ đổi giờ, địa điểm, link; giờ bắt đầu mới phải ở tương lai
   (BR-015). Hệ thống lưu lịch mới và báo lại người được mời; lỗi gửi không huỷ thay đổi (BR-031, BR-032). Dời lịch không
   mở lại câu trả lời lời mời.
9. **Huỷ cuộc họp (UC-22):** người tạo cuộc họp huỷ bất cứ lúc nào trước khi bắt đầu (`Draft` → `Published`), bắt buộc
   lý do; cuộc họp thành `Cancelled`, chỉ đọc (BR-028). Đã `Published` thì người được mời được báo trong ứng dụng và qua
   email huỷ (kèm tệp lịch huỷ); đang `WaitingApproval` thì báo Approver; trạng thái khác không báo ai. Cuộc họp chưa bắt
   đầu 24 giờ sau giờ bắt đầu dự kiến thì hệ thống tự huỷ (BR-029). Sơ đồ chỉ vẽ ca đã `Published`.

---

# 4. BF-03 – Meeting Execution & Decision Making

**Trigger:** Chair quyết định bắt đầu một cuộc họp `Published`.
**Kết thúc:** cuộc họp `Ended`; kết quả agenda, kết quả biểu quyết, kết luận và bản ghi đã được lưu.

| Swimlane | Việc chính |
|---|---|
| Chair | Bắt đầu họp, bắt đầu một mục agenda bất kỳ đang chờ, mở – đóng biểu quyết, ghi kết luận biểu quyết và kết luận của mục, hoàn thành mục, giao chủ toạ, kết thúc họp |
| Participant / Guest | Vào phòng họp trực tuyến (Online / Hybrid), thảo luận (tài liệu, chat, chia sẻ màn hình, giơ tay phát biểu), xác nhận đã đọc tài liệu biểu quyết, bỏ phiếu |
| Secretary | Ghi điểm danh chính thức, bật ghi âm phòng, chuẩn bị nháp biểu quyết, ghi kết luận của mục |
| TrustCabinet | Chuyển `InProgress` và báo người tham dự; ghi tiếng nói và chép lời; kiểm phiếu; lưu trạng thái và thời gian thực tế của mục agenda; chuyển `Ended`, đóng phòng họp và lưu bản ghi |

**Luồng:**

1. **Bắt đầu (UC-20):** Chair hoặc Secretary bắt đầu; hệ thống kiểm tra đủ Chair và Secretary đang hoạt động (BR-024),
   chuyển `InProgress`, ghi giờ bắt đầu thực tế, báo người được mời trừ người bấm (BR-035).
2. **Vào phòng họp (UC-43 → UC-48)** nếu cuộc họp Online / Hybrid. Guest vào qua phiên Guest của UC-04. Vào hay rời phòng
   không đổi cuộc họp, điểm danh hay biểu quyết (BR-100, BR-101).
3. **Điểm danh (UC-33):** trong lúc `InProgress`, Thư ký lưu *Present / Absent* cho một hoặc nhiều người, cả thành viên nội
   bộ và Guest; người chưa điểm danh để trống, không tính là vắng (BR-056, BR-057).
4. **Ghi âm (UC-52):** Thư ký bật mic phòng sau khi xác nhận đã báo mọi người (họp trực tiếp, kết hợp); họp trực tuyến ghi
   theo micro từng người khi micro bật. Hệ thống lưu từng lượt ghi và chép lời (BR-107 → BR-109, BR-113).
5. **Vòng agenda** — lặp cho từng mục:
   1. Chair bắt đầu một mục `Pending` **bất kỳ** (UC-24): mục → `InProgress`, ghi giờ bắt đầu; tối đa một mục đang chạy
      (BR-060, BR-061).
   2. Thảo luận với tài liệu, chat, chia sẻ màn hình; người dự giơ tay, Chair mời phát biểu theo lượt (UC-50; BR-104,
      BR-105).
   3. **Cần biểu quyết?** Có → Chair mở biểu quyết từ nháp đã chuẩn bị (UC-64; danh sách người bỏ phiếu và tài liệu chốt
      khi mở — BR-090, BR-091, BR-094) → người được bỏ phiếu xác nhận đã đọc từng tài liệu (UC-42, BR-095) rồi bỏ phiếu
      (UC-65; BR-092, BR-093) → Chair đóng, hệ thống kiểm phiếu và hiện kết quả (UC-66, UC-62; BR-098) → Chair ghi kết luận
      *Passed / NotPassed / Deferred* (UC-67, BR-099).
   4. Chair hoặc Secretary ghi kết luận của mục (UC-27; BR-066, BR-067); sửa được tới khi dựng nháp biên bản.
   5. Chair hoàn thành mục (UC-25): mục → `Done`, ghi giờ kết thúc (BR-062).
   6. Còn mục → quay lại 5.1.
   - Từ khi công bố, Chair có thể giao chủ toạ cho một thành phần nội bộ khác (UC-34); Chair cũ thành Member (BR-042).
     Không vẽ trong sơ đồ.
6. **Kết thúc (UC-21):** Chair kết thúc bất cứ lúc nào khi `InProgress`, không kiểm tra biểu quyết đang mở hay điểm danh
   (BR-025). Biểu quyết giữ trạng thái và phiếu đã lưu, cuộc họp `Ended` ngừng nhận phiếu (BR-026); nháp biểu quyết chưa
   mở bị xoá (BR-087). Mục agenda đang chạy được hoàn tất, các mục còn chờ giữ nguyên. Hệ thống đóng phòng họp, tắt mic
   phòng và lưu bản ghi. Cuộc họp còn `InProgress` 24 giờ sau giờ kết thúc dự kiến thì tự kết thúc (BR-027).

---

# 5. BF-04 – Post-Meeting Processing & Formal Records

**Trigger:** cuộc họp đã `Ended` và cần xử lý hồ sơ sau họp.
**Kết thúc:** biên bản đã được Chủ toạ duyệt và ban hành; hồ sơ cuộc họp tra cứu được theo quyền.

| Swimlane | Việc chính |
|---|---|
| AI Drafting Service | Viết dự thảo ba mục (tóm tắt) từ bản gỡ băng, agenda và kết quả biểu quyết |
| TrustCabinet | Kiểm tra tệp audio; lưu và xếp hàng gỡ băng; gỡ băng (tách người nói, gợi ý Voice ID); điền mẫu biên bản cố định; khoá biên bản đã ban hành, ghi nhật ký kiểm toán, báo người được đọc |
| Secretary | Tải tệp audio (họp trực tiếp), rà bản gỡ băng và gán người nói, yêu cầu sinh tóm tắt, rà – sửa – duyệt tóm tắt, sinh và sửa biên bản, gửi duyệt |
| Chair | Duyệt và ban hành biên bản, hoặc trả lại để sửa |
| Authorized User | Tra cứu hồ sơ cuộc họp |

**Luồng:**

1. **Tải tệp audio (UC-52)** — nhánh tuỳ chọn, chỉ cho họp trực tiếp; lượt ghi mic phòng và câu nói theo micro đã lưu ở
   BF-03. Thư ký tải tệp lên; tệp không hợp lệ → báo lỗi, tải lại; hợp lệ → lưu và xếp hàng gỡ băng (BR-107, BR-108).
   *Dữ liệu cá nhân theo Luật 91/2025/QH15 và Nghị định 356/2025/NĐ-CP:* bản ghi lưu kèm thời điểm xác nhận đã báo người dự
   và mục đích (BR-109); bản ghi, bản gỡ băng và câu nói tự xoá sau 180 ngày (BR-110). Âm thanh và mẫu giọng chỉ xử lý
   trong hệ thống (BR-121).
2. **Gỡ băng:** hệ thống chép lời, tách người nói và gợi ý tên theo Voice ID cho người đã đồng ý (BR-120, BR-123, BR-124).
   Lỗi → báo lỗi, Thư ký chạy lại; chạy lại giữ phần đã sửa tay (BR-125, BR-154).
3. **Rà bản gỡ băng (UC-54, UC-55):** Thư ký sửa chữ, gán người nói; Chủ toạ hoặc Thư ký loại câu khỏi hồ sơ khi cần
   (BR-114, BR-115).
4. **Tóm tắt (UC-58 → UC-61):** Chủ toạ hoặc Thư ký yêu cầu sinh dự thảo; AI Drafting Service viết ba mục I–III, kết quả
   biểu quyết chép từ dữ liệu (BR-126, BR-128). AI chạy trên máy của nhóm, cuộc họp Thường có thể dùng API ngoài
   (BR-129). Chủ toạ hoặc Thư ký rà, sửa và **duyệt tóm tắt**; người khác chỉ đọc sau khi duyệt (BR-127).
5. **Biên bản (UC-70, UC-71):** Chủ toạ hoặc Thư ký dựng nháp; hệ thống **điền mẫu cố định** từ hồ sơ cuộc họp và dự thảo
   hiện có, không có dự thảo thì để "……" cho Thư ký viết (BR-130). Thư ký rà và sửa; chỉ Chủ toạ và Thư ký đọc nháp
   (BR-131).
6. **Duyệt và ban hành (UC-72 → UC-74):** Thư ký gửi duyệt (BR-135). Chủ toạ duyệt là ban hành luôn: biên bản bị khoá,
   ghi nhật ký kiểm toán, người được đọc được báo trong ứng dụng và qua email (BR-010, BR-136, BR-137). Không duyệt thì trả
   lại kèm lý do cho Thư ký sửa rồi gửi lại.
7. **Đính chính (UC-75):** sửa sau ban hành thì tạo bản đính chính mới từ bản đã ban hành, đi lại bước 5 → 6; bản cũ thành
   `Superseded` (BR-138). Không vẽ trong sơ đồ.
8. **Tra cứu (UC-69, UC-79):** người đọc được cuộc họp (trừ Guest) xem và tải biên bản đã ban hành, trừ cuộc họp Mật không
   tải được (BR-132, BR-139); xem báo cáo thống kê.

---

# 6. Quan hệ giữa các luồng

```text
BF-01 ──(tài khoản, vai trò)──> BF-02 ──(Published, lời mời)──> BF-03 ──(Ended, biểu quyết, bản ghi)──> BF-04
                                  ↑ │  └──> Cancelled (huỷ trước khi họp)
                                  └─┘ vòng duyệt: Rejected → sửa → gửi lại
```

- BF-02 có nhánh tìm giờ họp chung ở đầu, vòng duyệt lặp tới khi `Approved`, nhánh dời lịch và nhánh huỷ sau khi
  `Published`.
- BF-03 lặp theo từng mục agenda; biểu quyết là nhánh tuỳ chọn trong vòng lặp; bản ghi tạo trong lúc họp.
- BF-04 có ba vòng lặp: tệp audio không hợp lệ → tải lại; gỡ băng lỗi → chạy lại; Chủ toạ trả biên bản → Thư ký sửa →
  gửi duyệt lại.

# 7. Quy tắc khi vẽ / sửa swimlane

- Mỗi ô là một mốc nghiệp vụ hoặc một quyết định; tên ô tiếng Anh, động từ đứng đầu.
- Ô quyết định chỉ có nhánh *Yes / No*.
- Không vẽ Meeting Session, Resolution, quorum, Audio Uploader, uỷ quyền tham dự — đã bỏ khỏi phạm vi.
- Sửa `.drawio` trong [09-bussinessflow/](../09-bussinessflow/README.md), xuất lại `.drawio.png`, cập nhật file này nếu
  luồng đổi.

## Guest — quyết định PO 13/10/2026

Tên/email do Thư ký mời → UC-04 xác minh bằng mã một lần → Guest tự khai tên hiển thị và chức vụ tuỳ chọn → tham gia như
Member trong đúng cuộc họp đó (BR-049 → BR-055). Toàn bộ tài liệu gắn vào cuộc họp, bình luận và biểu quyết áp dụng như
Member. Mời vào cuộc họp Mật hiện cảnh báo trong form bình thường, không thêm xác nhận
([ADR-0016](../05-decisions/0016-khach-tham-gia-nhu-thanh-vien.md)). Chưa điểm danh khác Vắng, không chặn kết thúc và không
tự chuyển thành Vắng khi sinh biên bản hay báo cáo (BR-057).
