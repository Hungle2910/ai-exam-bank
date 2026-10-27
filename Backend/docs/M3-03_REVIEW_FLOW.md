| Trạng thái hiện tại | Thao tác | Trạng thái sau | Điều kiện |
|---|---|---|---|
| DRAFT | Submit | PENDING_REVIEW | Người tạo gửi đúng phiên bản |
| PENDING_REVIEW | Approve | APPROVED | Người duyệt đúng phạm vi, không tự duyệt, version còn đúng |
| PENDING_REVIEW | Reject | REJECTED | Cùng điều kiện trên và có lý do |

sai quyền → 403; reject thiếu lý do → 400; phiên bản đã đổi hoặc đã được quyết định → 409.
giúp M3 và M4 thống nhất trạng thái trước khi nối API, tránh viết hai bộ trạng thái khác nhau.
