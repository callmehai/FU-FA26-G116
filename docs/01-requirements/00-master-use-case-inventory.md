# TrustCabinet — Danh mục Use Case tổng thể & Baseline vẽ Use Case Diagram

> **Baseline:** 05/10/2026 — Bản đã review sau khi đối soát Report 1, Report 2, Report 3 và các review vòng 1–2  
> **Mục đích:** Làm nguồn chuẩn để lập kế hoạch Use Case Diagram, giữ số UC, kiểm soát phạm vi Feature, ánh xạ Actor và quan hệ `<<extend>>` / `<<include>>`.  
> **Liên kết liên quan:** [Đặc tả Use Case](05-use-case-specifications.md) · [Danh mục Business Rules](06-business-rules.md) · [Luồng nghiệp vụ chính](../02-design/04-luong-nghiep-vu-chinh.md) · [Mô hình dữ liệu (ERD)](../02-design/02-erd.md).  
> **Nguồn phạm vi:** Report 1 — Project Introduction, FE-01 → FE-16.  
> **Nguồn chi tiết UC đã có:** Report 3 — SRS.  
> **Nguồn kế hoạch:** Report 2 — Project Management Plan.  

## Các quyết định PO mới nhất đã áp dụng

- Bỏ hoàn toàn đề xuất **Assign Audio Uploader**.
- **Secretary là actor duy nhất thực hiện UC-62 Upload Meeting Audio**.
- Mỗi internal user có **Kho tài liệu cá nhân / Personal Document Library**.
- Tài liệu có thể được đưa vào cuộc họp theo 2 cách:
  1. upload trực tiếp từ máy tính;
  2. chọn tài liệu đã có trong kho cá nhân.
- UC-89 được dùng lại cho **View Personal Document Library** vì đề xuất cũ `Assign Audio Uploader` chưa từng được promote vào Report 3.
- Thêm UC-94 **Upload Document to Personal Library**.

> **Quy ước trạng thái**
>
> - **ĐÃ ĐẶC TẢ**: UC đã tồn tại trong Report 3.
> - **ĐÃ LÊN KẾ HOẠCH**: UC được suy ra trực tiếp từ scope Report 1 nhưng chưa có full UC Specification trong Report 3.
> - **BỔ SUNG SAU REVIEW / PO ĐÃ XÁC NHẬN**: UC được thêm để đóng gap liên tài liệu và đã được PO đồng ý đưa vào scope.
> - UC-01 → UC-54 được **đóng băng ID**. Không renumber các UC này.
> - Các UC mới tiếp tục từ UC-55 trở đi; không renumber chỉ để gom UC của cùng Feature cho liền số.

---

# 0. Các quyết định review đã chốt

| Vấn đề | Quyết định dùng trong master |
|---|---|
| UC-37 / UC-38 lệch actor | Giữ logic chi tiết của Report 3: UC-37 chỉ dành cho Secretary. UC-38 có 2 đường vào: Secretary quản lý roster thì extend UC-37; Organizer thêm Secretary đầu tiên thì đi từ context UC-17. |
| Guest không đi qua được UC-17 | Thêm **UC-87 Truy cập cuộc họp qua lời mời Guest**. Guest dùng UC-87 làm luồng access/precondition thay vì coi Guest là internal user. |
| UC-25 mở target | Trước mắt UC-25 chỉ mở **meeting được liên kết**. Vote/Minutes chỉ mở rộng sau khi FE-12/FE-13 được đặc tả trong Report 3. |
| Actor cũ trong UC-01/02/03/06 | Master chỉ dùng actor canonical. Report 3 phải cleanup các actor legacy. |
| AI trong Context Diagram | Chuẩn là **audio → transcript có speaker → draft summary**. AI không trực tiếp tạo minutes từ secretary notes / meeting documents. |
| Thiếu attendance | **UC-88 Ghi nhận điểm danh — PO đã xác nhận.** Secretary ghi attendance chính thức; dữ liệu room presence chỉ có thể pre-fill. |
| Người upload audio | Không có UC phân công Audio Uploader. **Secretary trực tiếp upload audio bằng UC-62.** |
| Thiếu thao tác generate summary | **UC-90 Generate / Regenerate Meeting Summary — PO đã xác nhận.** Secretary được sửa speaker label trước rồi mới generate/regenerate. |
| Thiếu document comment/review | **UC-91 Comment on Meeting Document — PO đã xác nhận.** Chỉ cover collaborative comment; chưa tự sinh workflow approve/reject tài liệu. |
| Kho tài liệu cá nhân | **PO đã xác nhận.** Internal user có kho tài liệu riêng. UC-89 = View Personal Document Library; UC-94 = Upload Document to Personal Library. |
| Audit log chưa user-visible | **UC-92 View Audit Log — PO đã xác nhận.** System Admin xem/search/filter audit event. |
| Signer yêu cầu sửa minutes | **UC-93 Request Revision of Meeting Minutes — PO đã xác nhận.** Đây là UC riêng, không phải Alternative Flow của UC-81 vì không đạt mục tiêu "ký". |
| Approve / Publish Summary | Giữ UC-68 Approve và UC-69 Publish riêng. UC-69 là thao tác chủ động của Secretary, không tự publish sau approve. |
| Seal Minutes | UC-83 do Secretary chủ động Finalize / Seal. |
| Meeting lifecycle history / calendar | Không tạo UC mới; coi là view/Alternative Flow của UC-17 và UC-18. |
| ReturnToDraft / TransferOrganizer chỉ có trong code | Chưa promote thành UC vì chưa có trong Report 1. Cần PO/BA phê duyệt scope hoặc dọn code. |
| Priority | FE-01 → FE-14 = **Must-have**; FE-15 → FE-16 = **Should-have**. |

**Baseline kế hoạch hiện tại: 94 UC.**

---

# 1. Quy ước quan hệ UML dùng cho đồ án

## 1.1 `<<extend>>`

Dùng `<<extend>>` khi:

- base UC tự nó đã hoàn chỉnh;
- hành động mở rộng là tùy chọn / có điều kiện;
- hành động mở rộng vẫn là một user goal riêng.

Quy ước của dự án:

- UC kiểu View/List thường làm base.
- CRUD hoặc business action tùy chọn từ context đó có thể extend View UC.
- Mũi tên `<<extend>>` đi **từ extending UC → base UC**.

Ví dụ:

```text
UC-17 Xem chi tiết cuộc họp --<<extend>>--> UC-18 Xem danh sách cuộc họp
UC-16 Cập nhật cuộc họp     --<<extend>>--> UC-17 Xem chi tiết cuộc họp
```

## 1.2 `<<include>>`

Chỉ dùng `<<include>>` khi một UC **bắt buộc phải thực thi một behavior dùng chung** mỗi lần chạy.

Không dùng `include` chỉ vì:

- một UC xảy ra trước UC khác;
- UC trước tạo dữ liệu cho UC sau;
- UC kia chỉ là precondition;
- cần đăng nhập;
- hai UC cùng màn hình;
- hai UC là hai bước trong lifecycle.

### Ghi chú Guest

UC-87 là behavior xác minh/truy cập dành cho Guest.

Nhưng UC-41 và UC-45 có cả internal actor lẫn Guest. Vì vậy trên diagram tổng **không** vẽ vô điều kiện:

```text
UC-41 --<<include>>--> UC-87
```

