using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiExamBank.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExamDraftMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExamDraftBatches",
                columns: table => new
                {
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fingerprint = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamDraftBatches", x => new { x.ExamId, x.JobId });
                    table.ForeignKey(
                        name: "FK_ExamDraftBatches_ExamGenerations_ExamId",
                        column: x => x.ExamId,
                        principalTable: "ExamGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamDraftSlots",
                columns: table => new
                {
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlotIndex = table.Column<int>(type: "int", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamDraftSlots", x => new { x.ExamId, x.JobId, x.SlotIndex });
                    table.ForeignKey(
                        name: "FK_ExamDraftSlots_ExamDraftBatches_ExamId_JobId",
                        columns: x => new { x.ExamId, x.JobId },
                        principalTable: "ExamDraftBatches",
                        principalColumns: new[] { "ExamId", "JobId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExamDraftLinks",
                columns: table => new
                {
                    ExamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SlotIndex = table.Column<int>(type: "int", nullable: false),
                    Ordinal = table.Column<int>(type: "int", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SourceLocator = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamDraftLinks", x => new { x.ExamId, x.SlotIndex, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_ExamDraftLinks_ExamDraftSlots_ExamId_JobId_SlotIndex",
                        columns: x => new { x.ExamId, x.JobId, x.SlotIndex },
                        principalTable: "ExamDraftSlots",
                        principalColumns: new[] { "ExamId", "JobId", "SlotIndex" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamDraftLinks_ExamId_JobId_SlotIndex",
                table: "ExamDraftLinks",
                columns: new[] { "ExamId", "JobId", "SlotIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamDraftLinks");

            migrationBuilder.DropTable(
                name: "ExamDraftSlots");

            migrationBuilder.DropTable(
                name: "ExamDraftBatches");
        }
    }
}
