# ADR-0037: Thêm dấu câu và chữ hoa vào bản gỡ băng bằng viBERT-capu

- **Trạng thái:** Đề xuất — SCRUM-31, PO 28/10/2026
- **Ngày:** 2026-10-28
- **Người quyết định:** Hải (PO)

## Bối cảnh

Gipformer ([ADR-0026](0026-tu-chay-nhan-dang-giong-noi.md)) trả về CHỮ HOA không dấu câu. Worker chỉ đổi thành chữ thường, viết
hoa chữ đầu và thêm dấu chấm cuối mỗi đoạn, nên một lượt nói dài đọc thành một câu không ngắt, và Thư ký phải tự thêm mọi dấu
phẩy. AI-eCabinet giải quyết việc này bằng model viBERT-capu chạy ngay trong worker nhận dạng.

## Quyết định

1. **Worker thêm dấu câu và chữ hoa bằng viBERT-capu** (viBERT của FPT AI, dragonSwing tinh chỉnh; bản ONNX int8 của welcomyou,
   ghim mã băm trong `workers/speech/models.manifest` như các model khác). Chạy trên CPU bằng `onnxruntime`, không cần
   `transformers` hay GPU.
2. **Các đoạn liền nhau của cùng một người (cách nhau ≤ 5 giây) được đọc chung thành một đoạn văn**, rồi cắt lại theo số từ
   của từng đoạn. Chỗ ngắt hơi giữa câu không thành dấu chấm, và mốc giờ của mỗi đoạn giữ nguyên.
3. **Model chỉ được sửa từ tại chỗ** (thêm `,` `.` `:` `?`, đổi chữ hoa/thường). Các thao tác gộp từ hay đổi dạng động từ của
   model bị bỏ qua. Lỡ số từ đổi thì đoạn đó quay về cách cũ.
4. Áp dụng cho cả bản ghi cả phòng và từng câu ghi theo micro (mỗi câu là một đoạn văn).

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Giữ như cũ, Thư ký tự thêm dấu | Không thêm gì | Bản gỡ băng khó đọc, sửa tay rất nhiều | Hội đồng đánh giá trên bản gỡ băng |
| Gọi một LLM thêm dấu câu | Dấu câu tốt hơn, sửa được cả tên riêng | Gửi lời họp ra ngoài, hoặc phải chạy LLM nhiều GB RAM | Trái ADR-0026 (âm thanh và nội dung không ra ngoài) |
| viBERT-capu trong worker | 115 MB, ~680 từ/giây trên CPU, không đổi từ nên không làm hỏng mốc giờ và WER | Thêm ~270 MB RAM, image thêm ~180 MB; license CC BY-SA | **Chọn** |

## Hệ quả

- License **CC BY-SA 4.0**: phải ghi công dragonSwing và FPT AI (ghi trong `models.manifest` và README của worker). Share-alike
  áp cho trọng số đã sửa, không áp cho văn bản model tạo ra; nhóm không sửa trọng số.
- Đo trên cuộc họp thật 5 phút (MacBook M1): thời gian 20,7 s → 21,2 s, WER giữ 16,1%. Image worker ~770 MB → ~950 MB; RAM
  thêm ~270 MB, vẫn trong `mem_limit: 2500m` của server.
- Mã model thứ tư `vibert-capu-int8@67278b23502b` hiện ở `/health`. Bản gỡ băng cũ không tự đổi; Thư ký chạy lại nếu muốn.
- `SPEECH_PUNCTUATION=0` tắt bước này để so sánh.
