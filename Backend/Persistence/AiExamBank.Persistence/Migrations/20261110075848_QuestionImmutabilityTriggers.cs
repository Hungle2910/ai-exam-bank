using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiExamBank.Persistence.Migrations
{
    /// <summary>
    /// Makes question history immutable in the database itself, so a raw SQL fix, a script or a future module cannot
    /// rewrite what an exam may have pinned. The application enforces the same rules earlier (QuestionImmutabilityGuard);
    /// these triggers are the backstop. A revision's content is written once, only its status may move, and an approved
    /// or rejected revision never moves again. Options are never changed. Questions are never deleted and their latest
    /// revision only moves forward.
    /// </summary>
    public partial class QuestionImmutabilityTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_QuestionRevisions_Immutable] ON [QuestionRevisions] AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;

    -- A deleted row with no matching row left behind is a delete (or a change of the key).
    IF EXISTS (SELECT 1 FROM deleted d WHERE NOT EXISTS (
        SELECT 1 FROM inserted i WHERE i.[QuestionId] = d.[QuestionId] AND i.[Revision] = d.[Revision]))
        THROW 51001, 'A question revision cannot be deleted or renumbered.', 1;

    -- EXCEPT compares NULLs as equal, so optional columns are handled without special cases.
    IF EXISTS (
        SELECT 1 FROM deleted d JOIN inserted i ON i.[QuestionId] = d.[QuestionId] AND i.[Revision] = d.[Revision]
        WHERE EXISTS (
            SELECT d.[SubjectId], d.[TopicId], d.[Difficulty], d.[Stem], d.[CorrectOption], d.[Origin], d.[SourceText],
                   d.[ImportBatchId], d.[ImportRowNumber], d.[CreatedByUserId], d.[CreatedAtUtc]
            EXCEPT
            SELECT i.[SubjectId], i.[TopicId], i.[Difficulty], i.[Stem], i.[CorrectOption], i.[Origin], i.[SourceText],
                   i.[ImportBatchId], i.[ImportRowNumber], i.[CreatedByUserId], i.[CreatedAtUtc]))
        THROW 51002, 'The content of a question revision cannot be changed; save a new revision instead.', 1;

    IF EXISTS (
        SELECT 1 FROM deleted d JOIN inserted i ON i.[QuestionId] = d.[QuestionId] AND i.[Revision] = d.[Revision]
        WHERE d.[Status] IN (N'Approved', N'Rejected') AND i.[Status] <> d.[Status])
        THROW 51003, 'An approved or rejected question revision is final.', 1;
END");

            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_QuestionOptions_Immutable] ON [QuestionOptions] AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 51004, 'The options of a question revision cannot be changed or deleted.', 1;
END");

            migrationBuilder.Sql(@"
CREATE TRIGGER [TR_Questions_Immutable] ON [Questions] AFTER UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted d WHERE NOT EXISTS (SELECT 1 FROM inserted i WHERE i.[Id] = d.[Id]))
        THROW 51005, 'A question cannot be deleted.', 1;

    IF EXISTS (SELECT 1 FROM deleted d JOIN inserted i ON i.[Id] = d.[Id] WHERE i.[LatestRevision] < d.[LatestRevision])
        THROW 51006, 'The latest revision of a question can only move forward.', 1;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_Questions_Immutable]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QuestionOptions_Immutable]");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS [TR_QuestionRevisions_Immutable]");
        }
    }
}
