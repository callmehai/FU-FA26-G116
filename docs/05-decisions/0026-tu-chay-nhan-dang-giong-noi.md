# ADR-0026: Tự chạy nhận dạng giọng nói (chuyển thành chữ, tách người nói) trên một worker nội bộ

- **Trạng thái:** Đề xuất
- **Ngày:** 2026-10-23
- **Người quyết định:** PO (23/10/2026), nhóm SEP490-G116
- **Liên quan:** `SCRUM-29`, `SCRUM-31`, `SCRUM-35`; nhận xét hội đồng review vòng 1 (17/10/2026);
  bỏ dòng *"tự vận hành mô hình AI"* trong mục cân nhắc thu hẹp của README

## Bối cảnh

Hội đồng review vòng 1 nêu hạn chế quan trọng nhất của đồ án: với file ghi âm, hệ thống chưa biết ai
phát biểu, và bản gỡ băng chưa gắn với từng người nói. Hội đồng đề xuất xây dựng Voice ID cho từng
người, tách và nhận diện nhiều người nói trong cùng một file ghi âm, và yêu cầu nhóm **tính rõ chi phí
AI** (chuyển giọng nói thành chữ, nhận dạng người nói, tóm tắt) để chứng minh dự án khả thi về kinh tế.
Giảng viên hướng dẫn giao kế hoạch tuần 23–30/10/2026: code xong và báo cáo phần này.

Trước đó SCRUM-31 để ngỏ lựa chọn giữa dịch vụ ASR ngoài và mô hình tự chạy. README xếp *"tự vận
hành mô hình AI"* vào nhóm nên cắt nếu tiến độ căng.

Ràng buộc:

- Giọng nói là dữ liệu cá nhân, và **giọng nói dùng để nhận diện người là dữ liệu sinh trắc học**,
  thuộc nhóm nhạy cảm theo Luật Bảo vệ dữ liệu cá nhân 91/2025/QH15 (hiệu lực 01/01/2026) và Nghị định
  356/2025/NĐ-CP. Gửi ra dịch vụ nước ngoài là **chuyển dữ liệu ra nước ngoài**, kéo theo hồ sơ đánh giá
  tác động riêng.
- Dịch vụ ngoài chỉ trả nhãn *Speaker A/B*, không biết đó là ai. Muốn nhận ra người thì vẫn phải tự giữ
  và so khớp giọng mẫu (SCRUM-35). Azure Speaker Recognition, dịch vụ phổ biến nhất cho việc này, đã
  ngừng từ 30/09/2025.
- Server test hiện là Lightsail 1 GB, không chạy nổi mô hình. Nhóm sẽ chuyển sang EC2 lớn hơn sau khi
  chạy ổn ở máy cá nhân.
- AI-eCabinet, hệ thống nhóm lấy làm mẫu nghiệp vụ, cũng tự chạy toàn bộ trên CPU bằng sherpa-onnx.

## Quyết định

Chúng tôi sẽ chạy nhận dạng giọng nói **trong hệ thống**, bằng một worker riêng `workers/speech`
(Python 3.12, sherpa-onnx, chỉ CPU, không cần GPU). Backend gọi worker qua HTTP trong mạng nội bộ.
Worker không lưu gì: nhận âm thanh, trả kết quả, xoá tệp tạm.

| Việc | Mô hình | License |
|---|---|---|
| Chuyển giọng nói tiếng Việt thành chữ | Gipformer 1.5 (Zipformer RNN-T 68M, int8) | MIT |
| Lọc khoảng lặng | Silero VAD | MIT |
| Đặc trưng giọng (vector 512 số), dùng cả để tách người nói | 3D-Speaker ERes2Net base | Apache-2.0 |