vì sẽ khiến Participant nội bộ cũng phải chạy Guest OTP.

Trên diagram tổng nên hiểu:

> Nếu actor là Guest, UC-41 / UC-45 / tài liệu được cho phép chỉ chạy khi đã có guest session hợp lệ do UC-87 tạo.

Nếu vẽ **Guest-only diagram**, có thể ghi rõ constraint:

```text
UC-41 [Guest path] --<<include>>--> UC-87
UC-45 [Guest path] --<<include>>--> UC-87
```

## 1.3 Lifecycle không phải `include/extend`

Các chuỗi sau là state/business dependency:

```text
Tạo Meeting → Gửi duyệt → Duyệt → Publish → Start → End
Tạo Session → Start Session → End Session
Tạo Agenda Item → Set Current → Complete
Mở Vote → Cast Vote → Close Vote → Resolution
Chuẩn bị Minutes → Ký → Seal
```

Các chuỗi này nên thể hiện bằng State / Activity / Sequence Diagram.

---

# 2. Mô hình Actor chuẩn

## 2.1 Actor nghiệp vụ

1. **System Admin — Quản trị viên hệ thống**
2. **Meeting Organizer — Người tổ chức cuộc họp**
3. **Meeting Approver — Người phê duyệt cuộc họp**
4. **Chair — Chủ trì**
5. **Secretary — Thư ký**
6. **Participant — Người tham gia**
7. **Guest — Khách mời bên ngoài**

### Ý nghĩa

- **System Admin**, **Meeting Organizer**, **Meeting Approver** là trách nhiệm/capability ở cấp hệ thống.
- **Chair**, **Secretary**, **Participant** là role gắn theo từng meeting.
- **Guest** không có internal account, chỉ truy cập đúng meeting được mời thông qua guest session đã xác minh.
- Nếu code còn enum `Member`, tài liệu nghiệp vụ nên ghi rõ:

```text
MeetingRole.Member = Participant
```

Không tạo actor thứ 8 chỉ vì tên enum khác.

## 2.2 Actor hệ thống ngoài

- **Google Identity Provider**
- **Email / SMTP Service**
- **AI / Speech Processing Provider**

Không coi repository, database, background job, LiveKit, SignalR, middleware… là Use Case actor.

---

# 3. Quy ước ưu tiên

| Feature | Priority |
|---|---|
| FE-01 → FE-14 | **Must-have** |
| FE-15 → FE-16 | **Should-have** |

UC kế thừa priority của Feature trừ khi có change request được duyệt.

---

# 4. Danh mục Use Case tổng thể

## FE-01 — Xác thực & Bảo mật tài khoản  
**Authentication & Account Security**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor chính | Quan hệ diagram |
|---|---|---|---|---|
| UC-01 | Đăng nhập bằng Email và Mật khẩu *(Sign in with Email and Password)* | ĐÃ ĐẶC TẢ | Các internal actor | Độc lập |
| UC-02 | Đăng nhập bằng Google *(Sign in with Google)* | ĐÃ ĐẶC TẢ | Các internal actor | Độc lập; Google Identity Provider là secondary actor |
| UC-03 | Đăng xuất *(Sign Out)* | ĐÃ ĐẶC TẢ | Internal actor đã đăng nhập | Độc lập |
| **UC-87** | **Truy cập cuộc họp qua lời mời Guest *(Access Meeting via Guest Invitation)*** | **BỔ SUNG SAU REVIEW / PO ĐÃ XÁC NHẬN** | Guest | Độc lập |

### UC-87

Guest:

1. mở secure invitation của đúng meeting;
2. xác minh quyền sở hữu email theo cơ chế OTP/one-time verification;
3. nhận guest session giới hạn theo meeting;
4. chỉ dùng các chức năng được phép trong meeting đó;
5. không tạo internal account và không nhận system role.

### Account lockout

Automatic lockout vẫn là Business Rule, không phải UC riêng.

Report 3 phải chốt:

- số lần sai;
- thời gian lock;
- có auto-unlock không;
- EX cho UC-01 / UC-02 khi account Locked;
- quan hệ với UC-55 Unlock User Account.

---

## FE-02 — Quản lý Người dùng & Hồ sơ  
**User & Profile Management**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-04 | Xem danh sách tài khoản người dùng *(View User Accounts)* | ĐÃ ĐẶC TẢ | System Admin | Base |
| UC-05 | Tạo tài khoản người dùng *(Create User Account)* | ĐÃ ĐẶC TẢ | System Admin | `<<extend>>` UC-04 |
| UC-06 | Cập nhật tài khoản người dùng *(Update User Account)* | ĐÃ ĐẶC TẢ | System Admin | `<<extend>>` UC-04 |
| UC-07 | Vô hiệu hóa / Kích hoạt lại tài khoản *(Deactivate / Reactivate User Account)* | ĐÃ ĐẶC TẢ | System Admin | `<<extend>>` UC-04 |
| UC-08 | Đặt lại mật khẩu *(Reset User Password)* | ĐÃ ĐẶC TẢ | System Admin | `<<extend>>` UC-04 |
| UC-09 | Gán system role cho người dùng *(Assign System Role to User)* | ĐÃ ĐẶC TẢ | System Admin | `<<extend>>` UC-04 |
| UC-55 | Mở khóa tài khoản *(Unlock User Account)* | ĐÃ LÊN KẾ HOẠCH — cần promote | System Admin | `<<extend>>` UC-04 |

```text
UC-05 ─┐
UC-06 ─┤
UC-07 ─┤
UC-08 ─┼─ <<extend>> → UC-04
UC-09 ─┤
UC-55 ─┘
```

**Lưu ý:** UC-07 không được dịch thành “Khóa/Mở khóa”.  
`Deactivate/Reactivate` khác với trạng thái `Locked` do failed login.

---

## FE-03 — Kiểm soát truy cập theo vai trò  
**Role-Based Access Control**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-10 | Xem ma trận Role & Permission *(View Role & Permission Matrix)* | ĐÃ ĐẶC TẢ | System Admin | Base |
| UC-11 | Cấu hình quyền của Role *(Configure Role Permissions)* | ĐÃ ĐẶC TẢ | System Admin | `<<extend>>` UC-10 |
| UC-12 | Tạo Role *(Create Role)* | ĐÃ ĐẶC TẢ | System Admin | `<<extend>>` UC-10 |
| UC-13 | Cập nhật Role *(Update Role)* | ĐÃ ĐẶC TẢ | System Admin | `<<extend>>` UC-10 |
| UC-14 | Xóa Role *(Delete Role)* | ĐÃ ĐẶC TẢ | System Admin | `<<extend>>` UC-10 |
| UC-92 | Xem Audit Log *(View Audit Log)* | PO ĐÃ XÁC NHẬN | System Admin | Độc lập |

UC-92 chỉ hiển thị audit event mang ý nghĩa nghiệp vụ/bảo mật; không biến technical debug log thành business data.

---

