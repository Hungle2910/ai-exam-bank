# Team & ownership

Nhóm tham gia **AWS FCAJ 2026 Internship** với tên nhóm **AWS Hello World**. Tên chính thức không dấu dưới đây theo roster nhóm; task IDs giữ M1–M5 để các dependencies ổn định. Một số issue cũ dùng nhãn ngắn “Minh Phúc” cho M3.

| ID | Official name (ASCII) | Task label | Role | Hours | Primary modules | Peer reviewers |
|---|---|---|---|---:|---|---|
| M1 | Le Doan Gia Hung | Gia Hưng | Solution Architect & Exam Workflow Lead | 184 | Contracts, Blueprint, Generation, Final Preview | Nguyen Thien Phuc domain; Tran Gia Bao resilience |
| M2 | Nguyen Huynh Huu Phuoc | Hữu Phước | Cloud & DevOps Engineer + Import Owner | 186 | IaC/network/deploy, Import, Infrastructure Health | Nguyen Thien Phuc schema; Tran Gia Bao jobs; Nguyen Hoang Phuc IAM |
| M3 | Nguyen Hoang Phuc | Minh Phúc (legacy task label) | Security & Approval Platform Engineer | 180 | Identity/RBAC, Review/Approval, Audit | Le Doan Gia Hung transitions; Nguyen Thien Phuc evidence |
| M4 | Nguyen Thien Phuc | Thiên Phúc | Question Bank & RAG Product Engineer | 196 | Question Bank, Revisions, Knowledge/RAG; optional Lex | Le Doan Gia Hung domain; Nguyen Hoang Phuc access |
| M5 | Tran Gia Bao | Gia Bảo | Reliability & ML Evaluation Engineer | 180 | Jobs/Retry/Monitoring; optional ML | Nguyen Huynh Huu Phuoc infra; Le Doan Gia Hung worker contracts |

## Shared ownership rules

Mỗi thành viên có ít nhất một vertical slice DB → API → UI → AWS/integration → tests → docs. M2 provision AWS resources; owner module viết SDK adapter/telemetry; M3 review permissions. M1 chốt contracts và integration conflicts, không viết thay mọi feature.

Import thuộc M2 và gọi domain writer của M4. Approval/audit/UI thuộc M3, sử dụng citations/evidence do M4 cung cấp. Job state/retry thuộc M5, business handlers thuộc M1/M2/M4. Mỗi owner instrument metrics module mình; M5 quản reliability signals, M2 quản infra/log pipeline.

## GitHub identities

| Official name (ASCII) | ID / task label | GitHub account | Repository task assignment |
|---|---|---|---|
| Le Doan Gia Hung | M1 / Gia Hưng | [@Hungle2910](https://github.com/Hungle2910) | 10/10 |
| Nguyen Huynh Huu Phuoc | M2 / Hữu Phước | [@HuuPhuoc-NH](https://github.com/HuuPhuoc-NH) | 10/10 |
| Nguyen Hoang Phuc | M3 / Minh Phúc (legacy task label) | [@Lancelot-sys25](https://github.com/Lancelot-sys25) | 10/10 |
| Nguyen Thien Phuc | M4 / Thiên Phúc | [@flwndyy](https://github.com/flwndyy) | 10/10 |
| Tran Gia Bao | M5 / Gia Bảo | [@TranGiaBao2005](https://github.com/TranGiaBao2005) | 10/10 |

Bốn thành viên M2–M5 đã được cấp quyền Write trên Project và đã chấp nhận lời mời repo. Cả 50 issues có GitHub Assignee tương ứng; Owner field vẫn giữ họ tên để các view hiện có hoạt động. Thêm module owners vào CODEOWNERS khi module paths ổn định. PR của Gia Hưng cần người khác có quyền review; không tự duyệt thay cho peer review.

## Capacity and role fit

Planning estimate 926h, khoảng 18.5h/người/tuần; cần dung lượng dự phòng. M4 tuần 4–5 có tải cao nhất; Lex được hoãn trước nếu cần. M5 làm reliability từ tuần 1; ML chỉ mở sau MVP gate. Các role-specific skills/bài kiểm tra và weekly outputs được ghi trong [Implementation plan](IMPLEMENTATION_PLAN.md).
