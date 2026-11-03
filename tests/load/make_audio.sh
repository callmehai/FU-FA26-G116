#!/usr/bin/env bash
# Synthetic Vietnamese speech for the load tests, so no one's real voice is needed (or leaves the server).
# macOS only: uses the built-in "Linh" voice and ffmpeg. Writes tests/load/out/audio/:
#   clip01.wav … clip12.wav   one sentence each, 16 kHz mono 16-bit (what the call room sends per sentence)
#   room-60s.wav, room-300s.wav, room-600s.wav   the sentences looped with pauses (a room-mic take)
set -euo pipefail
cd "$(dirname "$0")"
out=out/audio
mkdir -p "$out"

sentences=(
  "Kính thưa các đồng chí, hôm nay chúng ta họp để rà soát kế hoạch công tác quý bốn."
  "Tôi đề nghị bổ sung nội dung về tiến độ giải ngân vốn đầu tư công vào chương trình."
  "Báo cáo cho thấy tỷ lệ hoàn thành đạt tám mươi lăm phần trăm so với kế hoạch năm."
  "Về dự thảo quy chế làm việc, tôi thống nhất với phương án thứ hai."
  "Đề nghị văn phòng tổng hợp ý kiến và gửi lại các thành viên trước thứ sáu tuần này."
  "Phần kinh phí cần làm rõ thêm nguồn và thời điểm bố trí."
  "Tôi xin phát biểu ngắn về công tác chuyển đổi số của đơn vị."
  "Chúng ta biểu quyết thông qua nội dung này."
  "Đồng ý."
  "Đề nghị chủ toạ cho ý kiến kết luận."
  "Cuộc họp tạm nghỉ mười phút, sau đó tiếp tục phần thảo luận."
  "Cảm ơn các đồng chí đã tham dự, cuộc họp kết thúc tại đây."
)

i=0
for s in "${sentences[@]}"; do
  i=$((i + 1))
  f=$(printf "%s/clip%02d.wav" "$out" "$i")
  say -v Linh -o "$out/tmp.aiff" "$s"
  ffmpeg -loglevel error -y -i "$out/tmp.aiff" -ar 16000 -ac 1 -map_metadata -1 -fflags +bitexact -flags:a +bitexact -c:a pcm_s16le "$f"
done
rm -f "$out/tmp.aiff"

# One pass of every sentence with 0.8 s of silence after each, then loop it to length.
list="$out/concat.txt"
ffmpeg -loglevel error -y -f lavfi -i anullsrc=r=16000:cl=mono -t 0.8 -c:a pcm_s16le "$out/gap.wav"
: > "$list"
for f in "$out"/clip*.wav; do
  printf "file '%s'\nfile '%s'\n" "$(basename "$f")" gap.wav >> "$list"
done
ffmpeg -loglevel error -y -f concat -safe 0 -i "$list" -c copy "$out/pass.wav"
for secs in 60 300 600; do
  ffmpeg -loglevel error -y -stream_loop -1 -i "$out/pass.wav" -t "$secs" -ar 16000 -ac 1 -map_metadata -1 -fflags +bitexact -flags:a +bitexact -c:a pcm_s16le "$out/room-${secs}s.wav"
done
rm -f "$list" "$out/gap.wav" "$out/pass.wav"
ls -l "$out"