## FE-04 — Quản lý cuộc họp  
**Meeting Management**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-15 | Tạo cuộc họp *(Create Meeting)* | ĐÃ ĐẶC TẢ | Meeting Organizer | `<<extend>>` UC-18 theo convention UI của dự án |
| UC-16 | Cập nhật cuộc họp *(Update Meeting)* | ĐÃ ĐẶC TẢ | Meeting Organizer | `<<extend>>` UC-17 |
| UC-17 | Xem chi tiết cuộc họp *(View Meeting Details)* | ĐÃ ĐẶC TẢ | Actor có quyền xem meeting | `<<extend>>` UC-18 |
| UC-18 | Xem danh sách cuộc họp *(View Meeting List)* | ĐÃ ĐẶC TẢ | Actor có quyền | Base |
| UC-19 | Gửi cuộc họp để phê duyệt *(Submit Meeting for Approval)* | ĐÃ ĐẶC TẢ | Meeting Organizer | `<<extend>>` UC-17 |
| UC-20 | Duyệt / Từ chối cuộc họp *(Review Meeting Approval)* | ĐÃ ĐẶC TẢ | Meeting Approver | UC nghiệp vụ độc lập |
| UC-21 | Công bố cuộc họp *(Publish Meeting)* | ĐÃ ĐẶC TẢ | Meeting Organizer | `<<extend>>` UC-17 |
| UC-43 | Bắt đầu cuộc họp *(Start Meeting)* | ĐÃ ĐẶC TẢ | Chair | `<<extend>>` UC-17 |
| UC-44 | Kết thúc cuộc họp *(End Meeting)* | ĐÃ ĐẶC TẢ | Chair | `<<extend>>` UC-17 |

```text
UC-15 ───────────────┐
UC-17 ───────────────┼─ <<extend>> → UC-18

UC-16 ───────────────┐
UC-19 ───────────────┤
UC-21 ───────────────┼─ <<extend>> → UC-17
UC-43 ───────────────┤
UC-44 ───────────────┘
```

### Không tạo UC mới cho

- View Meeting Lifecycle History → AF/view của UC-17.
- Calendar View → AF/view của UC-18.

### Chưa promote từ code

- ReturnToDraft.
- TransferOrganizer.

Nếu muốn giữ phải có requirement/change request chính thức.

---

## FE-05 — Quản lý phiên họp  
**Meeting Session Management**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-26 | Tạo phiên họp *(Create Meeting Session)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-53 |
| UC-27 | Cập nhật phiên họp *(Update Meeting Session)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-53 |
| UC-28 | Hủy phiên họp *(Cancel Meeting Session)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-53 |
| UC-29 | Bắt đầu phiên họp *(Start Meeting Session)* | ĐÃ ĐẶC TẢ | Chair | `<<extend>>` UC-53 |
| UC-30 | Kết thúc phiên họp *(End Meeting Session)* | ĐÃ ĐẶC TẢ | Chair | `<<extend>>` UC-53 |
| UC-53 | Xem các phiên họp *(View Meeting Sessions)* | ĐÃ ĐẶC TẢ | Actor có quyền xem meeting | `<<extend>>` UC-17 |

```text
UC-53 --<<extend>>--> UC-17

UC-26 ─┐
UC-27 ─┤
UC-28 ─┼─ <<extend>> → UC-53
UC-29 ─┤
UC-30 ─┘
```

---

## FE-06 — Quản lý chương trình nghị sự  
**Agenda Management**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-31 | Tạo Agenda Item *(Create Agenda Item)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-54 |
| UC-32 | Cập nhật Agenda Item *(Update Agenda Item)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-54 |
| UC-33 | Xóa Agenda Item *(Delete Agenda Item)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-54 |
| UC-34 | Sắp xếp lại Agenda Item *(Reorder Agenda Items)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-54 |
| UC-35 | Đặt Agenda Item hiện tại *(Set Current Agenda Item)* | ĐÃ ĐẶC TẢ | Chair | `<<extend>>` UC-54 |
| UC-36 | Hoàn thành Agenda Item *(Complete Agenda Item)* | ĐÃ ĐẶC TẢ | Chair | `<<extend>>` UC-54 |
| UC-54 | Xem Agenda của phiên họp *(View Meeting Agenda)* | ĐÃ ĐẶC TẢ | Actor có quyền | `<<extend>>` UC-53 |

```text
UC-54 --<<extend>>--> UC-53

UC-31 ─┐
UC-32 ─┤
UC-33 ─┤
UC-34 ─┼─ <<extend>> → UC-54
UC-35 ─┤
UC-36 ─┘
```

---

## FE-07 — Quản lý thành phần tham dự  
**Meeting Participant Management**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-37 | Xem danh sách người tham gia *(View Meeting Participants)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-17 |
| UC-38 | Thêm người tham gia *(Add Meeting Participant)* | ĐÃ ĐẶC TẢ | Meeting Organizer / Secretary | Secretary path `<<extend>>` UC-37; Organizer bootstrap path `<<extend>>` UC-17 |
| UC-39 | Gán / đổi Meeting Role *(Assign / Change Meeting Role)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-37 |
| UC-40 | Xóa người tham gia *(Remove Meeting Participant)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-37 |
| UC-41 | Phản hồi lời mời họp *(Respond to Meeting Invitation)* | ĐÃ ĐẶC TẢ | Chair / Secretary / Participant / Guest | Độc lập |
| UC-42 | Xem trạng thái phản hồi *(View Participant Response Status)* | ĐÃ ĐẶC TẢ | Secretary | `<<extend>>` UC-37 |
| UC-88 | Ghi nhận điểm danh *(Record Attendance)* | PO ĐÃ XÁC NHẬN | Secretary | `<<extend>>` UC-37 |

### UC-37 / UC-38

- Khi meeting chưa có Secretary: Meeting Organizer dùng UC-38 để gán Secretary đầu tiên.
- Sau khi đã có Secretary: Secretary quản lý roster.

### UC-88 Attendance

- Attendance độc lập với Invitation Response.
- Attendance cũng độc lập với Room Presence.
- Online/Hybrid có thể pre-fill attendance dựa trên room presence.
- Secretary vẫn là người xác nhận/chỉnh official Attendance Status.
- InPerson meeting vẫn ghi attendance được dù không ai join online room.

---

## FE-08 — Kho tài liệu cá nhân & Quản lý tài liệu cuộc họp  
**Personal Document Library & Meeting Document Management**

**Priority:** Must-have

### Mô hình nghiệp vụ đã PO xác nhận

Mỗi internal user có **Kho tài liệu cá nhân** độc lập với một meeting cụ thể.

Khi thêm tài liệu vào meeting, Organizer / Secretary có 2 cách:

1. upload file trực tiếp từ máy tính;
2. chọn file đã có trong kho tài liệu cá nhân.

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-56 | Xem tài liệu của cuộc họp *(View Meeting Documents)* | ĐÃ LÊN KẾ HOẠCH — cần promote | Internal user có quyền; Guest nếu được phép | Internal: `<<extend>>` UC-17; Guest qua UC-87 |
| **UC-57** | **Thêm tài liệu vào cuộc họp *(Add Document to Meeting)*** | **ĐÃ LÊN KẾ HOẠCH — cần promote** | Meeting Organizer / Secretary | `<<extend>>` UC-56 |
| UC-58 | Thêm phiên bản mới của tài liệu cuộc họp *(Add New Meeting Document Version)* | ĐÃ LÊN KẾ HOẠCH | Meeting Organizer / Secretary | `<<extend>>` UC-56 |
| UC-59 | Xem lịch sử phiên bản *(View Document Version History)* | ĐÃ LÊN KẾ HOẠCH | User có quyền xem | `<<extend>>` UC-56 |
| UC-60 | Xem trước tài liệu *(Preview Meeting Document)* | ĐÃ LÊN KẾ HOẠCH | User có quyền xem | `<<extend>>` UC-56 |
| **UC-89** | **Xem kho tài liệu cá nhân *(View Personal Document Library)*** | **PO ĐÃ XÁC NHẬN** | Internal user | UC base độc lập |
| **UC-94** | **Tải tài liệu lên kho cá nhân *(Upload Document to Personal Library)*** | **PO ĐÃ XÁC NHẬN** | Internal user | `<<extend>>` UC-89 |
| **UC-91** | **Bình luận tài liệu cuộc họp *(Comment on Meeting Document)*** | **PO ĐÃ XÁC NHẬN** | User được cấp quyền comment | `<<extend>>` UC-60 |

