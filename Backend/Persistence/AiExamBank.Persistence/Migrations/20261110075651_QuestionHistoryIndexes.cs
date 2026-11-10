using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiExamBank.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionHistoryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Questions_OwnerUserId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_SchoolId_SubjectId_DepartmentId",
                table: "Questions");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_OwnerUserId_UpdatedAtUtc_Id",
                table: "Questions",
                columns: new[] { "OwnerUserId", "UpdatedAtUtc", "Id" },
                descending: new[] { false, true, false })
                .Annotation("SqlServer:Include", new[] { "SchoolId", "DepartmentId", "SubjectId", "LatestRevision" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SchoolId_SubjectId_DepartmentId_UpdatedAtUtc_Id",
                table: "Questions",
                columns: new[] { "SchoolId", "SubjectId", "DepartmentId", "UpdatedAtUtc", "Id" },
                descending: new[] { false, false, false, true, false })
                .Annotation("SqlServer:Include", new[] { "OwnerUserId", "LatestRevision" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Questions_OwnerUserId_UpdatedAtUtc_Id",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_SchoolId_SubjectId_DepartmentId_UpdatedAtUtc_Id",
                table: "Questions");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_OwnerUserId",
                table: "Questions",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SchoolId_SubjectId_DepartmentId",
                table: "Questions",
                columns: new[] { "SchoolId", "SubjectId", "DepartmentId" });
        }
    }
}
