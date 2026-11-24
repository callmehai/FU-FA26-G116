# Báo cáo và thống kê — SCRUM-38

UC-79a *View Reports & Statistics* và UC-79b *Export Report*, theo
[đặc tả sơ bộ](../notes/NOTE-dac-ta-so-bo-scrum-38.md). Người dùng xem số liệu tổng hợp về các cuộc họp mình đọc
được trong một khoảng tháng, và tải cùng số liệu đó về dạng Excel.

Từ 23/11/2026 có thêm **truy vấn báo cáo** (`GET /api/reports/query`), theo góp ý của thầy hướng dẫn ngày 22/11/2026:
đưa các số người quản lý quan tâm nhất lên đầu, và cho người xem tự chọn tham số theo từng bảng chính (bảng nào, nhóm
theo gì, lọc gì, lấy bao nhiêu nhóm) để trả lời được câu hỏi tiếp theo, ví dụ "ai chủ trì nhiều nhất, ai thứ hai".

Mã thông báo: `MSG463`–`MSG466`, `MSG484` ([danh mục](00-danh-muc-ma-thong-bao.md)). `MSG466` do FE tự hiện.
Code: [`ReportEndpoints`](../../backend/TrustCabinet.Web/Endpoints/ReportEndpoints.cs),
[`ReportService`](../../backend/TrustCabinet.Infrastructure/Reports/ReportService.cs),
[`MeetingReportXlsx`](../../backend/TrustCabinet.Infrastructure/Reports/MeetingReportXlsx.cs); truy vấn:
[`ReportCatalog`](../../backend/TrustCabinet.Application/Reports/ReportCatalog.cs),
[`ReportFactQuery`](../../backend/TrustCabinet.Infrastructure/Reports/ReportFactQuery.cs),
[`ReportTableXlsx`](../../backend/TrustCabinet.Infrastructure/Reports/ReportTableXlsx.cs). FE:
[`ReportsPage`](../../frontend/src/features/reports/ReportsPage.tsx), kiểu [`types/report.ts`](../../frontend/src/types/report.ts),
gọi API ở [`api/reports.ts`](../../frontend/src/api/reports.ts).

## Quyền

| Ai | Kết quả |
|---|---|
| Chưa đăng nhập | `401` |
| Tài khoản đã bị vô hiệu hoá, hoặc token Khách | `403`, không mã (như mọi endpoint `RequireActiveUser`) |
| Mọi người dùng nội bộ đang hoạt động, kể cả Quản trị | `200`. Không cần capability (BR-149, BR-150) |

- **Chỉ đếm cuộc họp người xem đọc được:** server lọc bằng `MeetingReadPolicy.ForUser` ngay trong câu truy vấn, trước khi
  đếm. Đây là cùng luật với danh sách cuộc họp: người tạo, người duyệt được giao, Chủ toạ/Thư ký hiện tại, thành phần hiện
  tại từ khi `Published`.
- **Quản trị không thấy toàn hệ thống** (D2 trong note, PO chốt Q1 ngày 09/11/2026). Quản trị chỉ thấy số liệu của cuộc họp
  mình đọc được, như mọi người khác.
- **Không có capability riêng** (PO chốt Q3 ngày 09/11/2026): ai đăng nhập bằng tài khoản nội bộ cũng xem và xuất được.
- **Không lộ cuộc họp (BR-152):** body và tệp của `GET /api/reports/meetings` chỉ có số đếm, tỷ lệ và trung bình. Không có
  `id`, tiêu đề, người dự hay nội dung cuộc họp.
- **Truy vấn báo cáo (đổi BR-152, góp ý 22/11/2026):** khi nhóm theo người (Chủ toạ, người tạo, người dự, người đưa tài
  liệu lên, người lập biên bản) hoặc theo từng cuộc họp, mỗi dòng mang tên người hoặc `id` và tiêu đề cuộc họp. Đó chỉ là
  những cuộc họp người xem đã đọc được, và những người họ đã thấy trong các cuộc họp đó, nên không lộ gì hơn danh sách cuộc
  họp. Không bao giờ có nội dung cuộc họp, và Khách chỉ có tên, không có email (PO 01/11/2026).
- **Thấy gì trong từng bảng, như trên các tab của cuộc họp:**
  - biểu quyết `Draft` chỉ được đếm cho Chủ toạ/Thư ký đang giữ vai trò của cuộc họp đó;
  - biên bản chưa ban hành (`Draft`, `WaitingApproval`, `Rejected`) chỉ được đếm cho Chủ toạ/Thư ký (BR-139);
  - bản ghi chỉ được đếm cho người đang là thành phần của cuộc họp (`RecordingRules.CanListen`): người duyệt hay người tạo
    không có tên trong thành phần thì không thấy; bản ghi đã xoá không bao giờ được đếm.