```text
UC-56 --<<extend>>--> UC-17

UC-57 ─┐
UC-58 ─┤
UC-59 ─┼─ <<extend>> → UC-56
UC-60 ─┘

UC-91 --<<extend>>--> UC-60

UC-94 --<<extend>>--> UC-89
```

### UC-57 — Thêm tài liệu vào cuộc họp

**Mục tiêu:** tài liệu được gắn vào meeting.

#### Main Flow — Upload từ máy tính

1. Organizer / Secretary mở khu vực tài liệu của meeting.
2. Chọn Add Document.
3. Chọn file từ máy tính.
4. Hệ thống validate file + quyền của meeting.
5. Hệ thống tạo tài liệu/phiên bản đầu tiên trong meeting.
6. Tài liệu xuất hiện trong danh sách tài liệu của meeting.

#### AF-1 — Chọn từ kho tài liệu cá nhân

Tại bước 3:

1. Actor chọn **Choose from My Documents**.
2. Hệ thống hiển thị kho tài liệu cá nhân của actor.
3. Actor chọn document/version có sẵn.
4. Hệ thống kiểm tra quyền sử dụng tài liệu.
5. Hệ thống gắn **đúng version được chọn** vào meeting.
6. Quay lại bước 6 Main Flow.

### Rule quan trọng về version

Khi một tài liệu từ kho cá nhân đã được gắn vào meeting:

> Meeting phải giữ version đã chọn tại thời điểm add.

Nếu tài liệu trong kho cá nhân thay đổi sau đó thì tài liệu đã gắn vào meeting **không tự cập nhật theo**.

Muốn đổi version trong meeting phải dùng UC-58.

### UC-89 — Xem kho tài liệu cá nhân

- Chỉ dành cho internal user.
- Guest không có kho tài liệu cá nhân.
- Search/filter là AF.
- Sort/pagination là BR/UI behavior.

### UC-94 — Upload tài liệu vào kho cá nhân

- Internal user upload file từ máy tính vào kho của chính mình.
- Upload thành công chưa có nghĩa là file đã thuộc meeting.
- Sau đó file có thể được chọn từ UC-57 AF-1.

### UC-57 và UC-89 có `include` không?

**Không.**

Không vẽ:

```text
UC-57 <<include>> UC-89
```

vì UC-57 vẫn có thể thành công bằng cách upload thẳng từ máy tính.

Chọn từ My Documents chỉ là **Alternative Flow của UC-57**.

### Document review

`Documents awaiting review` ở Dashboard chưa tạo ra workflow approve/reject tài liệu.

Hiện tại chỉ chốt:

- view/preview;
- comment;
- dashboard có thể đưa tài liệu cần chú ý lên cho user.

Nếu sau này cần reviewer assignment / Reviewed status thì phải định nghĩa BR trước.

---

## FE-09 — Phòng họp trực tuyến  
**Online Meeting Room Management**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-45 | Tham gia phòng họp *(Join Meeting Room)* | ĐÃ ĐẶC TẢ | Chair / Secretary / Participant / Guest được phép | Internal `<<extend>>` UC-17; Guest qua UC-87 |
| UC-46 | Rời phòng họp *(Leave Meeting Room)* | ĐÃ ĐẶC TẢ | User đang connected | Độc lập sau khi connected |
| UC-47 | Quản lý microphone *(Manage Microphone)* | ĐÃ ĐẶC TẢ | User đang connected | Độc lập |
| UC-48 | Quản lý camera *(Manage Camera)* | ĐÃ ĐẶC TẢ | User đang connected | Độc lập |
| UC-49 | Chia sẻ màn hình *(Manage Screen Sharing)* | ĐÃ ĐẶC TẢ | User đang connected | Độc lập |
| UC-50 | Xem người đang trong phòng *(View Meeting Room Participants)* | ĐÃ ĐẶC TẢ | User đang connected | Độc lập |
| UC-51 | Điều phối phòng họp *(Moderate Meeting Room)* | ĐÃ ĐẶC TẢ | Chair | Độc lập |
| UC-52 | Chat trong phòng họp *(Use In-room Chat)* | ĐÃ ĐẶC TẢ | User đang connected | Độc lập |

Không vẽ UC-46 → UC-52 `include` UC-45.  
`Currently connected` chỉ là precondition.

---

## FE-10 — Upload Audio cuộc họp & Thư viện Media  
**Meeting Audio Upload & Media Library**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-61 | Xem Media Library *(View Media Library)* | ĐÃ LÊN KẾ HOẠCH | User có quyền | `<<extend>>` UC-17 |
| UC-62 | Upload Audio cuộc họp *(Upload Meeting Audio)* | ĐÃ LÊN KẾ HOẠCH | **Secretary** | `<<extend>>` UC-61 |
| UC-63 | Phát Audio cuộc họp *(Play Meeting Audio)* | ĐÃ LÊN KẾ HOẠCH | User có quyền | `<<extend>>` UC-61 |

```text
UC-61 --<<extend>>--> UC-17
UC-62 --<<extend>>--> UC-61
UC-63 --<<extend>>--> UC-61
```

### UC-62 — rule đã PO chốt

- Actor duy nhất: **Secretary**.
- Secretary phải được assign vào meeting.
- Không có UC Assign Audio Uploader.
- Không có actor / meeting role Audio Uploader.
- Notification yêu cầu upload audio gửi cho Secretary.
- Secretary upload file sau meeting bằng hành động chủ động của con người.
- Hệ thống không tự record.
- Automatic/server-side recording vẫn out of scope.

### Không phải UC

- queue ASR;
- retry processing;
- storage worker;
- background processing.

Đây là behavior kỹ thuật.

---

## FE-11 — Transcript có Speaker & Tóm tắt cuộc họp  
**Speaker-Attributed Transcription & Meeting Summary**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-64 | Xem transcript có speaker *(View Speaker-Attributed Transcript)* | ĐÃ LÊN KẾ HOẠCH | Secretary | `<<extend>>` UC-17 |
| UC-65 | Chỉnh transcript & speaker *(Edit Transcript & Speaker Attribution)* | ĐÃ LÊN KẾ HOẠCH | Secretary | `<<extend>>` UC-64 |
| UC-90 | Generate / Regenerate Meeting Summary | PO ĐÃ XÁC NHẬN | Secretary | `<<extend>>` UC-64 |
| UC-66 | Xem Meeting Summary *(View Meeting Summary)* | ĐÃ LÊN KẾ HOẠCH | Secretary / user được xem bản published | `<<extend>>` UC-17 |
| UC-67 | Chỉnh Meeting Summary *(Edit Meeting Summary)* | ĐÃ LÊN KẾ HOẠCH | Secretary | `<<extend>>` UC-66 |
| UC-68 | Duyệt Meeting Summary *(Approve Meeting Summary)* | ĐÃ LÊN KẾ HOẠCH | Secretary | `<<extend>>` UC-66 |
| UC-69 | Công bố Meeting Summary *(Publish Meeting Summary)* | ĐÃ LÊN KẾ HOẠCH | Secretary | `<<extend>>` UC-66 |

