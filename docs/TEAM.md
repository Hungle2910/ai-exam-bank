# Team & ownership

Tên thành viên do project owner cung cấp. Task IDs giữ M1–M5 để các dependencies ổn định.

| ID | Member | Role | Hours | Primary modules | Peer reviewers |
|---|---|---|---:|---|---|
| M1 | Gia Hưng | Solution Architect & Exam Workflow Lead | 184 | Contracts, Blueprint, Generation, Final Preview | Thiên Phúc domain; Gia Bảo resilience |
| M2 | Hữu Phước | Cloud & DevOps Engineer + Import Owner | 186 | IaC/network/deploy, Import, Infrastructure Health | Thiên Phúc schema; Gia Bảo jobs; Minh Phúc IAM |
| M3 | Minh Phúc | Security & Approval Platform Engineer | 180 | Identity/RBAC, Review/Approval, Audit | Gia Hưng transitions; Thiên Phúc evidence |
| M4 | Thiên Phúc | Question Bank & RAG Product Engineer | 196 | Question Bank, Revisions, Knowledge/RAG; optional Lex | Gia Hưng domain; Minh Phúc access |
| M5 | Gia Bảo | Reliability & ML Evaluation Engineer | 180 | Jobs/Retry/Monitoring; optional ML | Hữu Phước infra; Gia Hưng worker contracts |

## Shared ownership rules

Mỗi thành viên có ít nhất một vertical slice DB → API → UI → AWS/integration → tests → docs. M2 provision AWS resources; owner module viết SDK adapter/telemetry; M3 review permissions. M1 chốt contracts và integration conflicts, không viết thay mọi feature.

Import thuộc M2 và gọi domain writer của M4. Approval/audit/UI thuộc M3, sử dụng citations/evidence do M4 cung cấp. Job state/retry thuộc M5, business handlers thuộc M1/M2/M4. Mỗi owner instrument metrics module mình; M5 quản reliability signals, M2 quản infra/log pipeline.

## GitHub identities

| Member | GitHub account | Repository task assignment |
|---|---|---|
| Gia Hưng (M1) | [@Hungle2910](https://github.com/Hungle2910) | 10/10 |
| Hữu Phước (M2) | [@HuuPhuoc-NH](https://github.com/HuuPhuoc-NH) | Chờ chấp nhận lời mời repo; 0/10 |
| Minh Phúc (M3) | [@Lancelot-sys25](https://github.com/Lancelot-sys25) | 10/10 |
| Thiên Phúc (M4) | [@flwndyy](https://github.com/flwndyy) | 10/10 |
| Gia Bảo (M5) | [@TranGiaBao2005](https://github.com/TranGiaBao2005) | 10/10 |

Bốn thành viên M2–M5 đã được cấp quyền Write trên Project; repo invitations đã gửi. GitHub Assignees của M2 sẽ được gán sau khi Hữu Phước chấp nhận lời mời repo. Owner field vẫn giữ họ tên để các view hiện có hoạt động. Thêm module owners vào CODEOWNERS khi repo access ổn định. PR của Gia Hưng cần người khác có quyền review; không tự duyệt thay cho peer review.

## Capacity and role fit

Planning estimate 926h, khoảng 18.5h/người/tuần; cần dung lượng dự phòng. M4 tuần 4–5 có tải cao nhất; Lex được hoãn trước nếu cần. M5 làm reliability từ tuần 1; ML chỉ mở sau MVP gate. Các role-specific skills/bài kiểm tra và weekly outputs được ghi trong [Implementation plan](IMPLEMENTATION_PLAN.md).
