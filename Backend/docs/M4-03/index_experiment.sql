-- Same statements the application sends (via sp_executesql, so plans are built the way EF's are), with STATISTICS IO.
-- Run before and after the candidate indexes:  sqlcmd -S .\SQLEXPRESS -E -C -I -d AiExamBankM4Perf -i index_experiment.sql
SET NOCOUNT ON; SET STATISTICS IO ON;
DECLARE @school uniqueidentifier='11111111-1111-1111-1111-111111111111', @dept uniqueidentifier='22222222-2222-2222-2222-222222222222',
        @math uniqueidentifier='8ab2a612-3558-435f-9e60-ef3f8e24a8a8', @topic uniqueidentifier=(SELECT Id FROM Topics WHERE SubjectId='8ab2a612-3558-435f-9e60-ef3f8e24a8a8' ORDER BY Id OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY),
        @teacher uniqueidentifier='44444444-4444-4444-4444-000000000001';
DECLARE @page nvarchar(max) = N'SELECT [q].[Id], [q0].[Revision], [q].[OwnerUserId], [q0].[Difficulty], [q0].[Status], [q0].[Stem], [q].[UpdatedAtUtc]
 FROM [Questions] AS [q] INNER JOIN [QuestionRevisions] AS [q0] ON [q].[Id]=[q0].[QuestionId] AND [q].[LatestRevision]=[q0].[Revision]
 INNER JOIN [Subjects] AS [s] ON [q].[SubjectId]=[s].[Id] INNER JOIN [Topics] AS [t] ON [q0].[TopicId]=[t].[Id]
 WHERE [q].[SchoolId]=@school AND @filter AND [q].[SubjectId]=@math @extra
 ORDER BY [q].[UpdatedAtUtc] DESC,[q].[Id] OFFSET @skip ROWS FETCH NEXT 20 ROWS ONLY';
DECLARE @count nvarchar(max) = N'SELECT COUNT(*) FROM [Questions] AS [q] INNER JOIN [QuestionRevisions] AS [q0] ON [q].[Id]=[q0].[QuestionId] AND [q].[LatestRevision]=[q0].[Revision]
 WHERE [q].[SchoolId]=@school AND @filter AND [q].[SubjectId]=@math @extra';
DECLARE @head nvarchar(max) = N'[q].[DepartmentId]=@dept AND [q0].[Status]=N''PendingReview''';
DECLARE @own  nvarchar(max) = N'[q].[OwnerUserId]=@teacher AND [q].[DepartmentId]=@dept';
DECLARE @topicx nvarchar(max) = N' AND [t].[Id]=@topic AND [q0].[Difficulty]=N''Easy''';
DECLARE @sql nvarchar(max), @params nvarchar(200) = N'@school uniqueidentifier,@dept uniqueidentifier,@math uniqueidentifier,@topic uniqueidentifier,@teacher uniqueidentifier,@skip int';

PRINT 'CASE 1 head queue, page 1';
SET @sql = REPLACE(REPLACE(REPLACE(@page,'@filter',@head),'@extra',''),'@skip','@skip');
EXEC sp_executesql @sql,@params,@school,@dept,@math,@topic,@teacher,0;
PRINT 'CASE 2 head queue, topic+difficulty, page 1';
SET @sql = REPLACE(REPLACE(@page,'@filter',@head),'@extra',@topicx);
EXEC sp_executesql @sql,@params,@school,@dept,@math,@topic,@teacher,0;
PRINT 'CASE 3 head queue count';
SET @sql = REPLACE(REPLACE(@count,'@filter',@head),'@extra','');
EXEC sp_executesql @sql,@params,@school,@dept,@math,@topic,@teacher,0;
PRINT 'CASE 4 teacher own list, page 1';
SET @sql = REPLACE(REPLACE(@page,'@filter',@own),'@extra','');
EXEC sp_executesql @sql,@params,@school,@dept,@math,@topic,@teacher,0;
PRINT 'CASE 5 teacher own list, deep page';
EXEC sp_executesql @sql,@params,@school,@dept,@math,@topic,@teacher,1000;
PRINT 'CASE 6 teacher own list, topic+difficulty, page 1';
SET @sql = REPLACE(REPLACE(@page,'@filter',@own),'@extra',@topicx);
EXEC sp_executesql @sql,@params,@school,@dept,@math,@topic,@teacher,0;
PRINT 'CASE 7 teacher own count';
SET @sql = REPLACE(REPLACE(@count,'@filter',@own),'@extra','');
EXEC sp_executesql @sql,@params,@school,@dept,@math,@topic,@teacher,0;