Luồng:

```text
Secretary upload audio (UC-62)
        ↓
System xử lý transcript + diarization
        ↓
UC-64 View Transcript
        ↓
UC-65 sửa transcript / speaker label
        ↓
UC-90 Generate / Regenerate Summary
        ↓
UC-66 View Summary
        ↓
UC-67 Edit
        ↓
UC-68 Approve
        ↓
UC-69 Publish
```

### Quyết định Approve / Publish

UC-68 và UC-69 tách riêng.

Hệ thống **không tự publish** ngay khi Secretary approve.

### Biên AI

AI / Speech Processing Provider xử lý:

- audio;
- transcript có speaker;
- draft summary dựa trên transcript.

AI không trực tiếp ban hành formal minutes.

Action Item trong summary chỉ là **text**, không trở thành tracked task.

---

## FE-12 — Biểu quyết & Nghị quyết  
**Voting & Resolution Management**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-70 | Xem các biểu quyết của meeting *(View Meeting Votes)* | ĐÃ LÊN KẾ HOẠCH | Actor có quyền | `<<extend>>` UC-17 |
| UC-71 | Tạo / cấu hình biểu quyết *(Create / Configure Vote)* | ĐÃ LÊN KẾ HOẠCH | Chair | `<<extend>>` UC-70 |
| UC-72 | Mở / Publish biểu quyết *(Open / Publish Vote)* | ĐÃ LÊN KẾ HOẠCH | Chair | `<<extend>>` UC-70 |
| UC-73 | Bỏ phiếu *(Cast Vote)* | ĐÃ LÊN KẾ HOẠCH | Participant đủ điều kiện | `<<extend>>` UC-70 |
| UC-74 | Xem trạng thái biểu quyết *(View Vote Status)* | ĐÃ LÊN KẾ HOẠCH | Actor theo rule visibility | `<<extend>>` UC-70 |
| UC-75 | Đóng biểu quyết *(Close Vote)* | ĐÃ LÊN KẾ HOẠCH | Chair | `<<extend>>` UC-70 |
| UC-76 | Xem nghị quyết *(View Resolution)* | ĐÃ LÊN KẾ HOẠCH | Actor có quyền | `<<extend>>` UC-70 |

**Resolution là business outcome chính thức**, không phải chi tiết thiết kế dữ liệu.

Sau khi vote đóng và kết quả hợp lệ:

> Kết quả được ghi thành Resolution gắn vào meeting và bất biến.

---

## FE-13 — Biên bản cuộc họp & Ký điện tử  
**Meeting Minutes & Digital Signing**

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-77 | Xem Meeting Minutes *(View Meeting Minutes)* | ĐÃ LÊN KẾ HOẠCH | User được quyền | `<<extend>>` UC-17 |
| UC-78 | Chuẩn bị draft Minutes *(Prepare Draft Meeting Minutes)* | ĐÃ LÊN KẾ HOẠCH | Secretary | `<<extend>>` UC-77 |
| UC-79 | Chỉnh Minutes *(Edit Meeting Minutes)* | ĐÃ LÊN KẾ HOẠCH | Secretary | `<<extend>>` UC-77 |
| UC-80 | Gửi Minutes để ký *(Submit Meeting Minutes for Signing)* | ĐÃ LÊN KẾ HOẠCH | Secretary | `<<extend>>` UC-77 |
| UC-81 | Ký Minutes *(Sign Meeting Minutes)* | ĐÃ LÊN KẾ HOẠCH | Designated signer | `<<extend>>` UC-77 |
| UC-82 | Xem trạng thái ký & Audit Trail *(View Signature Status & Audit Trail)* | ĐÃ LÊN KẾ HOẠCH | User có quyền | `<<extend>>` UC-77 |
| UC-83 | Finalize / Seal Meeting Minutes | ĐÃ LÊN KẾ HOẠCH | **Secretary** | `<<extend>>` UC-77 |
| UC-93 | Yêu cầu sửa Minutes *(Request Revision of Meeting Minutes)* | PO ĐÃ XÁC NHẬN | Designated signer | `<<extend>>` UC-77 |

### Rule sửa minutes sau khi đã bước vào signing

Nếu signer yêu cầu sửa:

1. UC-93 kết thúc với trạng thái yêu cầu revision.
2. Secretary chỉnh minutes.
3. Mọi chữ ký đã ký trên **version cũ** không còn hợp lệ cho nội dung mới.
4. Mở signing cycle mới.
5. Các signer bắt buộc phải ký lại version mới.
6. Signature/version cũ vẫn giữ trong audit trail để làm lịch sử, không bị xóa.

UC-93 **không** phải AF của UC-81 vì request revision không đạt mục tiêu "Sign".

---

## FE-14 — Email & In-App Notification

**Priority:** Must-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-22 | Xem thông báo *(View Notifications)* | ĐÃ ĐẶC TẢ | Internal user | Base |
| UC-23 | Đánh dấu một thông báo đã đọc *(Mark Notification as Read)* | ĐÃ ĐẶC TẢ | Internal user | `<<extend>>` UC-22 |
| UC-24 | Đánh dấu tất cả đã đọc *(Mark All Notifications as Read)* | ĐÃ ĐẶC TẢ | Internal user | `<<extend>>` UC-22 |
| UC-25 | Mở đối tượng từ thông báo *(Open Notification Target)* | ĐÃ ĐẶC TẢ | Internal user | `<<extend>>` UC-22 |

Notification scope cần cover:

- yêu cầu duyệt / kết quả duyệt;
- meeting invitation;
- session/agenda change nếu áp dụng;
- reminder trước meeting;
- yêu cầu / nhắc **Secretary upload meeting audio**;
- voting call;
- request ký minutes.

Task notification bị loại khỏi scope.

---

## FE-15 — Dashboard cá nhân & Meeting Workspace  
**Personal Dashboard & Meeting Workspace**

**Priority:** Should-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-84 | Xem Dashboard cá nhân & Meeting Workspace *(View Personal Dashboard & Meeting Workspace)* | ĐÃ LÊN KẾ HOẠCH | Internal user | Độc lập |

Dashboard có thể hiển thị:

- upcoming meetings;
- meeting đang chờ user duyệt;
- documents cần user chú ý/review;
- open votes;
- minutes đang chờ chữ ký.

Không tạo duplicate UC cho từng card nếu card chỉ điều hướng sang UC đã có.

---

## FE-16 — Báo cáo & Thống kê  
**Reporting & Statistics**

**Priority:** Should-have

| UC | Use Case | Trạng thái | Actor | Quan hệ |
|---|---|---|---|---|
| UC-85 | Xem Báo cáo & Thống kê *(View Reports & Statistics)* | ĐÃ LÊN KẾ HOẠCH | User có reporting permission | Base |
| UC-86 | Xuất báo cáo *(Export Report)* | ĐÃ LÊN KẾ HOẠCH | User có reporting permission | `<<extend>>` UC-85 |

