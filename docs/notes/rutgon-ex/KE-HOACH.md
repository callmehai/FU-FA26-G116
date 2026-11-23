# Kế hoạch rút gọn Exception Flow và Sequence Diagram

Tài liệu cho agent. Đọc hết trước khi làm. Chủ sở hữu: BA (Phạm Gia Khánh).

## 0. Mục tiêu

Sequence diagram và UC Specification đang phình vì quá nhiều nhánh lỗi (trung bình 4,2 EX mỗi UC,
29/73 UC có từ 5 EX; 49 diagram dài hơn 100 dòng). Cần rút xuống **tối đa 3 EX nghiệp vụ mỗi UC**,
diagram gọn như mẫu nhóm G158, và **UC Spec khớp sequence từng EX**.

Mẫu chuẩn đã làm xong: `docs/06-sequencediagram/document-management/UC32a_ViewMeetingDocuments.txt`
(71 → 56 dòng, 1 EX nghiệp vụ, message ngắn, không tự gọi). Bắt chước mẫu này.

## 1. Quy tắc (bắt buộc)

1. **Tối đa 3 EX nghiệp vụ mỗi UC.** Nghiệp vụ là: sai quyền, trạng thái không cho phép, dữ liệu
   không hợp lệ, vi phạm BR. Ưu tiên giữ lỗi có BR đi kèm.
2. **Bỏ hẳn lỗi hạ tầng** ở cả sequence lẫn spec: không lưu được, không tải được, dịch vụ không phản
   hồi, "thử lại" (ví dụ MSG304, 315, 298, 404, 447, 453, 95, 176, 297, 299, 392, 393, 157).
   Không vẽ nhánh `Save failed` / `Load failed` / `Read failure`.
3. **Gộp EX cùng MSG hoặc cùng tình huống** thành một.
4. **Message ngắn** (dưới khoảng 80 ký tự), **không** ghi `BR-xx` hay `UC-xx` trong câu message.
5. **Không có bước tự gọi** (`A->A`). Logic nội bộ gộp vào mũi tên gọi hoặc bỏ.
6. Mũi tên trả lỗi **vẫn đi qua từng tầng** như cũ (Service → Controller → View → Actor). Không gộp
   thành một mũi tên.
7. Giữ lifeline `Database`. Chỉ bỏ các lần đọc lặp không cần thiết.
8. Nhãn `- EX-n` trong sequence **đánh lại liền mạch** và bằng đúng số EX trong spec patch.
9. AF là thao tác giao diện thuần (đổi thứ tự hiển thị, ẩn nút) thì bỏ khỏi sequence.
10. Không đổi MSG, BR trong danh mục. Chỉ gộp hoặc bỏ EX trong tài liệu.

**Không chắc thì không đoán.** UC còn hơn 3 EX nghiệp vụ, hoặc EX khó phân loại (hạ tầng hay
nghiệp vụ, gộp có mất nghĩa không), ghi vào `<nhom>-ba-chon.md` rồi bỏ qua UC đó.

## 2. Cấu trúc nhánh

```
docs/rutgon-ex-base                (script + UC-32a mẫu, đã có)
  ├─ docs/rutgon-ex-tai-lieu       document-management
  ├─ docs/rutgon-ex-go-bang        transcription-voice-id + meeting-recording
  ├─ docs/rutgon-ex-hop            meeting-management + agenda-management
  │                                + meeting-participant-management + online-meeting-room
  ├─ docs/rutgon-ex-lich-phieu     calendar-sharing + voting-management + minutes-management
  └─ docs/rutgon-ex-con-lai        auth + user-management + role-management
                                   + notification-management + reporting
```

Mỗi nhánh con tách từ `docs/rutgon-ex-base`. Mỗi agent làm đúng một nhánh, trong worktree riêng:

```powershell
git worktree add ../TrustCabinet-<nhom> -b docs/rutgon-ex-<nhom> docs/rutgon-ex-base
```

## 3. Việc trong mỗi nhánh con

Chỉ được đụng: `docs/06-sequencediagram/<thu-muc-nhom>/*.txt`, ảnh `.png` cạnh nó, và
`docs/notes/rutgon-ex/<nhom>-*.md`. **Không** đụng `TrustCabinet_Sequence_Diagrams.drawio`,
`drawio-png/`, `docs/08-reports/`, `05-use-case-specifications.md`, `06-business-rules.md`
(file chung, merge song song sẽ xung đột; làm ở bước 5).

Với mỗi UC của nhóm:

1. Chạy `python scripts/rutgon_ex_report.py` để thấy EX nên giữ, gộp, bỏ. Mặc định trần 3 EX và bỏ
   hết lỗi hạ tầng. Script là gợi ý, quyết định cuối theo mục 1.
