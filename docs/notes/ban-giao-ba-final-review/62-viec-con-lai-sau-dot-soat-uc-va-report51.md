# Note 62 — việc còn lại cho BA/PO sau đợt soát UC (note 60/61) và khôi phục Report 5.1 (25/11/2026)

Nhánh: `docs/note60-61-agent` @ `7a54215b`, cộng thay đổi chưa commit khôi phục Report 5.1 (xlsx + `functions.json` + 2 JSON + 2 preview).
Agent đã xong note 60 §5 bước 1–4 và note 61 §3–§4 trên DOCX nguồn; Markdown snapshot sinh lại bằng `sync_reports.py --report 3`. Không push Drive.

## 1. Đã xong đợt này, BA/PO khỏi tìm lại

- UC-52b bỏ, gộp tắt mic/lưu lượt vào UC-52a (đổi tên Turn Room Microphone On/Off, thêm EX-4 MSG360, AF họp kết hợp ADR-0085).
- UC-54c bỏ, thành AF-2/AF-3 của UC-54a (Thư ký chạy gỡ băng: chưa có/Failed/Completed, MSG300/301/479).
- Style 131 bảng UC: `EX-n – Tên`, viết như câu, bước chữ thường, câu kết hai dạng, `Normal Flow`, MSG lần đầu kèm nội dung. `uc_spec_style_report3.py` không tham số: sạch. `check_docx.py`: OK.
- Report 5.1: đã khôi phục 2 sheet `RoomTake.Complete` (`RoomTakeService.CompleteAsync`) và `Transcription.ProcessNext` (`TranscriptionService.ProcessNextAsync`); 147 → 149 sheet, 596 → 604 UTC; `RetryAsync`/`RequeueInterruptedAsync`/`CompleteIdleAsync` về `Covered indirectly via …`. Quyết định bỏ 2 case ở note 60 §6.1 hết hiệu lực.

## 2. BA chốt kỹ thuật (đọc code rồi sửa DOCX nguồn, rồi sync)

BR (`06-business-rules.md` + code): BR-015 (reschedule Published, bỏ AF tự hủy UC-22), BR-019 (1 Approver/vòng, EX-4 UC-17), BR-069 (PDF/DOCX — UC-39 EX-1 theo code: file lẻ lỗi nêu tên, file đúng vẫn lên), BR-085 (UC-37a gọt read-only), BR-091 (chốt voter lúc mở vote — UC-63/64), BR-098 (chỉ Chair đóng/chốt vote), BR-110 (UC-52e bỏ vế hệ thống tự xóa, giữ luật 180 ngày), BR-113 (câu mic riêng, bỏ câu lọc ồn sang BR), BR-146/147 (bận/rảnh — UC-81a sang BR-144→147). Thêm 1 BR mới hạn mức 250 GB (`FileStorageOptions`, PR #529), sửa UC-36a/b + UC-39 EX-4.

UC (`05-use-case-specifications.md`, số mới): UC-04 (thiếu `●` POST, `job title` → chức danh), UC-09 (bỏ câu API/paging), UC-11c/UC-22 (bỏ dialog/AF tự hủy), UC-15 (AF lịch sử + EX-2 MSG136 đã thêm — kiểm phân quyền xem lý do từ chối), UC-16 (4 mốc thời gian + sort), UC-24 (bỏ mã HTTP/tên trường), UC-36a/b (MSG288 10 file/100 MB + MSG346 đối chiếu `useFileUpload.ts` + `DocumentFileRules`), UC-63 (bỏ lặp Không ý kiến, chip/toast), UC-64 (gọt kiểm đếm), UC-70/68a (4 bước kỹ thuật sang Report 4), UC-80f (bỏ chip/màu sang màn hình/BR).

MSG: MSG334/337 (FE chặn mic/camera — thống nhất câu tiếng Việt catalogue), MSG461/462 (UC-50 — catalogue 500/503 lỗi DB, Report 3 EX-5/EX-6 — thống nhất số EX + ngữ cảnh).

Thiết kế theo code: §3.2 bổ sung `AiProviderSetting`, `CalendarShare`; Report 4 §3.26 bổ sung `WordStartsMs`, `WordConfidence`. Họp kết hợp: Thư ký rời phòng online mà mic phòng vẫn ghi — giữ theo ADR-0085 (`sendsRoomMic`, một trạng thái), ghi rõ vào UC-52a AF-1.

## 3. PO quyết

1. UC-82 cấu hình AI (`GET/PUT /api/admin/ai`, MSG408–413 đã code, Report 3 chưa có UC): thêm UC ở FE-03 hay để cấu hình không UC.
2. Actor `Resend Email` ngoài hệ thống (cùng OAuth, AI Drafting Service): giữ hay bỏ.
3. Hạn mức 250 GB toàn hệ thống: chốt số, cho BA thêm BR.
4. Câu chữ MSG334/337/461/462 tiếng Việt cuối cùng.
5. Màn 9.1, 9.2, 10.1, 5.2 + screen flow còn ảnh cũ: có chụp lại theo wireframe v7 không.

## 4. Sửa tài liệu thuần túy (không cần quyết)

- Report 4 I.1 + Figure 1 còn `Cloudflare R2` → `SeaweedFS` (ADR-0084 đã chốt).
- Tiêu đề phần II Report 4 còn số UC cũ; QA plan + báo cáo round 1 còn nhắc UC-52b/UC-54c → thay bằng UC-52a AF / UC-54a AF-2/3.
- 28 tab drawio hiện `title UC-…` do file `.txt` nguồn có BOM: save UTF-8 không BOM, xuất lại PNG.

Quy tắc: sửa DOCX ở `docs/08-reports/source/`, rồi `sync_reports.py --report 3`; không sửa tay snapshot (`Report-*.md`, `05-use-case-specifications.md`, `06-business-rules.md`); không push Drive khi chưa được yêu cầu.