Dữ liệu có thể gồm:

- số meeting;
- attendance;
- decisions;
- resolutions;
- minutes đã ký.

Reporting dimension `by project` chỉ được dùng khi Project thực sự là domain entity được xác nhận.

---

# 5. Ma trận quan hệ `<<extend>>`

```text
# FE-02
UC-05  --<<extend>>--> UC-04
UC-06  --<<extend>>--> UC-04
UC-07  --<<extend>>--> UC-04
UC-08  --<<extend>>--> UC-04
UC-09  --<<extend>>--> UC-04
UC-55  --<<extend>>--> UC-04

# FE-03
UC-11  --<<extend>>--> UC-10
UC-12  --<<extend>>--> UC-10
UC-13  --<<extend>>--> UC-10
UC-14  --<<extend>>--> UC-10

# FE-04
UC-15  --<<extend>>--> UC-18
UC-17  --<<extend>>--> UC-18
UC-16  --<<extend>>--> UC-17
UC-19  --<<extend>>--> UC-17
UC-21  --<<extend>>--> UC-17
UC-43  --<<extend>>--> UC-17
UC-44  --<<extend>>--> UC-17

# FE-05
UC-53  --<<extend>>--> UC-17
UC-26  --<<extend>>--> UC-53
UC-27  --<<extend>>--> UC-53
UC-28  --<<extend>>--> UC-53
UC-29  --<<extend>>--> UC-53
UC-30  --<<extend>>--> UC-53

# FE-06
UC-54  --<<extend>>--> UC-53
UC-31  --<<extend>>--> UC-54
UC-32  --<<extend>>--> UC-54
UC-33  --<<extend>>--> UC-54
UC-34  --<<extend>>--> UC-54
UC-35  --<<extend>>--> UC-54
UC-36  --<<extend>>--> UC-54

# FE-07
UC-37  --<<extend>>--> UC-17
UC-38 [Secretary path] --<<extend>>--> UC-37
UC-38 [Organizer bootstrap path] --<<extend>>--> UC-17
UC-39  --<<extend>>--> UC-37
UC-40  --<<extend>>--> UC-37
UC-42  --<<extend>>--> UC-37
UC-88  --<<extend>>--> UC-37

# FE-08
UC-56  --<<extend>>--> UC-17
UC-57  --<<extend>>--> UC-56
UC-58  --<<extend>>--> UC-56
UC-59  --<<extend>>--> UC-56
UC-60  --<<extend>>--> UC-56
UC-91  --<<extend>>--> UC-60
UC-94  --<<extend>>--> UC-89

# FE-09
UC-45  --<<extend>>--> UC-17 [internal path]

# FE-10
UC-61  --<<extend>>--> UC-17
UC-62  --<<extend>>--> UC-61
UC-63  --<<extend>>--> UC-61

# FE-11
UC-64  --<<extend>>--> UC-17
UC-65  --<<extend>>--> UC-64
UC-90  --<<extend>>--> UC-64
UC-66  --<<extend>>--> UC-17
UC-67  --<<extend>>--> UC-66
UC-68  --<<extend>>--> UC-66
UC-69  --<<extend>>--> UC-66

# FE-12
UC-70  --<<extend>>--> UC-17
UC-71  --<<extend>>--> UC-70
UC-72  --<<extend>>--> UC-70
UC-73  --<<extend>>--> UC-70
UC-74  --<<extend>>--> UC-70
UC-75  --<<extend>>--> UC-70
UC-76  --<<extend>>--> UC-70

# FE-13
UC-77  --<<extend>>--> UC-17
UC-78  --<<extend>>--> UC-77
UC-79  --<<extend>>--> UC-77
UC-80  --<<extend>>--> UC-77
UC-81  --<<extend>>--> UC-77
UC-82  --<<extend>>--> UC-77
UC-83  --<<extend>>--> UC-77
UC-93  --<<extend>>--> UC-77

# FE-14
UC-23  --<<extend>>--> UC-22
UC-24  --<<extend>>--> UC-22
UC-25  --<<extend>>--> UC-22

# FE-16
UC-86  --<<extend>>--> UC-85
```

## Các UC độc lập / nối trực tiếp với Actor

```text
UC-01 Đăng nhập Email/Password
UC-02 Đăng nhập Google
UC-03 Đăng xuất
UC-20 Duyệt cuộc họp
UC-41 Phản hồi lời mời họp
UC-46 Rời room
UC-47 Microphone
UC-48 Camera
UC-49 Screen Share
UC-50 Xem user trong room
UC-51 Moderate room
UC-52 In-room Chat
UC-84 Dashboard
UC-85 Reports
UC-87 Guest Access
UC-89 Personal Document Library
UC-92 Audit Log
```

## `<<include>>`

### Diagram tổng

Hiện **không có include bắt buộc nào cần vẽ**.

### Guest-only diagram

Có thể thể hiện có constraint:

```text
UC-41 [Guest path] --<<include>>--> UC-87
UC-45 [Guest path] --<<include>>--> UC-87
```

### Document Library

Không vẽ:

```text
UC-57 <<include>> UC-89
```

vì `Add Document to Meeting` có thể thành công bằng local upload.

---

# 6. Lifecycle / dependency map

## Meeting

```text
UC-15 Create → Draft
        ↓ UC-19 Submit
WaitingApproval
        ↓ UC-20 Approve / Reject
Approved / Rejected
        ↓ UC-21 Publish (nếu Approved)
Published
        ↓ UC-43 Start
InProgress
        ↓ UC-44 End
Ended
```

## Meeting Session

```text
UC-26 Create → Planned
                ├─ UC-27 Update
                ├─ UC-28 Cancel → Cancelled
                └─ UC-29 Start → InProgress
                                   └─ UC-30 End → Completed
```

## Agenda Item

```text
UC-31 Create → Pending
                ├─ UC-32 Update
                ├─ UC-33 Delete
                ├─ UC-34 Reorder
                └─ UC-35 Set Current → InProgress
                                         └─ UC-36 Complete → Done
```

## Guest

```text
Invitation
   ↓
UC-87 Access Meeting via Guest Invitation
   ↓
Meeting-scoped Guest Session
   ├─ UC-41 Respond Invitation
   ├─ UC-45 Join Room
   └─ Document access nếu được BR cho phép
```

## Document

```text
UC-94 Upload to My Documents
   ↓
UC-89 View Personal Document Library
   ↓
UC-57 AF-1 Choose from My Documents
   ↓
Document version attached to Meeting
   ↓
UC-58 nếu cần add version mới
```

Hoặc:

```text
Local computer
   ↓
UC-57 Main Flow
   ↓
Document attached to Meeting
```

## Audio → AI Summary

```text
Secretary
   ↓
UC-62 Upload Meeting Audio
   ↓
System ASR + Speaker Attribution
   ↓
UC-64 View Transcript
   ↓
UC-65 Edit Transcript / Speaker
   ↓
UC-90 Generate / Regenerate Summary
   ↓
UC-66 View Summary
   ↓
UC-67 Edit → UC-68 Approve → UC-69 Publish
```

## Voting

```text
UC-71 Configure Vote
   ↓
UC-72 Open / Publish
   ├─ UC-73 Cast Vote
   ├─ UC-74 View Status
   ↓
UC-75 Close
   ↓
Record immutable Resolution
   ↓
UC-76 View Resolution
```