2. Đọc phần UC tương ứng trong `docs/01-requirements/05-use-case-specifications.md` (chỉ đọc).
3. Viết lại file `.txt` theo mục 1, theo mẫu UC-32a.
4. Xuất ảnh sequencediagram.org: `python scripts/export_sequence.py <mã UC> --only seqorg`.
   Cần Playwright: tạo junction `node_modules\playwright` trỏ tới
   `..\TrustCabinet\tests\qa\node_modules\playwright` (thư mục đã được `.gitignore`).
5. Ghi vào `docs/notes/rutgon-ex/<nhom>-spec-patch.md`, mỗi UC một mục:
   - EX giữ lại, kèm số **mới** và MSG;
   - EX bỏ hoặc gộp (ghi số cũ, lý do: hạ tầng / trùng MSG / vượt trần);
   - các bước "flow resumes at…" hoặc tham chiếu EX cũ trong spec phải sửa.
6. UC không giải quyết được: ghi vào `docs/notes/rutgon-ex/<nhom>-ba-chon.md` (UC, EX ứng viên, MSG,
   gợi ý), **không sửa `.txt` của UC đó**.
7. Cuối nhánh viết `docs/notes/rutgon-ex/<nhom>-bao-cao.md`: bảng UC, số dòng `.txt` trước và sau,
   EX trước và sau, UC chuyển sang ba-chon.

## 4. Điều kiện xong của một nhánh con

- Nhãn `- EX-n` trong mọi `.txt` của nhóm trùng danh sách EX giữ lại trong spec patch.
- `grep -riE "save failed|load failed|read failure|try again" docs/06-sequencediagram/<thu-muc>`
  không còn trong các `.txt` đã sửa.
- Mỗi `.png` đã xuất lại và mở được.
- `git status` chỉ có file trong phạm vi mục 3.
- Báo cáo của nhóm đã viết.

Mặc định **không commit, không push**. Chỉ commit khi người giao việc nói rõ. Nếu được phép: commit
Vietnamese, tiền tố `[Docs]`, một commit cho mỗi nhóm UC.

## 5. Gộp và hoàn tất (tuần tự, một agent)

1. Merge từng nhánh con vào `docs/rutgon-ex-base` (không xung đột vì mỗi nhóm một thư mục).
2. Chạy `python scripts/export_sequence.py <mã UC> --only drawio` cho **mọi UC đã đổi**, để cập nhật
   tab trong `.drawio` gộp và `drawio-png/` một lần.
3. Chờ BA xử lý các file `*-ba-chon.md`, rồi lặp lại bước 3 của mục 3 cho các UC đó.
4. **UC Spec (DOCX):** áp dụng toàn bộ `*-spec-patch.md` vào
   `docs/08-reports/source/Report_3_Software_Requirement_Specification.docx` — bỏ/gộp EX, đánh lại
   số, sửa "resumes". Chạy
   `python scripts/sync_reports.py --source docs/08-reports/source/Report_3_Software_Requirement_Specification.docx --report 3`.
   Chỉ một agent làm bước này. Không sửa `.md` sinh tự động bằng tay.
5. **Report 4:** cập nhật ảnh sequence trong DOCX Report 4 và chạy `sync_reports.py --report 4`.
6. Kiểm khớp toàn cục: so mọi UC giữa spec đã sync và sequence (nhãn EX, MSG), xác nhận 155 UC khớp.
7. Chạy `dotnet test` (chỉ `MessageCodeCatalogueTests` đủ): không MSG nào bị mất khỏi catalogue.

## 6. Rủi ro

- `.drawio` gộp 3 800 dòng và DOCX 9 MB: chỉ đụng ở mục 5, không ở nhánh con.
- Playwright lỗi trên Windows: thử lại một lần; vẫn lỗi thì ghi vào báo cáo, không bỏ qua `.png`.
- Số EX đổi thì chỗ trỏ tới "EX-n của UC-xx" (case QA trong `tests/qa`, bộ case integration, ghi chú
  Jira) có thể lệch. Ở mục 5 bước 6, `grep` các tham chiếu này và liệt kê cho BA.

## 7. Prompt mẫu cho từng agent

Thay `<nhom>` và `<thu-muc>` theo mục 2:

> Đọc `docs/notes/rutgon-ex/KE-HOACH.md` trong repo TrustCabinet. Bạn làm nhánh `docs/rutgon-ex-<nhom>`
> (tách từ `docs/rutgon-ex-base`, trong worktree `../TrustCabinet-<nhom>`), nhóm thư mục
> `<thu-muc>`. Làm đúng mục 3: viết lại từng `.txt` theo mục 1 và mẫu UC-32a, xuất ảnh
> `--only seqorg`, ghi spec patch, ba-chon và báo cáo. Không đụng file ngoài phạm vi mục 3, không
> commit. Dừng khi mọi UC của nhóm xong hoặc đã chuyển sang ba-chon, rồi báo kết quả.