## Endpoint

| Method | Đường dẫn | Kết quả |
|---|---|---|
| `GET` | `/api/reports/meetings?from=2026-01&to=2026-12` | `200` `MeetingReport`; `400` `MSG463`; `500` `MSG464` |
| `GET` | `/api/reports/meetings/export?from=2026-01&to=2026-12` | `200` tệp `.xlsx`; `400` `MSG463`; `500` `MSG465` |
| `GET` | `/api/reports/query?entity=votes&from=2026-09&to=2026-11&groupBy=conclusion&sort=count&top=10&status=Ended` | `200` `ReportTable`; `400` `MSG463`, `MSG484`; `500` `MSG464` |
| `GET` | `/api/reports/query/export?…` (cùng tham số) | `200` tệp `.xlsx`; `400` `MSG463`, `MSG484`; `500` `MSG465` |

Cả hai trả header `Cache-Control: private, no-store`.

### Khoảng thời gian (UC-79a)

- `from`, `to` có dạng `yyyy-MM`, tính theo giờ Việt Nam (`+07:00`, không có giờ mùa hè).
- Bỏ cả hai thì server lấy 12 tháng, kết thúc ở tháng hiện tại theo giờ Việt Nam.
- Server trả `400` `MSG463` khi:
  - chỉ gửi một trong hai tham số;
  - tham số sai dạng, ví dụ `2026-1`, `2026-13`, hoặc năm ngoài khoảng 2000–9998;
  - tháng bắt đầu sau tháng kết thúc;
  - khoảng thời gian dài quá 24 tháng.
