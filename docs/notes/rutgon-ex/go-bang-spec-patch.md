# Spec patch — nhóm go-bang (transcription-voice-id + meeting-recording)
n> **Lưu ý:** nếu mục này lệch với `go-bang-spec-patch.md` thì theo file `go-bang-ex-final.md` (sinh từ diagram sau lượt chọn cuối của BA).

Áp dụng sau khi merge. Chỉ ghi EX giữ lại (số mới + MSG) và EX bỏ/gộp (số cũ + lý do). Không đổi MSG/BR.

## transcription-voice-id

- UC-45a View Recording Transcript: không còn EX. Bỏ EX-1 cũ (MSG299, hạ tầng).
- UC-45b Export Meeting Transcript: giữ EX-1 (MSG355), EX-2 (MSG356), EX-3 (MSG357).
- UC-45c Process Transcription Queue: giữ EX-2 (no MSG). (transcription failed)
- UC-45c Transcribe Meeting Recording: giữ EX-3 (MSG479), EX-1 (MSG301). Bỏ EX-2 cũ (MSG304, hạ tầng).
- UC-46a Edit Transcript Text: giữ EX-2 (MSG295), EX-1 (MSG303). Bỏ EX-2 cũ (MSG304, hạ tầng).
- UC-46b Assign Speaker: giữ EX-2 (MSG295), EX-1 (MSG317), EX-3 (no MSG).
- UC-46c Exclude or Restore Sentence: giữ EX-1 (MSG353). Bỏ EX-2 cũ (MSG354, BA xem: lý do loại trừ không hợp lệ).
- UC-46d Delete Sentence: giữ EX-1 (MSG323). Bỏ EX-2 cũ (MSG304, hạ tầng).
- UC-47 Generate / Regenerate Meeting Summary: giữ EX-1 (MSG385), EX-2 (MSG386), EX-3 (MSG387). Bỏ EX-9 cũ (MSG481, vượt trần), EX-4 cũ (MSG394, vượt trần), EX-5 cũ (no MSG, hạ tầng lưu nháp), EX-6 cũ (no MSG, hạ tầng AI server).
- UC-48 View Meeting Summary: giữ EX-1 (no MSG). Bỏ EX-1 cũ (MSG392, hạ tầng).
- UC-49 Edit Meeting Summary: giữ EX-3 (MSG385), EX-2 (MSG390), EX-1 (MSG389). Bỏ EX-6 cũ (MSG387, vượt trần), EX-4 cũ (MSG393, hạ tầng).
- UC-50 Approve Meeting Summary: giữ EX-2 (MSG385), EX-1 (MSG390). Bỏ EX-5 cũ (MSG387, vượt trần), EX-3 cũ (MSG393, hạ tầng).
- UC-81a View My Voice ID: không còn EX. Bỏ EX-1 cũ (MSG314, hạ tầng).
- UC-81b Give Voice ID Consent: giữ EX-1 (MSG313). Bỏ EX-2 cũ (MSG315, hạ tầng).
- UC-81c Record Voice Sample: giữ EX-2 (MSG292), EX-1 (MSG308), EX-3 (MSG309). Bỏ EX-6 cũ (MSG313, vượt trần), EX-4 cũ (MSG310, vượt trần), EX-5 cũ (MSG312, hạ tầng), EX-7 cũ (MSG315, hạ tầng).
- UC-81d Play Voice Sample: giữ EX-1 (no MSG).
- UC-81e Delete Voice Sample: không còn EX. Bỏ EX-1 cũ (MSG315, hạ tầng).
- UC-81f Withdraw Voice ID Consent: không còn EX. Bỏ EX-1 cũ (MSG315, hạ tầng).
- UC-86a View Voice Collection: giữ EX-1 (no MSG). Bỏ EX-2 cũ (no MSG, hạ tầng).
- UC-86b Record Voice Sample for Attendee: giữ EX-1 (no MSG), EX-2 (MSG475), EX-3 (MSG476). Bỏ EX-4 cũ (MSG483, vượt trần), EX-6 cũ (MSG309, vượt trần), EX-7 cũ (MSG312, hạ tầng).
- UC-86c Play Collected Voice Sample: giữ EX-1 (no MSG).
- UC-86d Delete Collected Voice Sample: giữ EX-1 (no MSG). Bỏ EX-2 cũ (MSG315, hạ tầng).

## meeting-recording

- UC-43a View Meeting Recordings: giữ EX-1 (no MSG). Bỏ EX-3 cũ (no MSG, hạ tầng roster), EX-2 cũ (MSG299, hạ tầng câu), EX-1 cũ (MSG297, hạ tầng danh sách).
- UC-43b Play Meeting Recording: giữ EX-1 (no MSG). Bỏ EX-1 cũ (MSG297, hạ tầng).
- UC-43c Download Meeting Recording: giữ EX-1 (MSG355), EX-2 (MSG356). Bỏ EX-3 cũ (MSG482, hạ tầng chuyển MP3).
- UC-44a Start Room Recording: giữ EX-1 (MSG291), EX-2 (MSG295), EX-3 (MSG358). Bỏ EX-5 cũ (MSG359, vượt trần), EX-6 cũ (MSG360, vượt trần), EX-3 cũ (MSG298, hạ tầng), EX-7 cũ (MSG361, vượt trần).
- UC-44b Stop and Save Room Recording: giữ EX-1 (MSG362), EX-2 (MSG295). Bỏ EX-3 cũ (MSG298, hạ tầng).
- UC-44c Upload Meeting Audio File: giữ EX-2 (MSG292), EX-1 (MSG291), EX-3 (MSG293). Bỏ EX-5 cũ (MSG295, vượt trần), EX-7 cũ (MSG358, vượt trần), EX-8 cũ (MSG478, vượt trần), EX-4 cũ (MSG294, vượt trần), EX-6 cũ (MSG298, hạ tầng).
- UC-44d Delete Recording Take: giữ EX-2 (MSG295), EX-1 (MSG367). (không bỏ)
- UC-44e Erase Meeting Recording: giữ EX-2 (MSG295), EX-1 (MSG364). Bỏ EX-3 cũ (MSG298, hạ tầng).
- UC-80 Manage My Recorded Speech: giữ EX-1 (MSG323). Bỏ EX-2 cũ (MSG304, hạ tầng).

## meeting-recording (SYS)

- SYS_BuildCombinedRecording: không đổi EX; sửa thanh activation.

## Ghi chú cho BA khi sync Report 3

- Các tham chiếu "EX-n của UC-xx" và các bước "flow resumes at…" trong UC Spec phải đánh lại theo số mới ở trên (đặc biệt UC-45c có hai sơ đồ, UC-47 bỏ EX-9 "no sentence recorded", UC-44a/UC-44c đổi thứ tự EX).
- UC Spec đã sync theo code; BA Final Review điều chỉnh UC Spec, sơ đồ và BR cho khớp.
PYEOF