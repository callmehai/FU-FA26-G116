# Báo cáo và thống kê — SCRUM-38

UC-70a *View Reports & Statistics* và UC-70b *Export Report*, theo
[đặc tả sơ bộ](../notes/NOTE-dac-ta-so-bo-scrum-38.md). Người dùng xem số liệu tổng hợp về các cuộc họp mình đọc
được trong một khoảng tháng, và tải cùng số liệu đó về dạng Excel.

Mã thông báo: `MSG463`–`MSG466` ([danh mục](00-danh-muc-ma-thong-bao.md)). `MSG466` do FE tự hiện.
Code: [`ReportEndpoints`](../../backend/TrustCabinet.Web/Endpoints/ReportEndpoints.cs),
[`ReportService`](../../backend/TrustCabinet.Infrastructure/Reports/ReportService.cs),
[`MeetingReportXlsx`](../../backend/TrustCabinet.Infrastructure/Reports/MeetingReportXlsx.cs). FE chưa làm.

## Quyền

| Ai | Kết quả |
|---|---|
| Chưa đăng nhập | `401` |
| Tài khoản đã bị vô hiệu hoá, hoặc token Khách | `403`, không mã (như mọi endpoint `RequireActiveUser`) |
| Mọi người dùng nội bộ đang hoạt động, kể cả Quản trị | `200`. Không cần capability (BR-432) |

- **Chỉ đếm cuộc họp người xem đọc được:** server lọc bằng `MeetingReadPolicy.ForUser` ngay trong câu truy vấn, trước khi
  đếm. Đây là cùng luật với danh sách cuộc họp: người tạo, người duyệt được giao, Chủ toạ/Thư ký hiện tại, thành phần hiện
  tại từ khi `Published`.
- **Quản trị không thấy toàn hệ thống** (D2 trong note, PO chốt Q1 ngày 09/11/2026). Quản trị chỉ thấy số liệu của cuộc họp
  mình đọc được, như mọi người khác.
- **Không có capability riêng** (PO chốt Q3 ngày 09/11/2026): ai đăng nhập bằng tài khoản nội bộ cũng xem và xuất được.
- **Không lộ cuộc họp (BR-437):** body và tệp chỉ có số đếm, tỷ lệ và trung bình. Không có `id`, tiêu đề, người dự hay
  nội dung cuộc họp.

## Endpoint

| Method | Đường dẫn | Kết quả |
|---|---|---|
| `GET` | `/api/reports/meetings?from=2026-01&to=2026-12` | `200` `MeetingReport`; `400` `MSG463`; `500` `MSG464` |
| `GET` | `/api/reports/meetings/export?from=2026-01&to=2026-12` | `200` tệp `.xlsx`; `400` `MSG463`; `500` `MSG465` |

Cả hai trả header `Cache-Control: private, no-store`.

### Khoảng thời gian (BR-433)

- `from`, `to` có dạng `yyyy-MM`, tính theo giờ Việt Nam (`+07:00`, không có giờ mùa hè).
- Bỏ cả hai thì server lấy 12 tháng, kết thúc ở tháng hiện tại theo giờ Việt Nam.
- Server trả `400` `MSG463` khi:
  - chỉ gửi một trong hai tham số;
  - tham số sai dạng, ví dụ `2026-1`, `2026-13`, hoặc năm ngoài khoảng 2000–9998;
  - tháng bắt đầu sau tháng kết thúc;
  - khoảng thời gian dài quá 24 tháng.
- Cuộc họp thuộc tháng chứa **giờ bắt đầu dự kiến** của nó (BR-434). Cuộc họp đã huỷ vẫn được đếm.

### `MeetingReport`

```jsonc
{
  "from": "2026-01",
  "to": "2026-12",
  "generatedAt": "2026-11-09T17:00:00+07:00",   // giờ Việt Nam
  "total": 42,
  "byMonth": [{ "month": "2026-01", "count": 3 }],         // đủ mọi tháng trong khoảng, kể cả tháng 0
  "byStatus": [{ "status": "Draft", "count": 2 }],         // đủ 8 trạng thái, theo thứ tự enum MeetingStatus
  "bySensitivity": [{ "sensitivity": "Normal", "count": 30 }], // đủ Normal, Internal, Secret
  "attendance": { "averageRate": 0.87, "meetings": 18, "meetingsWithoutAttendance": 2 },
  "duration": { "averageMinutes": 95.5, "meetings": 20, "meetingsWithoutTimes": 0 },
  "minutesTurnaround": { "averageHours": 30.5, "meetings": 0, "meetingsWithoutIssuedMinutes": 20 }
}
```