- Cuộc họp thuộc tháng chứa **giờ bắt đầu dự kiến** của nó (BR-149, BR-150). Cuộc họp đã huỷ vẫn được đếm.

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
  "minutesTurnaround": { "averageHours": 30.5, "meetings": 0, "meetingsWithoutIssuedMinutes": 20 },
  "votes": { "closed": 50, "passed": 26, "notPassed": 10, "deferred": 4 },
  "minutes": { "issued": 18, "waitingApproval": 5 },
  "recordings": { "count": 59, "recordedHours": 24.6, "transcribedHours": 18.1 }
}
```

- **`votes`, `minutes`, `recordings`** (góp ý 22/11/2026, số người quản lý đọc đầu tiên): tính trên đúng những gì người
  xem thấy, theo các luật ở mục Quyền.
  - `votes`: số biểu quyết đã đóng, và trong đó bao nhiêu thông qua, không thông qua, hoãn.
  - `minutes.issued`: số cuộc họp đã ban hành biên bản ít nhất một lần. `minutes.waitingApproval`: số bản đang chờ duyệt,
    chỉ của các cuộc họp người xem là Chủ toạ/Thư ký.
  - `recordings`: số bản ghi chưa xoá; `recordedHours` là giờ nghe được (`Available`), không tính bản tổng hợp (`Combined`)
    vì nó trộn lại các bản khác (ADR-0061); `transcribedHours` là giờ của các bản đã gỡ băng xong. Làm tròn 1 chữ số.

- **`attendance` (BR-151):** chỉ tính cuộc họp `Ended`.
  - Tỷ lệ của một cuộc họp = số người có mặt (`Present`) / (thành phần nội bộ hiện tại + Khách chưa bị thu hồi lời mời).
  - Cuộc họp chưa ai được điểm danh `Present` hoặc `Absent` thì không tính, và được đếm vào `meetingsWithoutAttendance`.
  - `averageRate` là trung bình các tỷ lệ, dạng phân số (`0.87` là 87 %), làm tròn 4 chữ số. Bằng `null` khi không có
    cuộc họp nào để tính.
- **`duration` (PO chốt Q2 ngày 09/11/2026, BR-151 đề xuất):** chỉ tính cuộc họp `Ended`.
  - Thời lượng của một cuộc họp = từ `actualStartedAt` tới `actualEndedAt`.
  - Cuộc họp thiếu một trong hai mốc thì không tính, và được đếm vào `meetingsWithoutTimes`.
  - `averageMinutes` làm tròn 1 chữ số. Bằng `null` khi không có cuộc họp nào để tính.
- **`minutesTurnaround` (BR-151):** chỉ tính cuộc họp `Ended`.
  - Thời gian của một cuộc họp = từ `actualEndedAt` tới lần **ban hành đầu tiên** của biên bản (`IssuedAt` nhỏ nhất).
    Bản đính chính ban hành sau không tính.
  - Cuộc họp chưa có biên bản ban hành thì không tính, và được đếm vào `meetingsWithoutIssuedMinutes`.
  - `averageHours` làm tròn 2 chữ số. Bằng `null` khi không có cuộc họp nào để tính. Mốc ban hành là
    `IssuedAt` do luồng duyệt và ban hành biên bản (SCRUM-33, đã vào `main`) ghi; bản bị thay (`Superseded`) vẫn giữ mốc
    ban hành đầu.
- **`total = 0`:** FE hiện `MSG466` (UC-79a AF-2). Server vẫn trả `200`, không có mã.
- Server đếm bằng một `GROUP BY` (tháng theo giờ Việt Nam, trạng thái, mức mật) trên database. Hai chỉ số trung bình
  được tính từ các con số của từng cuộc họp `Ended`. Không con số nào mang id hay tiêu đề.

### Tệp Excel (UC-79b, BR-010)

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

## Truy vấn báo cáo — `GET /api/reports/query`

Mọi tham số là chuỗi trên query string. Bỏ trống một tham số lọc thì không lọc theo nó.

| Tham số | Giá trị | Mặc định |
|---|---|---|
| `entity` | `meetings`, `participants`, `votes`, `documents`, `minutes`, `recordings` | `meetings` |
| `from`, `to` | như UC-79a ở trên | 12 tháng tới tháng này |
| `groupBy` | một cột nhóm của bảng (bảng dưới) | cột đầu của bảng |
| `sort` | `group` (thứ tự của nhóm: tháng theo thời gian, trạng thái theo thứ tự liệt kê, tên theo ABC, cuộc họp theo giờ bắt đầu dự kiến) hoặc khoá một số liệu của bảng, lớn trước | `group` khi nhóm theo tháng, còn lại số liệu đầu |
| `top` | 1–500: chỉ giữ chừng ấy nhóm đầu sau khi sắp xếp | mọi nhóm |
| `status`, `mode`, `sensitivity` | lọc theo trạng thái, hình thức, mức độ của cuộc họp (mọi bảng) | — |
| `role`, `attendance`, `response` | lọc người dự họp (chỉ `participants`) | — |
| `conclusion`, `voteStatus`, `ballot` | lọc biểu quyết (chỉ `votes`) | — |
| `fileType` | `pdf`, `word`, `excel`, `powerpoint`, `image`, `other` (chỉ `documents`) | — |
| `minutesStatus` | lọc biên bản (chỉ `minutes`) | — |
| `source`, `transcript` | lọc bản ghi (chỉ `recordings`) | — |

Một dòng của mỗi bảng thuộc tháng chứa **giờ bắt đầu dự kiến của cuộc họp** của nó, như BR-149, BR-150. Mọi bảng nhóm được theo
`month`, `meeting` (từng cuộc họp), `status`, `mode`, `sensitivity`.

| `entity` | Một dòng là | Cột nhóm riêng | Số liệu (`columns`) |
|---|---|---|---|
| `meetings` (Cuộc họp) | một cuộc họp | `chair` (Chủ toạ hiện tại, `None` khi chưa có), `creator` | `count`; `ended` (đã kết thúc); `cancelled`; `hours` (tổng giờ họp thực tế của cuộc họp đã kết thúc); `attendance` (tỷ lệ có mặt trung bình, BR-151) |
| `participants` (Người dự họp) | một thành phần hiện tại hoặc một Khách còn lời mời | `person` (id người dùng, hoặc `guest:<id lời mời>`), `role`, `attendance`, `response` | `count` (lượt mời); `present`; `absent` (vắng, kể cả vắng có phép); `rate` (có mặt / số lần được điểm danh) |
| `votes` (Biểu quyết) | một biểu quyết | `conclusion` (`None` khi chưa kết luận), `voteStatus`, `ballot` (`public`, `anonymous`) | `count`; `passed`; `notPassed`; `turnout` (phiếu đã bỏ / số người được bỏ phiếu, trên biểu quyết đã mở) |
| `documents` (Tài liệu) | một tài liệu đang gắn với cuộc họp | `uploader`, `fileType` (theo đuôi tên tệp) | `count`; `pages`; `size` (MB); `comments`; `reads` (lượt xác nhận đã đọc) |
| `minutes` (Biên bản) | một bản biên bản | `minutesStatus`, `author` | `count`; `issued`; `waiting`; `returned`; `turnaround` (giờ từ khi họp xong tới lần ban hành đầu, trung bình, BR-151) |
| `recordings` (Bản ghi) | một bản ghi chưa xoá | `source`, `transcript` (`None` khi chưa gỡ băng) | `count`; `hours` (giờ nghe được, không tính bản tổng hợp); `transcribed` |

### `ReportTable`

```jsonc
{
  "entity": "participants", "entityLabel": "Người dự họp",
  "groupBy": "person", "groupLabel": "Người dự",
  "sort": "absent", "top": 5,
  "from": "2026-09", "to": "2026-11", "generatedAt": "2026-11-23T01:34:00+07:00",
  "filters": [{ "key": "status", "label": "Trạng thái cuộc họp", "value": "Ended", "valueLabel": "Đã kết thúc" }],
  "columns": [{ "key": "count", "label": "Lượt mời", "format": "count" }, { "key": "rate", "label": "Tỷ lệ có mặt", "format": "percent" }],
  "groups": 84,                                  // số nhóm trước khi cắt theo top
  "total": { "count": 4885, "rate": 0.7861 },     // trên mọi dòng qua bộ lọc, không chỉ các nhóm hiện ra
  "rows": [{ "rank": 1, "key": "01a1…", "label": "Lê An", "values": { "count": 30, "rate": 0.6667 } }]
}
```

- `format` là `count`, `percent` (phân số, làm tròn 4 chữ số), `hours` hoặc `megabytes` (làm tròn 1 chữ số). Một số liệu
  không có gì để tính trung bình thì là `null`.
- Nhóm theo tháng thì có đủ mọi tháng của khoảng, kể cả tháng 0. Các cách nhóm khác chỉ có nhóm có dữ liệu.
- Khi hai nhóm bằng nhau, nhóm đứng trước theo thứ tự của nhóm.
- Server đọc các dòng của bảng (đã lọc theo quyền đọc cuộc họp ngay trong câu truy vấn), rồi lọc theo quyền từng bảng, nhóm,
  sắp xếp và cắt ở tầng Application. Không có bảng mới, không có migration.

### Tệp Excel của truy vấn

- Tên tệp: `bao-cao-<bảng>_<from>_<to>.xlsx`, với `<bảng>` là `cuoc-hop`, `thanh-phan`, `bieu-quyet`, `tai-lieu`, `bien-ban`,
  `ban-ghi`. Một sheet, đặt tên theo bảng.
- Đầu sheet: khoảng thời gian, nhóm theo, bộ lọc (bằng chữ), thời điểm xuất. Sau đó là bảng: `#`, nhóm, từng số liệu, và
  dòng *Tổng*. Số vẫn là số; tỷ lệ giữ định dạng phần trăm.