Tách người nói không dùng bộ có sẵn của sherpa-onnx (pyannote): đo trên máy nhóm, bộ đó chậm gần bằng
độ dài bản ghi khi chạy trong Docker. Worker cắt phần có tiếng thành khung 3 giây, lấy vector giọng
từng khung rồi gom nhóm. Sau đó nó gộp tiếp những nhóm có giọng tổng thể gần như trùng nhau. Cách
này nhanh hơn khoảng 6 lần, đổi lại chỗ chuyển người nói chỉ chính xác tới khoảng 0,75 giây và không
tách được hai người nói chồng lên nhau. Trước khi lọc khoảng lặng, worker tăng âm trượt theo từng
quãng 1,5 giây, để lời của người ngồi xa máy ghi không bị bỏ qua. Ngưỡng được chỉnh trên giọng tổng
hợp và một bản ghi cuộc họp thật có đáp án (`workers/speech/README.md`, mục "Hiệu chỉnh").

Mỗi tệp mô hình được ghim bằng SHA-256 trong `workers/speech/models.manifest`, tải lúc build image và
kiểm lại lúc khởi động. Tên và mã băm của mô hình được ghi vào từng bản gỡ băng
(`Transcript.Provider = "self-hosted:gipformer-…@<sha12>"`), nên luôn biết bản nào do mô hình nào tạo.

Backend xếp hàng bản gỡ băng (`Transcript.Status`) và chạy **từng bản một**: worker dùng hết CPU, chạy
lần lượt nhanh hơn nhiều bản tranh nhau. Bản đang chạy dở khi app tắt sẽ được xếp lại khi khởi động.

## Phương án đã cân nhắc

| Phương án | Ưu | Nhược | Vì sao không chọn |
|---|---|---|---|
| Dịch vụ ngoài (Deepgram, AssemblyAI, Google Chirp…) cho ASR và tách người nói | Dựng nhanh; độ chính xác tiếng Việt của Google khá | Tính tiền theo phút (khoảng 0,0025–0,016 USD/phút, tách người nói tính thêm); giọng nói ra nước ngoài; vẫn phải tự làm phần nhận ra ai | Không giải quyết được câu hỏi "ai nói", lại thêm nghĩa vụ chuyển dữ liệu ra nước ngoài |
| Dịch vụ trong nước (FPT.AI, Viettel AI) | Dữ liệu ở Việt Nam | Không có nhận dạng người nói theo giọng mẫu; vẫn tính tiền theo phút; phụ thuộc tài khoản doanh nghiệp | Như trên, và khó có tài khoản cho đồ án |
| Whisper large-v3 / PhoWhisper tự chạy | Chính xác cao | Cần GPU hoặc chạy CPU rất chậm; image vài GB | Không chạy nổi trên server của nhóm trong một tuần |
| Bỏ phần AI, chỉ quản lý biên bản (phương án thay thế của hội đồng) | Ít rủi ro | Mất điểm khác biệt chính của đề tài | Nhóm đã chạy thử thành công trên CPU |

## Hệ quả

- **Không có chi phí theo phút.** Chi phí là tiền máy: báo cáo cho hội đồng sẽ đo thời gian xử lý thực
  tế (RTF) trên server test và quy ra tiền máy cho mỗi giờ họp, đặt cạnh giá dịch vụ ngoài.
- **Âm thanh không rời hệ thống**, nên không phát sinh chuyển dữ liệu ra nước ngoài. Đồng ý, mục đích
  và hạn lưu trữ của bản ghi vẫn giữ như SCRUM-29. Bản gỡ băng bị xoá cùng âm thanh.
- **Phải có máy đủ lớn:** worker cần khoảng 2–3 GB RAM. Máy 1 GB hiện tại không đủ, phải lên EC2 trước
  khi demo trên link chung. Ở máy cá nhân chạy được bằng `docker compose --profile speech up -d`.
- Không có worker thì app vẫn chạy. Bản gỡ băng báo *"Máy chủ nhận dạng giọng nói chưa được cấu hình"*
  hoặc *"Không kết nối được…"*, và Thư ký chạy lại được khi worker lên.
- Độ chính xác chữ thấp hơn Whisper. Vì vậy bản gỡ băng luôn được đánh dấu **do máy tạo**, và Thư ký
  sửa được từng đoạn (ghi rõ ai sửa, lúc nào).
- ADR-0010 vẫn đúng: không có bot nào vào phòng họp. Worker chỉ xử lý tệp Thư ký đã lưu.
