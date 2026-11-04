# Đo chất lượng dự thảo AI

Bộ script so các model viết dự thảo biên bản (SCRUM-36, [ADR-0054](../../docs/05-decisions/0054-ai-du-thao-bien-ban-chay-tai-cho.md))
trên cùng 8 cuộc họp mẫu, từ đầy đủ đến thiếu từng phần. Không chạy trong CI; chạy tay khi đổi model hay đổi prompt.

| Cuộc họp mẫu | Kiểm tra gì |
|---|---|
| Đầy đủ | Chương trình có giờ, kết luận, biểu quyết, tài liệu, lời thoại theo micro: đủ số liệu, đúng tên, số viết bằng chữ số |
| Không có biểu quyết | Không bịa kết quả biểu quyết |
| Không có chương trình | Mục II chia theo vấn đề được bàn |
| Chương trình chưa bấm giờ | Lời thoại không chia được theo mục, vẫn phải đủ ý |
| Không có lời thoại | Ghi "Chưa ghi nhận ý kiến thảo luận", không bịa số liệu |
| Mic phòng chưa gán tên | Giữ "Người nói N", không đoán tên |
| Rất ít dữ liệu | Không từ chối, viết hoa tên riêng, đổi ngày sang chữ số |
| Họp dài | Một mục vượt một lần gọi AI: chia nhỏ rồi gộp |

Mỗi cuộc họp có danh sách **ý cần có** và **lỗi cấm** trong [`cases.py`](cases.py). Báo cáo đếm số ý đúng, số lỗi và
thời gian, kèm toàn bộ dự thảo để đọc lại.

## Chạy

Cần stack local đang chạy (`docker compose up -d`, backend ở `:5225`) và một server AI. Ví dụ với Ollama:

```bash
brew install ollama && ollama serve &
ollama pull qwen2.5:7b-instruct
# backend/TrustCabinet.Web/appsettings.Development.json: "Ai": { "BaseUrl": "http://localhost:11434/v1", "Model": "qwen2.5:7b-instruct" }

python3 tests/ai/benchmark.py seed                          # tạo 8 cuộc họp [AI-BENCH], ghi out/fixture.json
python3 tests/ai/benchmark.py run --label qwen2.5-7b        # ghi out/qwen2.5-7b.md
python3 tests/ai/benchmark.py clean                         # huỷ các cuộc họp [AI-BENCH]
```

- **Đổi model:** sửa `Ai:Model` (hoặc `Ai:BaseUrl` sang máy khác, ví dụ `llama-server` của llama.cpp ở
  `http://<máy>:8080/v1`), khởi động lại backend, rồi `run` với `--label` khác. Không cần `seed` lại.
- **Lệnh `seed`** ghi thẳng vào database dev qua `docker compose exec postgres psql`, nên chỉ chạy với stack local.
- **Lệnh `clean`** chuyển các cuộc họp sang "Đã huỷ" chứ không xoá, vì lịch sử trạng thái cuộc họp không cho xoá.
- **Máy nóng:** model 7B dùng hết CPU khoảng 1–2 phút cho mỗi cuộc họp. Chạy xong thì `ollama stop <model>` để giải
  phóng RAM.