- **Audit:** như UC-79b, mỗi lần xuất ghi một dòng `report.exported` với `details = { report: "<entity>", groupBy, from, to,
  filters: "status=Ended, …", rows }`. Không ghi được audit thì không trả tệp (`MSG465`).

## Lỗi

| Mã | HTTP | Khi nào |
|---|---|---|
| `MSG463` | 400 | Khoảng thời gian không hợp lệ (xem trên). Không tính gì, không ghi audit |
| `MSG464` | 500 | `GET /api/reports/meetings`: lỗi database khi đọc |
| `MSG465` | 500 | `GET …/export`: lỗi database khi đọc, dựng tệp lỗi, hoặc không ghi được audit. Không trả tệp |
| `MSG484` | 400 | `GET /api/reports/query` và `…/export`: `entity`, `groupBy`, `sort`, `top` hoặc giá trị một bộ lọc mà bảng đó không có. Khoảng thời gian sai thì báo `MSG463` trước. Không đọc gì, không ghi audit |

## Kiểm thử

[`ReportEndpointTests`](../../tests/TrustCabinet.Web.Tests/Reports/ReportEndpointTests.cs) và
[`ReportQueryEndpointTests`](../../tests/TrustCabinet.Web.Tests/Reports/ReportQueryEndpointTests.cs) chạy trên PostgreSQL
thật; [`ReportQueryServiceTests`](../../tests/TrustCabinet.Application.Tests/Reports/ReportQueryServiceTests.cs) thử
nhóm, sắp xếp, cắt, lọc và quyền từng bảng không cần database.
Các test phủ:

