# SCRUM-97 — UC-73 giao Chủ tọa

**Ngày ghi:** 24/10/2026
**Nguồn:** tin nhắn agent của leader gửi Khánh
**Khi nào sửa:** lúc **BA Final Review**

SCRUM-97 (UC-73 giao Chủ tọa): FE xong ở PR #123. PO Hải đã chốt nghiệp vụ ngày 23/10/2026.

PR: https://github.com/callmehai/TrustCabinet/pull/123

Chi tiết: Jira SCRUM-97 (2 comment mới nhất), `docs/03-api/09-thanh-phan-tham-du-ban-giao.md` mục "Nghiệp vụ đổi vai trò đã chốt".

## Nghiệp vụ đã chốt

Theo các tình huống họp nội bộ hay gặp. Quy tắc giữ như baseline 20/10/2026:

- Đã công bố: Chủ tọa giao cho một Thành viên, Chủ tọa cũ thành Thành viên. Thư ký chỉ thay Chủ tọa khi tài khoản Chủ tọa bị khóa.
- Trước công bố: đổi vai trò tự do.
- Đổi người làm thư ký: cho người mới làm Thư ký, rồi đổi người cũ về Thành viên.
- **Không** làm việc Chủ tọa và Thư ký đổi vai trò cho nhau (thực tế gần như không xảy ra).

## FE khác spec UC-73

- Bỏ bước "bấm Hand over Chair → hệ thống hiện danh sách người nhận" (Normal Flow bước 1–2). Thay vào đó, Chủ tọa bấm Đổi vai trò trên dòng người muốn giao, chọn "Chủ tọa", rồi bấm Giao Chủ tọa.
- MSG258 (không ai nhận được) không hiện trên UI: nếu không ai đủ điều kiện thì không có lựa chọn "Chủ tọa".
- Chọn vai trò là danh sách radio, mỗi vai trò có một dòng mô tả quyền, như AI-eCabinet. Tên của người đang đăng nhập có thêm "(Tôi)".

## Cần sửa

- Report 3 mục 6.6 (UC-73) và sequence `UC73_HandOverChair` (`.txt`, `.png`, tab trong `TrustCabinet_Sequence_Diagrams.drawio`)
- Dòng MSG258 trong `docs/03-api/00-danh-muc-ma-thong-bao.md`
- BR-126: thêm ngoại lệ "Thư ký thay Chủ tọa khi tài khoản Chủ tọa bị khóa"
