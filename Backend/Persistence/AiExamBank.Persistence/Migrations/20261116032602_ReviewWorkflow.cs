using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiExamBank.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ReviewVersion",
                table: "QuestionRevisions",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedAtUtc",
                table: "QuestionRevisions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_AspNetUsers_ReviewerUserId",
                        column: x => x.ReviewerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_QuestionRevisions_QuestionId_Revision",
                        columns: x => new { x.QuestionId, x.Revision },
                        principalTable: "QuestionRevisions",
                        principalColumns: new[] { "QuestionId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_QuestionId_Revision",
                table: "ReviewDecisions",
                columns: new[] { "QuestionId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ReviewerUserId",
                table: "ReviewDecisions",
                column: "ReviewerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewDecisions");

            migrationBuilder.DropColumn(
                name: "ReviewVersion",
                table: "QuestionRevisions");

            migrationBuilder.DropColumn(
                name: "SubmittedAtUtc",
                table: "QuestionRevisions");
        }
    }
}