## Minutes

```text
UC-78 Prepare Draft
   ↓
UC-79 Edit
   ↓
UC-80 Submit for Signing
   ↓
Signing Cycle
   ├─ UC-81 Sign
   └─ UC-93 Request Revision
          ↓
       Secretary Edit
          ↓
       invalidate signatures of old content version
          ↓
       new Signing Cycle
   ↓
All required signatures completed
   ↓
UC-83 Secretary Finalize / Seal
```

---

# 7. Thống kê số lượng UC

| FE | Feature | Số UC |
|---|---|---:|
| FE-01 | Authentication & Account Security | 4 |
| FE-02 | User & Profile Management | 7 |
| FE-03 | Role-Based Access Control | 6 |
| FE-04 | Meeting Management | 9 |
| FE-05 | Meeting Session Management | 6 |
| FE-06 | Agenda Management | 7 |
| FE-07 | Meeting Participant Management | 7 |
| FE-08 | Personal Document Library & Meeting Document Management | 8 |
| FE-09 | Online Meeting Room Management | 8 |
| FE-10 | Meeting Audio Upload & Media Library | 3 |
| FE-11 | Speaker-Attributed Transcription & Meeting Summary | 7 |
| FE-12 | Voting & Resolution Management | 7 |
| FE-13 | Meeting Minutes & Digital Signing | 8 |
| FE-14 | Email & In-App Notification | 4 |
| FE-15 | Personal Dashboard & Meeting Workspace | 1 |
| FE-16 | Reporting & Statistics | 2 |
| **TỔNG** |  | **94** |

---

# 8. Thứ tự promote / đặc tả

## Làm ngay

1. UC-55 Unlock User Account.
2. UC-56 → UC-60 FE-08.
3. UC-87 Guest Access.
4. UC-88 Attendance.
5. UC-89 View Personal Document Library.
6. UC-94 Upload Document to Personal Library.
7. UC-91 Comment on Meeting Document.
8. Cleanup actor UC-01/02/03/06.
9. Sửa Context AI.
10. Xóa task-related content trái LI-12.
11. Sắp lại thứ tự section FE trong Report 3.
12. Xóa nội dung template tuyển dụng còn sót.

## Trước FE-10 / FE-11

1. Sửa UC-62 actor = Secretary.
2. Xóa toàn bộ dependency vào Assign Audio Uploader.
3. Promote UC-90.
4. Chốt audio format / size / upload count.
5. Chốt transcript visibility.
6. Giữ UC-68 và UC-69 riêng.

## Trước FE-13

1. Promote UC-93.
2. Chốt designated signer.
3. BR về invalid signature khi minutes bị sửa sau khi signing đã bắt đầu.
4. Secretary là actor UC-83.

## Trước FE-16

1. Promote UC-92.
2. Chốt reporting dimension.
3. Chốt notification target cho vote/minutes/document.

---

# 9. Các correction bắt buộc trong Report 3

## 9.1 Actor

Loại các actor legacy không nằm trong canonical model:

- Internal User;
- Workspace Manager;
- DPO;
- Auditor.

UC-01 email/password không có generic Identity Provider.

## 9.2 AI Context

Mô tả chuẩn:

> AI / Speech Processing Provider xử lý meeting audio đã upload để tạo speaker-attributed transcript và draft summary từ transcript. Formal meeting minutes được chuẩn bị riêng từ meeting records đã được duyệt; AI provider không trực tiếp sinh minutes từ secretary notes hay meeting documents.

## 9.3 Xóa template tuyển dụng

Xóa / thay:

- Candidate Self-Service;
- End-to-End Recruitment;
- Job Posting / Application / Interview;
- HR Manager / Interviewer / Candidate;
- User Register;
- VnPay / SePay sample;
- NFR về CV/public jobs;
- sample JOB không thuộc TrustCabinet.

## 9.4 Xóa task management

Không còn:

- assigned tasks;
- task notifications;
- task lifecycle.

Action items chỉ là text.

## 9.5 Notification BR

Bổ sung đủ:

- approval;
- invitation;
- agenda/session changes;
- reminders;
- Secretary audio upload request;
- vote;
- minutes signing.

## 9.6 Thuật ngữ role

Business-facing term dùng **Participant**.

## 9.7 Guest verification

Sau khi UC-87 được promote:

- UC-41 Guest path chỉ reference UC-87/BR-102.
- UC-45 Guest path cũng vậy.
- Không lặp toàn bộ OTP flow ở nhiều UC.

---

# 10. Các scope decision đã được PO xác nhận

| UC / Quyết định | Scope đã chốt |
|---|---|
| UC-88 | Secretary ghi official Attendance. |
| Audio upload | Không có Assign Audio Uploader; Secretary thực hiện UC-62. |
| UC-89 | Internal user xem kho tài liệu cá nhân. |
| UC-94 | Internal user upload tài liệu vào kho cá nhân. |
| UC-57 | Add Document to Meeting có 2 nguồn: local computer hoặc My Documents. |
| UC-90 | Secretary Generate / Regenerate Summary sau khi chỉnh transcript. |
| UC-91 | Comment on Meeting Document nằm trong scope. |
| UC-92 | System Admin có View Audit Log. |
| UC-93 | Designated signer có thể Request Revision of Minutes. |

---

# 11. Kế hoạch đồng bộ Report 1 / Report 2 / Report 3

## 11.1 Report 1 — Project Introduction

### FE-07

Giữ:

- attendance do Secretary quản lý;
- bỏ mọi mô tả liên quan assign Audio Uploader.

### FE-08

Đổi tên khuyến nghị:

> **FE-08 — Personal Document Library & Meeting Document Management**

Mô tả cần nói rõ:

- mỗi internal user có kho tài liệu cá nhân;
- user có thể upload tài liệu vào kho trước khi có meeting;
- Organizer / Secretary thêm tài liệu vào meeting từ:
  - máy tính;
  - kho tài liệu cá nhân;
- tài liệu meeting có version;
- preview/search được;
- meeting giữ version được chọn;
- authorized user có thể comment.

### FE-10

Thay wording cũ `designated participant uploads audio` bằng:

> Hệ thống không tự record. Sau meeting, Secretary được assign vào meeting chủ động upload audio file. Audio được gắn vào meeting, lưu theo quyền của meeting, phát lại được trên web và được đưa vào pipeline hậu kỳ.

LI-01 cũng sửa theo Secretary.

### FE-11

Bổ sung rõ:

- Secretary có thể Generate / Regenerate Summary sau khi sửa transcript / speaker.

### FE-13

Bổ sung:

- signer có thể Request Revision;
- sửa minutes sau khi signing bắt đầu làm signature của version cũ không còn hiệu lực với version mới.

### FE-03

Bổ sung user-visible View Audit Log cho System Admin.

### Change Log Report 1

Thêm entry ngày 05/10/2026, ví dụ:

> Clarify PO-approved document workflow by adding a personal document library and allowing meeting documents to be added from local files or the user's library; make Secretary responsible for meeting-audio upload; align attendance, summary generation, document comments, audit-log viewing and minutes-revision scope with the Master UC Inventory.

---

## 11.2 Report 2 — Project Management Plan

### Iteration 2 — Document

Work package FE-08 phải cover:

