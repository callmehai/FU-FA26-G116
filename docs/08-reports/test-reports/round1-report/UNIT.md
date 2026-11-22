# Round 1 — Kết quả Unit Test (Report 5.1)

- Người kiểm thử: Phạm Gia Khánh
- Ngày chạy: 21–22/11/2026
- Mã nguồn được kiểm thử: nhánh `app`, commit `23c41181`
- Số liệu và test case: `docs/08-reports/test-reports/cases/FE-xx/unit/*.json` → `Report5.1_Unit_Test.xlsx`
- Test tự động: `tests/TrustCabinet.Application.Tests/Unit/Round1/` và `tests/TrustCabinet.Web.Tests/Authentication/PasswordVerifyJwtRound1Tests.cs`; mỗi test gắn với một UTC qua trait `UTC` và tên hiển thị.

## 1. Tóm tắt

| Chỉ số | Giá trị |
|---|---|
| Hàm được kiểm thử (sheet) | 158 |
| Unit Test Case (UTC) | 844 |
| Passed | 821 |
| Failed | 0 |
| Pending | 23 |

Không có lỗi sản phẩm. Các UTC Pending chưa có test tự động, lý do ở §3.

## 2. Kết quả theo tính năng

| FE | UTC | Passed | Failed | Pending |
|---|---|---|---|---|
| FE-01 Xác thực | 47 | 47 | 0 | 0 |
| FE-02 Người dùng | 30 | 30 | 0 | 0 |
| FE-03 Phân quyền | 38 | 38 | 0 | 0 |
| FE-04 Cuộc họp | 76 | 76 | 0 | 0 |
| FE-05 Chương trình họp | 80 | 80 | 0 | 0 |
| FE-06 Thành phần tham dự | 12 | 12 | 0 | 0 |
| FE-07 Tài liệu | 68 | 68 | 0 | 0 |
| FE-08 Phòng họp | 56 | 51 | 0 | 5 |
| FE-09 Ghi âm | 109 | 108 | 0 | 1 |
| FE-10 Phiên âm | 87 | 82 | 0 | 5 |
| FE-11 Biểu quyết | 33 | 33 | 0 | 0 |
| FE-12 Biên bản | 66 | 55 | 0 | 11 |
| FE-13 Thông báo | 62 | 61 | 0 | 1 |
| FE-15 Báo cáo | 10 | 10 | 0 | 0 |
| FE-16 Lịch | 70 | 70 | 0 | 0 |
| **Tổng** | **844** | **821** | **0** | **23** |

Mỗi sheet có tối đa 6 UTC (hàm xử lý trạng thái, quyền, biểu quyết, biên bản, xung đột lịch tối đa 8), mỗi hàm có ít nhất 1 UTC. Mỗi sheet có đúng 1 ca Normal cho luồng chính, mỗi mã lỗi MSG một ca Abnormal đại diện, Boundary chỉ giữ giới hạn số cụ thể trong code.

## 3. UTC Pending (23)

| UTC | Lý do |
|---|---|
| CallRoom.EnforceRoomAccess UTC001; CallRoom.Reconcile UTC001, 002, 003, 005; Notification.QueueStartingSoon UTC006 | Cần liệt kê phiên và lọc theo trạng thái, test double hiện tại chưa mô phỏng |
| SpeechClip.AddForGuest UTC004 | Cần mô phỏng thu hồi quyền giữa lúc kiểm tra và lúc lưu |
| Summary.ProcessNext UTC003, UTC006 | Cần mô phỏng hai tiến trình chạy song song và rollback giao dịch |
| Transcription.ProcessNext UTC005, UTC006 | Cần mô phỏng hủy thay đổi và lỗi ở bước nhận việc |
| VoiceId.SampleAudio UTC004 | Cần mô phỏng lỗi giải mã mẫu giọng |
| Notes.Discussion UTC001–005, Notes.Read UTC001–006 | Lớp private lồng bên trong, hiện chỉ được kiểm gián tiếp qua `MinutesTemplate.Build` |

## 4. Test case thay đổi theo hành vi mới của hệ thống

| Hàm | Thay đổi |
|---|---|
| MeetingLifecycle.Transition | Thư ký cũng được bắt đầu cuộc họp (trước chỉ Chủ trì) |
| Meeting.Transition, MeetingOverdue.CancelOverdue | Hủy họp xóa luôn biểu quyết nháp (ADR-0067) |
| SharedCalendar.ReadWeek | Tuần hiển thị Thứ Hai–Chủ nhật, cả ngày |
| Summary.Request | Chỉ tạo được dự thảo khi cuộc họp đã kết thúc; thêm ca "chưa có lời nói" (MSG481) |
| Recording.Download | Thêm ca MP3 không khả dụng (MSG482) |
| SpeechClip.Add | Từ chối trước khi ghi âm, không phát sinh tệp cần xóa |
| VoiceId.Grant | Phiên bản thông báo đồng ý 1.3 → 1.4 (ADR-0071) |
| DocumentFile.Validate | Giới hạn tệp 10 MB, tổng 100 MB; thêm ca PDF có xuống dòng trước chữ ký |
| CallRoom.CloseMeetingRoom, CallRoom.Reconcile | Đóng phòng cũng đóng yêu cầu phát biểu đang mở |
| Minutes (Create, Save, Submit, Approve, Return, Correct) | Thành công kèm thông báo thời gian thực |
| Authentication.SignIn | Thêm 2 ca sai mật khẩu (tài khoản hoạt động và tài khoản đã khóa; cả hai đều trả MSG01 và không lộ trạng thái tài khoản) |
| Password.Verify | Hàm mới vào kế hoạch, 4 UTC; Jwt.AccessToken 5 UTC đã có test |

## 5. Lỗi gặp khi chạy toàn bộ solution

Không có lỗi sản phẩm. Trên máy chạy (Windows), 7 test của `TrustCabinet.Web.Tests` không đạt, đều ngoài phạm vi các UTC trên:

- 4 test `DataRetentionTests`: thiếu cấu hình cơ sở dữ liệu kiểm thử (`TRUSTCABINET_TEST_CONNECTION_STRING` hoặc `POSTGRES_PASSWORD`).
- 3 test `MinutesTextTests`: chuỗi so sánh có ký tự xuống dòng CR do khác biệt CRLF/LF khi checkout trên Windows; cần xác nhận lại trên CI Linux.

## 6. Giới hạn đã biết

- Một số UTC Passed kiểm bằng test double, không dùng cơ sở dữ liệu thật: khi lưu thất bại, test chỉ kiểm kết quả trả về và log, không kiểm trạng thái thực thể (test double thay đổi bộ nhớ trước khi ném lỗi, khác EF thật).
- Dòng Log của `AuditService` được kiểm gián tiếp qua số lần lưu và bản ghi (service không có logger).
- `MeetingDocument.Upload/UTC001`: tên thư mục tải lên được kiểm ở `MeetingDocumentAttacherTests`.
- Tên người dùng trong các assert là giá trị giả lập, chỉ kiểm cấu trúc và nhãn.
- Chưa có UTC: `ParticipantService.ReadAsync` (nhánh người thu giọng), `ReadersAsync(includeFormer=true)`, một số nhánh phụ của biểu quyết.
- Kế hoạch (`functions.json`) vẫn ghim mã nguồn ở commit `7286680a`; các hàm public mới thêm sau đó (lưu giữ dữ liệu, thu giọng theo người, thời gian từng từ…) chưa có sheet.
