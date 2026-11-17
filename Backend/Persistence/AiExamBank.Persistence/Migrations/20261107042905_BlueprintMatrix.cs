using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiExamBank.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BlueprintMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Blueprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    TotalQuestions = table.Column<int>(type: "int", nullable: false),
                    TotalPoints = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blueprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Blueprints_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Blueprints_SchoolSubjects_SchoolId_SubjectId_DepartmentId",
                        columns: x => new { x.SchoolId, x.SubjectId, x.DepartmentId },
                        principalTable: "SchoolSubjects",
                        principalColumns: new[] { "SchoolId", "SubjectId", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BlueprintSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BlueprintId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Difficulty = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    QuestionCount = table.Column<int>(type: "int", nullable: false),
                    PointsPerQuestion = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlueprintSlots_Blueprints_BlueprintId",
                        column: x => x.BlueprintId,
                        principalTable: "Blueprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlueprintSlots_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Blueprints_OwnerUserId_SchoolId_UpdatedAtUtc",
                table: "Blueprints",
                columns: new[] { "OwnerUserId", "SchoolId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Blueprints_SchoolId_SubjectId_DepartmentId",
                table: "Blueprints",
                columns: new[] { "SchoolId", "SubjectId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintSlots_BlueprintId_TopicId_Difficulty",
                table: "BlueprintSlots",
                columns: new[] { "BlueprintId", "TopicId", "Difficulty" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintSlots_TopicId",
                table: "BlueprintSlots",
                column: "TopicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlueprintSlots");

            migrationBuilder.DropTable(
                name: "Blueprints");
        }
    }
}
