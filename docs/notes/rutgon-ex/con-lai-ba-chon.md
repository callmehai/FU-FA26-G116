# UC chưa giải quyết được — nhóm con-lai

## UC-04 – Access Meeting via Guest Invitation
- Lý do: 9 EX nghiệp vụ (EX-1..EX-9), không gộp được về ≤ 3 mà không mất nghĩa.
- EX ứng viên (theo spec):
  - EX-1 invitation missing/revoked/Meeting no longer open — MSG371
  - EX-2 code incorrect/expired/already used — MSG126
  - EX-3 verification service unavailable — MSG374 (hạ tầng, có thể bỏ)
  - EX-4 code locked after 5 incorrect entries — MSG372
  - EX-5 new code requested too soon — MSG373
  - EX-6 too many codes requested (20/24h) — MSG379
  - EX-7 display information invalid — MSG377
  - EX-8 Guest session ended — MSG378
  - EX-9 display information already declared — MSG381
- Gợi ý: giữ EX-1, EX-2, EX-4, EX-7, EX-9 (nghiệp vụ); bỏ EX-3 (hạ tầng); EX-5, EX-6 có thể gộp (cùng MSG? khác MSG) — BA quyết. Không sửa `.txt`.