- đếm theo tháng, trạng thái và mức mật; ranh giới tháng theo giờ Việt Nam;
- lọc theo quyền đọc, kể cả Quản trị;
- ba chỉ số trung bình: thành phần đã bị gỡ, Khách bị thu hồi lời mời, thời lượng, bản đính chính;
- khoảng mặc định và giới hạn 24 tháng;
- `MSG463`–`MSG465`;
- nội dung tệp `.xlsx`, dòng audit, và trường hợp không ghi được audit thì không trả tệp;
- `401` và `403`;
- truy vấn: chỉ cuộc họp đọc được, tên khi nhóm theo cuộc họp, biểu quyết nháp, biên bản chưa ban hành, bản ghi của người
  không có tên trong thành phần, `MSG484`, `MSG463` trước `MSG484`, nội dung tệp và dòng audit.

## Hợp đồng tích hợp FE

- **Màn:** mục *Báo cáo* trên sidebar, cho mọi người dùng nội bộ (không cần capability), route `/mgmt/reports`.
  Khoảng thời gian và truy vấn nằm trên URL: `?from=yyyy-MM&to=yyyy-MM&entity=…&groupBy=…&sort=…&top=…&<lọc>=…`, chỉ ghi
  những gì khác mặc định. Đổi bất kỳ ô nào thì thay URL (`replace`), nên tải lại trang hoặc gửi link vẫn mở đúng câu trả
  lời. Giá trị lạ trên URL bị bỏ qua, trang dùng mặc định.
- **Khoảng mặc định:** FE tự tính 12 tháng tới tháng hiện tại theo giờ Việt Nam (`Asia/Ho_Chi_Minh`), như server.
- **Dải số liệu chính** (từ `GET /api/reports/meetings`), một hàng số trong một thẻ, không icon, không ô KPI: Đã họp
  (`Ended`), Sắp họp (`Approved` + `Published`), Đã huỷ, Tỷ lệ có mặt, Thông qua / số biểu quyết đã đóng, Biên bản ban
  hành (kèm số chờ duyệt nếu có), Giờ ghi âm (kèm giờ đã gỡ băng).
- **Truy vấn** (từ `GET /api/reports/query`): các ô chọn *Dữ liệu* (bảng), *Nhóm theo*, *Hiển thị* (tất cả hoặc 5/10/20/50
  nhóm đầu), rồi các bộ lọc của cuộc họp (trạng thái, hình thức, mức độ) và bộ lọc riêng của bảng. Nút *Xoá lọc* khi có
  lọc. Đổi bảng thì giữ các bộ lọc của cuộc họp, về cách nhóm mặc định. Nhóm theo người hoặc cuộc họp thì mặc định 10 nhóm
  đầu; nhóm theo từng cuộc họp thì mặc định xếp theo giờ họp.
- **Bảng kết quả:** cột `#`, nhóm, mọi số liệu, và một thanh ngang tỉ lệ theo số liệu đang sắp xếp. Bấm tiêu đề một số liệu
  để xếp theo nó (lớn trước), bấm tiêu đề nhóm để về thứ tự của nhóm. Dòng *Tổng* ở cuối; khi bị cắt thì có dòng
  "5 / 84 nhóm". Nhóm theo cuộc họp thì tên là link mở cuộc họp; trạng thái và mức độ hiện bằng `StatusBadge`/`SensitivityBadge`.
  Trên điện thoại mỗi dòng thành một khối: hạng và nhóm ở dòng đầu, các số liệu kèm tên ở dưới.
- **`MSG463`:** FE kiểm tra trước, cùng luật UC-79a và cùng câu thông báo. Khoảng sai thì hiện lỗi dưới thanh chọn tháng,
  không gọi API, và khoá *Xuất Excel*. Nếu server vẫn trả `MSG463`, FE hiện câu của server ở cùng chỗ.
- **`MSG464`:** thông báo của server kèm nút *Thử lại*, trong thẻ truy vấn (hoặc thay dải số liệu khi đó là lỗi của
  `GET /api/reports/meetings`). **`MSG465`:** toast. **`MSG466`:** FE tự hiện khi không đọc được cuộc họp nào trong khoảng.
  Lọc ra không còn gì thì "Không có dữ liệu khớp bộ lọc." kèm *Xoá bộ lọc*.
- **Đang tải lại:** bảng cũ vẫn hiện, mờ đi (`aria-busy`). Đổi bảng hoặc cách nhóm thì hiện khung chờ.
- **Xuất Excel** xuất đúng bảng đang xem (`GET /api/reports/query/export`), tên tệp như server đặt. `GET
  /api/reports/meetings/export` vẫn còn, màn không gọi nữa.
