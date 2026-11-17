-- M4-03 query evidence seed. Run against a THROWAWAY database that already has every EF migration applied.
--   sqlcmd -S .\SQLEXPRESS -E -C -d AiExamBankM4Perf -i query_evidence_seed.sql
-- Creates 1 school, 1 department (Toán), 20 teachers, 1 department head and @Questions questions with 1-4
-- revisions each (about 2.5 on average), 4 options per revision, all four statuses and all three difficulties.
-- Teacher #1 (UserName 'perf-teacher-01') owns about 1 question in 20. Nothing here touches other databases.
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
DECLARE @Questions int = 60000;
DECLARE @School uniqueidentifier = '11111111-1111-1111-1111-111111111111';
DECLARE @Dept   uniqueidentifier = '22222222-2222-2222-2222-222222222222';
DECLARE @Math   uniqueidentifier = '8ab2a612-3558-435f-9e60-ef3f8e24a8a8';
DECLARE @Head   uniqueidentifier = '33333333-3333-3333-3333-333333333333';
DECLARE @Now datetimeoffset = SYSDATETIMEOFFSET();

INSERT Schools(Id, Name) VALUES (@School, N'Trường evidence');
INSERT Departments(Id, SchoolId, Name) VALUES (@Dept, @School, N'Tổ Toán');
INSERT SchoolSubjects(SchoolId, SubjectId, DepartmentId) VALUES (@School, @Math, @Dept);

-- 20 teachers: ids 44444444-4444-4444-4444-0000000000NN
;WITH n AS (SELECT TOP (20) ROW_NUMBER() OVER (ORDER BY (SELECT 1)) AS i FROM sys.all_objects)
INSERT AspNetUsers(Id, UserName, NormalizedUserName, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, SchoolId)
SELECT TRY_CAST('44444444-4444-4444-4444-' + RIGHT('000000000000' + CAST(i AS varchar(12)), 12) AS uniqueidentifier),
       'perf-teacher-' + RIGHT('0' + CAST(i AS varchar(2)), 2), 'PERF-TEACHER-' + RIGHT('0' + CAST(i AS varchar(2)), 2),
       0, 0, 0, 0, 0, @School FROM n;
INSERT AspNetUsers(Id, UserName, NormalizedUserName, EmailConfirmed, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, SchoolId)
VALUES (@Head, 'perf-head', 'PERF-HEAD', 0, 0, 0, 0, 0, @School);
INSERT RoleAssignments(Id, UserId, Role, SchoolId, DepartmentId)
SELECT NEWID(), Id, 'Teacher', @School, @Dept FROM AspNetUsers WHERE UserName LIKE 'perf-teacher-%';
INSERT RoleAssignments(Id, UserId, Role, SchoolId, DepartmentId) VALUES (NEWID(), @Head, 'DepartmentHead', @School, @Dept);

-- questions
CREATE TABLE #q (n int PRIMARY KEY, Id uniqueidentifier, Revs int, Topic uniqueidentifier, Diff nvarchar(16), Owner uniqueidentifier);
;WITH nums AS (SELECT TOP (@Questions) ROW_NUMBER() OVER (ORDER BY (SELECT 1)) AS n FROM sys.all_objects a CROSS JOIN sys.all_objects b)
INSERT #q
SELECT n, NEWID(), 1 + (n % 4),
       (SELECT Id FROM Topics WHERE SubjectId = @Math ORDER BY Id OFFSET (n % 3) ROWS FETCH NEXT 1 ROWS ONLY),
       CHOOSE(1 + (n % 3), 'Easy', 'Medium', 'Hard'),
       TRY_CAST('44444444-4444-4444-4444-' + RIGHT('000000000000' + CAST(1 + ((n / 7) % 20) AS varchar(12)), 12) AS uniqueidentifier)
FROM nums;

INSERT Questions(Id, SchoolId, DepartmentId, SubjectId, OwnerUserId, CreatedAtUtc, UpdatedAtUtc, LatestRevision)
SELECT Id, @School, @Dept, @Math, Owner, DATEADD(MINUTE, -n, @Now), DATEADD(SECOND, -n * 7, @Now), Revs FROM #q;

-- revisions: older ones are decided (Approved/Rejected), the latest cycles through all four statuses
;WITH r AS (SELECT q.*, v.rev FROM #q q JOIN (VALUES (1),(2),(3),(4)) v(rev) ON v.rev <= q.Revs)
INSERT QuestionRevisions(QuestionId, Revision, SubjectId, TopicId, Difficulty, Stem, CorrectOption, Status, Origin, SourceText, CreatedByUserId, CreatedAtUtc)
SELECT Id, rev, @Math, Topic, Diff,
       N'Câu hỏi số ' + CAST(n AS nvarchar(12)) + N' (bản ' + CAST(rev AS nvarchar(2)) + N') về ứng dụng đạo hàm',
       CHOOSE(1 + (n % 4), 'A', 'B', 'C', 'D'),
       CASE WHEN rev < Revs THEN CHOOSE(1 + ((n + rev) % 2), 'Approved', 'Rejected')
            ELSE CHOOSE(1 + (n % 4), 'Draft', 'PendingReview', 'Approved', 'Rejected') END,
       'Manual', N'SGK Toán 12', Owner, DATEADD(SECOND, -n * 7 - (Revs - rev), @Now)
FROM r;

INSERT QuestionOptions(QuestionId, Revision, Label, Text)
SELECT QuestionId, Revision, l.Label, N'Đáp án ' + l.Label
FROM QuestionRevisions CROSS JOIN (VALUES ('A'),('B'),('C'),('D')) l(Label)
WHERE QuestionId IN (SELECT Id FROM #q);

DROP TABLE #q;
SELECT (SELECT COUNT(*) FROM Questions) AS Questions, (SELECT COUNT(*) FROM QuestionRevisions) AS Revisions, (SELECT COUNT(*) FROM QuestionOptions) AS Options;