- My Documents;
- upload vào personal library;
- search/view library;
- add vào meeting từ local;
- add vào meeting từ My Documents;
- version history;
- preview;
- comment;
- access-control giữa personal documents và meeting documents.

Demo Iteration 2 nên có:

```text
Upload document vào My Documents
→ mở meeting
→ Add Document from My Documents
→ Add thêm một document trực tiếp từ máy
→ Preview / kiểm tra version trong meeting
```

### Iteration 3 — Audio / AI

Không còn câu hỏi “who may upload”.

Chốt:

> **Secretary là actor upload meeting audio.**

Cập nhật:

- Requirements;
- implementation tasks;
- test cases;
- notification target.

### Effort / Risk

FE-08 cần re-estimate vì personal library thêm:

- ownership/access;
- library screen;
- upload ngoài meeting context;
- reuse/select document trong meeting.

Ghi vào Change Log của Report 2 vì đây là scope clarification được PO duyệt.

---

## 11.3 Report 3 — SRS

### FE-08

Promote / cập nhật:

- UC-56 View Meeting Documents.
- UC-57 Add Document to Meeting.
  - Main: upload từ máy.
  - AF-1: chọn từ Personal Document Library.
- UC-58 Add New Meeting Document Version.
- UC-59 View Document Version History.
- UC-60 Preview Meeting Document.
- UC-89 View Personal Document Library.
- UC-94 Upload Document to Personal Library.
- UC-91 Comment on Meeting Document.

### BR cho Document

Cần có:

1. ownership/access của personal library;
2. Guest không có personal library;
3. meeting-document access kế thừa meeting permission;
4. add từ library sẽ gắn version được chọn;
5. thay đổi file trong personal library không tự đổi version trong meeting;
6. search/filter của cả My Documents và meeting documents;
7. retention của version history.

### FE-10

UC-62:

- Primary Actor = **Secretary**.
- PRE: Secretary được assign meeting.
- meeting ở trạng thái cho phép post-meeting audio upload.
- xóa toàn bộ Assign Audio Uploader.
- xóa mọi `Designated Audio Uploader`.

Notification audio upload gửi cho Secretary.

### Các UC cần promote

- UC-55
- UC-87
- UC-88
- UC-89
- UC-90
- UC-91
- UC-92
- UC-93
- UC-94
- UC-56 → UC-60

### Actor / Diagram

- Không thêm Audio Uploader actor.
- Guest không có My Documents.
- UC-62 nối trực tiếp với Secretary.
- UC-89/94 chỉ nối các actor có internal account.

### Change Log Report 3

Ghi:

- thêm Personal Document Library;
- 2 cách Add Document to Meeting;
- Secretary-only audio upload;
- loại bỏ Assign Audio Uploader;
- promote các UC được PO xác nhận.

---

## 11.4 Business Flow / Use Case Diagram

- BF-02: bỏ bước assign designated audio uploader.
- BF-04: lane Secretary thực hiện Upload Meeting Audio.
- Document flow: thể hiện decision:
  - Upload from computer;
  - Choose from My Documents.
- UC Diagram: UC-89 thuộc FE-08 = View Personal Document Library.
- Thêm UC-94 dưới FE-08.
- Không tạo actor Audio Uploader.

---

# 12. Những thứ trong code không tự động trở thành UC

| Có trong code | Xử lý |
|---|---|
| ReturnToDraft | Chưa thuộc scope; phải approve requirement hoặc bỏ code |
| TransferOrganizer | Chưa thuộc scope; phải approve requirement hoặc bỏ code |
| Meeting history endpoint | AF/view của UC-17 |
| `/calendar` page | AF/view của UC-18 |
| Guest lưu như internal participant | Data model phải sửa theo Guest no-account |
| BrowserCapture / CallRoom recording enum | Trái LI-01, cần cleanup |
| Minutes approval/revision legacy state | Chỉ giữ nếu FE-13 requirement chấp nhận |
| SpeakingRequest / MeetingNote | Không tạo UC chỉ vì code có |
| comment UC numbering cũ | Dev cleanup |

---

# 13. Cách chia Use Case Diagram đề xuất

Không vẽ 94 UC vào một hình.

Bản sơ đồ theo mẫu tham khảo: mỗi actor có một ảnh duy nhất chứa toàn bộ UC của actor đó, không tách trang theo Feature: [Use Case Diagram theo Actor](../assets/use-case-diagrams/README.md).

1. **Overall TrustCabinet**
   - 7 actor
   - 16 FE package / representative UC

2. **FE-01 Authentication & Guest Access**
   - UC-01, UC-02, UC-03, UC-87

3. **FE-02 + FE-03 User / Role / Audit**
   - UC-04 → UC-14, UC-55, UC-92

4. **FE-04 Meeting**
   - UC-15 → UC-21, UC-43, UC-44

5. **FE-05 + FE-06 Session & Agenda**
   - UC-26 → UC-36, UC-53, UC-54

6. **FE-07 Participant / Attendance**
   - UC-37 → UC-42, UC-88

7. **FE-08 Personal Document Library & Meeting Documents**
   - UC-56 → UC-60, UC-89, UC-91, UC-94

8. **FE-09 Online Meeting Room**
   - UC-45 → UC-52

9. **FE-10 + FE-11 Audio / Transcript / Summary**
   - UC-61 → UC-69, UC-90

10. **FE-12 Voting & Resolution**
    - UC-70 → UC-76

11. **FE-13 Minutes & Signing**
    - UC-77 → UC-83, UC-93

12. **FE-14 Notification**
    - UC-22 → UC-25

13. **FE-15 + FE-16 Dashboard & Reporting**
    - UC-84 → UC-86

---

# 14. Quy tắc baseline cuối cùng

1. UC-01 → UC-54 giữ nguyên ID.
2. UC-55 → UC-94 được reserve trong master này.
3. UC mới chỉ trở thành SRS chính thức khi được đưa vào Report 3 với Actor, Preconditions, Postconditions, Main Flow, AF/EX, BR, MSG.
4. Không renumber UC chỉ để gom Feature.
5. Không tạo UC cho technical job/background worker nếu không có user/business goal.
6. Search/filter thường nằm trong View/List UC.
7. Sort/pagination thường là BR/UI behavior.
8. Lifecycle Meeting/Session/Agenda/Vote/Minutes không phải `include`.
9. Guest access dùng UC-87; Guest không phải internal actor.
10. Attendance độc lập với invitation response và room presence.
11. Không có Audio Uploader role/actor/assignment UC; UC-62 thuộc Secretary.
12. AI tạo transcript/summary; formal minutes là governance feature riêng.
13. Task assignment/tracking/deadline vẫn out of scope.
14. Code-only behavior không tự thành scope.
15. UC-88 → UC-94 đã được PO xác nhận và được phép đưa vào final Use Case Diagram sau khi sync report.
16. Request Revision là UC-93, không phải AF của UC-81.
17. Nếu minutes đổi nội dung sau khi signing bắt đầu, signature của content version cũ không hợp lệ cho version mới.
18. UC-62 actor = Secretary.
19. Resolution là business outcome bất biến của closed vote.
20. Personal Document Library chỉ dành cho internal user; Guest không có My Documents.
21. UC-57 Add Document to Meeting có 2 đường thành công: local computer hoặc Personal Document Library.
22. Document version đã gắn vào meeting không tự thay đổi khi source trong kho cá nhân thay đổi.