- **`attendance` (BR-435):** chỉ tính cuộc họp `Ended`.
  - Tỷ lệ của một cuộc họp = số người có mặt (`Present`) / (thành phần nội bộ hiện tại + Khách chưa bị thu hồi lời mời).
  - Cuộc họp chưa ai được điểm danh `Present` hoặc `Absent` thì không tính, và được đếm vào `meetingsWithoutAttendance`.
  - `averageRate` là trung bình các tỷ lệ, dạng phân số (`0.87` là 87 %), làm tròn 4 chữ số. Bằng `null` khi không có
    cuộc họp nào để tính.
- **`duration` (PO chốt Q2 ngày 09/11/2026, BR-447 đề xuất):** chỉ tính cuộc họp `Ended`.
  - Thời lượng của một cuộc họp = từ `actualStartedAt` tới `actualEndedAt`.
  - Cuộc họp thiếu một trong hai mốc thì không tính, và được đếm vào `meetingsWithoutTimes`.
  - `averageMinutes` làm tròn 1 chữ số. Bằng `null` khi không có cuộc họp nào để tính.
- **`minutesTurnaround` (BR-436):** chỉ tính cuộc họp `Ended`.
  - Thời gian của một cuộc họp = từ `actualEndedAt` tới lần **ban hành đầu tiên** của biên bản (`IssuedAt` nhỏ nhất).
    Bản đính chính ban hành sau không tính.
  - Cuộc họp chưa có biên bản ban hành thì không tính, và được đếm vào `meetingsWithoutIssuedMinutes`.
  - `averageHours` làm tròn 2 chữ số. Bằng `null` khi không có cuộc họp nào để tính. Mốc ban hành là
    `IssuedAt` do luồng duyệt và ban hành biên bản (SCRUM-33, đã vào `main`) ghi; bản bị thay (`Superseded`) vẫn giữ mốc
    ban hành đầu.
- **`total = 0`:** FE hiện `MSG466` (UC-70a AF-2). Server vẫn trả `200`, không có mã.
- Server đếm bằng một `GROUP BY` (tháng theo giờ Việt Nam, trạng thái, mức mật) trên database. Hai chỉ số trung bình
  được tính từ các con số của từng cuộc họp `Ended`. Không con số nào mang id hay tiêu đề.

### Tệp Excel (UC-70b, BR-438)

- Tên tệp là `bao-cao-cuoc-hop_<from>_<to>.xlsx`. Content type là
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.
- Tệp có một sheet *Báo cáo cuộc họp*:
  - đầu sheet: khoảng thời gian, thời điểm xuất (GMT+7) và tổng số cuộc họp;
  - ba bảng: theo tháng, theo trạng thái, theo mức độ mật. Nhãn dùng tiếng Việt, như trên giao diện;
  - ba dòng trung bình (tỷ lệ tham dự, thời lượng họp tính bằng phút, thời gian tới khi ban hành biên bản), mỗi dòng kèm số cuộc họp được tính và số cuộc họp chưa có dữ liệu. Chỉ số chưa có dữ liệu thì
    ghi `Chưa có dữ liệu`.
- Số giữ nguyên là số, nên người dùng tính tiếp được trong Excel.
- Tệp được dựng tay bằng `ZipArchive`, như `MinutesDocx`. Không thêm package.
- Không có bản PDF (PO chốt Q2 ngày 09/11/2026).
- Số liệu được tính lại lúc xuất, bằng cùng code với `GET /api/reports/meetings`.
- **Audit:** mỗi lần xuất ghi một dòng `report.exported`, `objectType = "Report"`, `objectId = null`, `details = { report:
  "meetings", from, to }`, kèm người xuất, IP và thời điểm.
  - Không ghi được audit thì server không trả tệp: `500` `MSG465`.
  - Xem báo cáo trên màn hình không ghi audit.

## Lỗi

| Mã | HTTP | Khi nào |
|---|---|---|
| `MSG463` | 400 | Khoảng thời gian không hợp lệ (xem trên). Không tính gì, không ghi audit |
| `MSG464` | 500 | `GET /api/reports/meetings`: lỗi database khi đọc |
| `MSG465` | 500 | `GET …/export`: lỗi database khi đọc, dựng tệp lỗi, hoặc không ghi được audit. Không trả tệp |

## Kiểm thử

[`ReportEndpointTests`](../../tests/TrustCabinet.Web.Tests/Reports/ReportEndpointTests.cs) chạy trên PostgreSQL thật.
Các test phủ:

- đếm theo tháng, trạng thái và mức mật; ranh giới tháng theo giờ Việt Nam;
- lọc theo quyền đọc, kể cả Quản trị;
- ba chỉ số trung bình: thành phần đã bị gỡ, Khách bị thu hồi lời mời, thời lượng, bản đính chính;
- khoảng mặc định và giới hạn 24 tháng;
- `MSG463`–`MSG465`;
- nội dung tệp `.xlsx`, dòng audit, và trường hợp không ghi được audit thì không trả tệp;
- `401` và `403`.
