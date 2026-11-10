# M4-03: bằng chứng truy vấn

Mọi thứ ở đây chạy trên một database **dùng một lần** (không bao giờ trỏ vào database chung hay staging). Kết quả và cách đọc nằm trong [../M4-03_VERSION_HISTORY.md](../M4-03_VERSION_HISTORY.md), mục 5 và 6.

| File | Dùng để |
|---|---|
| [query_evidence_seed.sql](query_evidence_seed.sql) | Tạo 60.000 câu / 150.000 revision / 600.000 option |
| [index_experiment.sql](index_experiment.sql), [run_experiment.sh](run_experiment.sh) | Chạy 7 câu SQL đại diện (cùng câu EF gửi) và in logical reads từng bảng |
| [query_evidence.sql](query_evidence.sql), [run_evidence.sh](run_evidence.sh) | Chạy các truy vấn thật của ứng dụng (`QuestionQueryEvidenceRunner`) rồi đọc chi phí từ DMV của SQL Server |
| `evidence_before.txt`, `evidence_after.txt` | Kết quả của `run_evidence.sh` trước (database ở migration `QuestionBank`) và sau hai migration M4-03 |
| `evidence_experiment_before.txt`, `evidence_experiment_after.txt` | Kết quả của `run_experiment.sh` trước và sau |

## Tái lập (từ thư mục `Backend`, SQL Server Express cục bộ, Windows auth)

```bash
export ConnectionStrings__DefaultConnection='Server=.\SQLEXPRESS;Database=AiExamBankM4Perf;Integrated Security=true;TrustServerCertificate=True'
E="--project Persistence/AiExamBank.Persistence --startup-project Api"

dotnet ef database update $E                                           # tạo database và áp mọi migration
sqlcmd -S .\\SQLEXPRESS -E -C -I -d AiExamBankM4Perf -i docs/M4-03/query_evidence_seed.sql   # nạp dữ liệu (cần -I)

dotnet ef database update 20261029062927_QuestionBank $E               # về trạng thái trước M4-03
bash docs/M4-03/run_experiment.sh  > docs/M4-03/evidence_experiment_before.txt
bash docs/M4-03/run_evidence.sh    > docs/M4-03/evidence_before.txt

dotnet ef database update $E                                           # lên trạng thái sau M4-03
bash docs/M4-03/run_experiment.sh  > docs/M4-03/evidence_experiment_after.txt
bash docs/M4-03/run_evidence.sh    > docs/M4-03/evidence_after.txt
```

Ghi chú:
- `run_evidence.sh` tắt `AUTO_CLOSE` của database trước khi chạy. SQL Express bật nó mặc định, và khi test đóng kết nối, SQL Server đóng database rồi xóa luôn plan cache mà script cần đọc.
- Thứ tự `uniqueidentifier` của SQL Server không theo thứ tự chuỗi. Seed chọn chủ đề bằng `ORDER BY Id` của SQL Server; mọi script và runner làm đúng như vậy.
- Thời gian chạy trên SQL Express dao động mạnh (sau khi dựng lại index lớn có lần một truy vấn chạm timeout 30 giây rồi lần sau chỉ mất vài mili giây). Chỉ so logical reads.
- Các test hồi quy về revision (edit-after-approve, 409, race, quyền, N+1, trigger) chạy với `AIEXAMBANK_TEST_SQLSERVER`, xem `Backend/tests/Persistence.Tests/QuestionRevisionDatabaseTests.cs`.
