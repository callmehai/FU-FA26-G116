# Đánh số lại UC / BR / MSG (một lần, trước khi nộp)

Mục tiêu: UC, BR, MSG tăng dần, liền mạch, không khuyết số — trong báo cáo, `docs/`, code và test.
Đây là một lần duy nhất; xong thì đóng băng lại (ADR thay thế luật "không đánh số lại" — xem bước 0).

## Thư mục

| File | Việc |
|---|---|
| `build_mapping.py` | Dựng `mapping/uc.csv`, `br.csv`, `msg.csv` (cũ → mới) + `review.txt` từ nguồn hiện tại |
| `apply_mapping.py` | Đổi mọi tham chiếu một lượt theo bảng; mặc định dry-run; `--write`, `--docx`, `--adrs` |
| `verify.py` | Kiểm tra liền mạch, tăng dần, mã trong code đều có trong catalogue |
| `mapping/` | Bảng ánh xạ (nguồn duy nhất, nộp kèm báo cáo), `orphans.log` |

## Quy tắc đánh số mới

- **UC:** theo thứ tự Feature trong inventory; UC con giữ hậu tố chữ (`UC-06a`) theo số mới của UC mẹ.
- **BR:** theo thứ tự bảng `06-business-rules.md`; dòng *Retired* bị xoá khỏi bảng.
- **MSG:** theo thứ tự nhóm trong mục "Danh mục"; mã *Ngừng dùng* bị xoá. Mã chỉ có ở phần contract được xếp cuối.

## Quy trình

Chạy từ gốc repo, trên một nhánh riêng, sau khi mọi PR đang mở đã merge hoặc bị hoãn.

1. **Chốt luật:** ADR mới thay luật cũ; sửa `AGENTS.md`, quy tắc trong catalogue MSG, "Quy ước số" trong inventory.
2. `python scripts/renumber/build_mapping.py` → duyệt `mapping/*.csv`, `review.txt`. **Điểm kiểm soát chính.**
3. `python scripts/renumber/apply_mapping.py` (dry-run) → đọc `mapping/orphans.log`: mỗi id mồ côi (BR/UC/MSG bị bỏ nhưng còn được nhắc) và mỗi "range" (`MSG147–151`, `UC-23a–d`) xử lý tay.
4. `python scripts/renumber/apply_mapping.py --write --docx`, rồi sửa tay phần ở bước 3.
5. Sắp xếp lại *thứ tự các dòng* (inventory theo Feature, bảng BR, catalogue MSG, mục UC trong DOCX Report 3) cho khớp số mới — script chỉ đổi số, không dời dòng.
6. DOCX: các số nằm giữa nhiều run bị tách đôi không tự đổi được — dò lại bằng cách tìm số cũ; sau đó `python scripts/sync_reports.py ...` để sinh lại snapshot và `uc-map.html`.
7. `python scripts/renumber/verify.py`, rồi `dotnet build TrustCabinet.sln -warnaserror`, `dotnet test`, `npm run lint`, `npm test`, `npm run build`.
8. Một PR vào `app`, tách commit: docs, backend, frontend, test. Nộp `mapping/` cùng báo cáo.

## Không tự động đổi

- ADR đã accepted (thêm `--adrs` nếu muốn); ghi một dòng trỏ tới bảng ánh xạ ở `docs/05-decisions/README.md`.
- `Report-*.md`, `05-use-case-specifications.md`: sinh từ DOCX.
- Ảnh PNG sơ đồ/wireframe có chữ UC/MSG: phải xuất lại từ `.drawio`.
- Jira: giữ comment cũ; bảng ánh xạ là cầu nối.
- Bảng "số cũ → số cũ hơn" ở cuối inventory (đợt đánh số lại 70 UC): nên xoá, vì sẽ thành vô nghĩa.

## Phát hiện từ lần chạy thử (trước khi đổi gì)

- `verify.py` trên repo hiện tại: BR thiếu 52 số, MSG thiếu 5, UC thiếu 9/62/63/69; catalogue và inventory chưa tăng dần.
- **MSG275, 276, 278, 349 được dùng trong code nhưng không có trong catalogue** — cần bổ sung dòng trước khi đánh số lại, nếu không script bỏ qua chúng.
- Dry-run: ~2.000 file, ~78.000 token đổi; 120 id mồ côi; 229 range cần xem tay.
