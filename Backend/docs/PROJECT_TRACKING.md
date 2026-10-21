# Project tracking and delivery process

Board structure tham khảo [AutoWash Pro Delivery](https://github.com/users/harry-leon/projects/4). Domain content, owner mapping và backlog riêng cho AI Exam Bank; không copy source project tasks/progress/assignees.

## Fields

| Field | Meaning / update rule |
|---|---|
| Title / Task ID | Stable M1–M5 + week ID; title describes concrete output |
| Owner | Gia Hưng / Hữu Phước / Minh Phúc / Thiên Phúc / Gia Bảo; một người primary |
| Assignees | M1 @Hungle2910, M2 @HuuPhuoc-NH, M3 @Lancelot-sys25, M4 @flwndyy, M5 @TranGiaBao2005; mỗi người 10 issue |
| Status | Backlog → Ready → In progress → In review → Done; Blocked khi cần upstream action |
| Priority | P0 critical-path/core foundation; P1 mandatory hardening; P2 gated extension |
| Epic | Foundation, Identity/Approval, Question Bank/RAG, Exam Workflow, Cloud/Import, Reliability/ML |
| Workspace | Cross-stack delivery domain; không ép owner chỉ frontend/backend |
| Type | Feature / Tech / Docs / Bug |
| Size | Estimate scale dùng 16–24h work packages; không gọi size là % completion |
| Estimate | Planned owner hours, gồm implementation/tests/review/docs |
| Actual Hours | Owner cập nhật khi có work log; unset không có nghĩa bằng 0 |
| Week | W01–W10, giữ thứ tự tuần dự án |
| Scope | MUST / MIXED / SHOULD / STRETCH; mixed phải hoàn thành MUST phần core trước |
| Difficulty | Technical difficulty 1–5; khác pedagogical question difficulty |
| Dependencies | Stable IDs + clickable issue references; dependency unresolved chặn Ready |
| Blocker / Risk | Actual blocker và known risk được tách nghĩa; không gọi mọi risk là blocker |
| Start Date / Target Date | Planning baseline khi đã chốt; không auto-close từ ngày trôi qua |
| Milestone | Weekly exit gate, gồm 5 member work packages |
| Linked PRs / Reviewers | PR/evidence và peer reviewer nếu có; owner tự merge vào `dev` sau bot/CI, `main` cần reviewer độc lập |

## Saved views

Delivery Table, Delivery Board, Prioritized Backlog, Roadmap, In Review, My Assigned Items, All Tasks và 5 Owner views. Thêm MVP & Core, Extensions và Blocked để nhìn critical path/capacity. `My Assigned Items` hiển thị 10 task tương ứng cho mỗi tài khoản M1–M5.

Nếu chưa chốt ngày bắt đầu, dates để trống và Roadmap theo date chỉ có ý nghĩa khi điền baseline; Week/milestones vẫn dùng để tracking. Không lấy iteration dates từ project mẫu của người khác.

## Workflow

1. Backlog: scoped nhưng chưa ready hoặc upstream còn thiếu.
2. Ready: owner hiểu scope/acceptance criteria, dependencies đủ và permissions/config cần thiết có sẵn.
3. In progress: có branch/work log; comment tiến độ khi có meaningful change.
4. In review: PR link, test/evidence, docs, actual effort; owner xử lý bot/CI findings và mời peer reviewer khi rủi ro cao.
5. Done: acceptance + DoD đạt, required checks xanh, owner xác nhận và merge; issue close và board cập nhật nhất quán. Promotion lên `main` cần human approval độc lập.
6. Blocked: ghi điều kiện cụ thể, upstream owner/issue và next action; không bỏ task khỏi completion denominator.

Project copy giữ 6 workflows ở trạng thái enabled: Auto-add sub-issues, Auto-close issue, Item added, Item closed, Pull request linked và Pull request merged. GitHub API xác nhận workflow tồn tại/enabled; lifecycle PR→status/close cần được thử khi bắt đầu implementation. Owner vẫn kiểm issue state và Project status nhất quán, và chỉ merge/đóng work package khi toàn DoD đạt. Extensions chưa mở ở gate MVP; giữ Backlog và Scope rõ.

## Issue anatomy

Owner/Role, Goal, Context, In/Out scope, step checklist, dependencies, backend/frontend/DB/AWS deliverables, security requirements, tests, documentation/evidence, acceptance/DoD, estimate/difficulty, risks/blockers và PR/evidence links. Weekly work packages có ba bước implementation và các checklist verification; tách child issues khi owner cần việc 2–4h, giữ liên kết parent.

## Weekly ceremony and completion

Đầu tuần review dependencies/capacity; cuối tuần demo module và exit gate. Mọi member demo DB→API→UI→AWS/integration→tests. Completion MVP dùng effort-weighted DoD, extension report riêng; không đo bằng commit count/service count/date elapsed. Chưa có evidence triển khai thì không claim completion.

926h estimate phân bổ: Gia Hưng 184h; Hữu Phước 186h; Minh Phúc 180h; Thiên Phúc 196h; Gia Bảo 180h. Nếu MVP gate W06 chưa đạt, W07–W08 dùng sửa core, không bắt buộc Lex/ML.
