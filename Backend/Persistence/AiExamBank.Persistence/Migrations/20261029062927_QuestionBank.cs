using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AiExamBank.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolSubjects",
                columns: table => new
                {
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolSubjects", x => new { x.SchoolId, x.SubjectId });
                    table.UniqueConstraint("AK_SchoolSubjects_SchoolId_SubjectId_DepartmentId", x => new { x.SchoolId, x.SubjectId, x.DepartmentId });
                    table.ForeignKey(
                        name: "FK_SchoolSubjects_Departments_DepartmentId_SchoolId",
                        columns: x => new { x.DepartmentId, x.SchoolId },
                        principalTable: "Departments",
                        principalColumns: new[] { "Id", "SchoolId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolSubjects_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SchoolSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                    table.UniqueConstraint("AK_Topics_Id_SubjectId", x => new { x.Id, x.SubjectId });
                    table.ForeignKey(
                        name: "FK_Topics_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestRevision = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.UniqueConstraint("AK_Questions_Id_SubjectId", x => new { x.Id, x.SubjectId });
                    table.CheckConstraint("CK_Questions_LatestRevision", "[LatestRevision] >= 1");
                    table.ForeignKey(
                        name: "FK_Questions_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Questions_SchoolSubjects_SchoolId_SubjectId_DepartmentId",
                        columns: x => new { x.SchoolId, x.SubjectId, x.DepartmentId },
                        principalTable: "SchoolSubjects",
                        principalColumns: new[] { "SchoolId", "SubjectId", "DepartmentId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionRevisions",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Difficulty = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Stem = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CorrectOption = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Origin = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ImportRowNumber = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionRevisions", x => new { x.QuestionId, x.Revision });
                    table.CheckConstraint("CK_QuestionRevisions_CorrectOption", "[CorrectOption] IN ('A', 'B', 'C', 'D')");
                    table.CheckConstraint("CK_QuestionRevisions_Difficulty", "[Difficulty] IN ('Easy', 'Medium', 'Hard')");
                    table.CheckConstraint("CK_QuestionRevisions_ImportRow", "([ImportBatchId] IS NULL AND [ImportRowNumber] IS NULL AND [Origin] <> 'Import') OR ([ImportBatchId] IS NOT NULL AND [ImportRowNumber] >= 1 AND [Origin] = 'Import')");
                    table.CheckConstraint("CK_QuestionRevisions_Origin", "[Origin] IN ('Manual', 'Import', 'AiDraft', 'Seed')");
                    table.CheckConstraint("CK_QuestionRevisions_Revision", "[Revision] >= 1");
                    table.CheckConstraint("CK_QuestionRevisions_Status", "[Status] IN ('Draft', 'ReviewRequired', 'Approved', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_QuestionRevisions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionRevisions_Questions_QuestionId_SubjectId",
                        columns: x => new { x.QuestionId, x.SubjectId },
                        principalTable: "Questions",
                        principalColumns: new[] { "Id", "SubjectId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionRevisions_Topics_TopicId_SubjectId",
                        columns: x => new { x.TopicId, x.SubjectId },
                        principalTable: "Topics",
                        principalColumns: new[] { "Id", "SubjectId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => new { x.QuestionId, x.Revision, x.Label });
                    table.CheckConstraint("CK_QuestionOptions_Label", "[Label] IN ('A', 'B', 'C', 'D')");
                    table.ForeignKey(
                        name: "FK_QuestionOptions_QuestionRevisions_QuestionId_Revision",
                        columns: x => new { x.QuestionId, x.Revision },
                        principalTable: "QuestionRevisions",
                        principalColumns: new[] { "QuestionId", "Revision" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Subjects",
                columns: new[] { "Id", "Code", "Name" },
                values: new object[,]
                {
                    { new Guid("1d9f0a75-b153-4626-a9c4-6d4830641c6a"), "PHYS", "Vật lý" },
                    { new Guid("4d779815-9f88-4ba2-a3e4-0b6916a4407b"), "CHEM", "Hóa học" },
                    { new Guid("8ab2a612-3558-435f-9e60-ef3f8e24a8a8"), "MATH", "Toán" }
                });

            migrationBuilder.InsertData(
                table: "Topics",
                columns: new[] { "Id", "Code", "Name", "SubjectId" },
                values: new object[,]
                {
                    { new Guid("223f2355-059b-4195-80e0-6056ea6115f6"), "MATH.DERIV_APP", "Ứng dụng đạo hàm để khảo sát hàm số", new Guid("8ab2a612-3558-435f-9e60-ef3f8e24a8a8") },
                    { new Guid("24b94d5e-208b-471a-9b1f-f8ee87241cdd"), "CHEM.CARBOHYDRATE", "Carbohydrate", new Guid("4d779815-9f88-4ba2-a3e4-0b6916a4407b") },
                    { new Guid("699edf31-9caf-43ae-8e38-603bcdf3ba4d"), "MATH.INTEGRAL", "Nguyên hàm và tích phân", new Guid("8ab2a612-3558-435f-9e60-ef3f8e24a8a8") },
                    { new Guid("6d4e7dd8-e7ce-4f69-808a-c8f4bd22b514"), "PHYS.MAGNETIC", "Từ trường", new Guid("1d9f0a75-b153-4626-a9c4-6d4830641c6a") },
                    { new Guid("7e1d4f7a-bf13-4c8a-9db5-8f34f51f1fd9"), "PHYS.IDEAL_GAS", "Khí lí tưởng", new Guid("1d9f0a75-b153-4626-a9c4-6d4830641c6a") },
                    { new Guid("7ecd1b83-2cb8-4026-9e8e-89d2dd0fb171"), "PHYS.NUCLEAR", "Vật lí hạt nhân và phóng xạ", new Guid("1d9f0a75-b153-4626-a9c4-6d4830641c6a") },
                    { new Guid("e8d1686c-50ad-4b59-866d-2eb558286919"), "CHEM.ELECTROCHEM", "Pin điện và điện phân", new Guid("4d779815-9f88-4ba2-a3e4-0b6916a4407b") },
                    { new Guid("f5da0e27-7b7e-4a06-951f-049c29f0596f"), "CHEM.ESTER_LIPID", "Ester – Lipid", new Guid("4d779815-9f88-4ba2-a3e4-0b6916a4407b") },
                    { new Guid("fbd23806-f00a-48d2-abc0-84790d72452a"), "MATH.OXYZ", "Phương pháp tọa độ trong không gian", new Guid("8ab2a612-3558-435f-9e60-ef3f8e24a8a8") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionRevisions_CreatedByUserId",
                table: "QuestionRevisions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionRevisions_ImportBatchId_ImportRowNumber",
                table: "QuestionRevisions",
                columns: new[] { "ImportBatchId", "ImportRowNumber" },
                unique: true,
                filter: "[ImportBatchId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionRevisions_QuestionId_SubjectId",
                table: "QuestionRevisions",
                columns: new[] { "QuestionId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionRevisions_TopicId_Difficulty_Status",
                table: "QuestionRevisions",
                columns: new[] { "TopicId", "Difficulty", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionRevisions_TopicId_SubjectId",
                table: "QuestionRevisions",
                columns: new[] { "TopicId", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_OwnerUserId",
                table: "Questions",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SchoolId_SubjectId_DepartmentId",
                table: "Questions",
                columns: new[] { "SchoolId", "SubjectId", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SchoolId_SubjectId_UpdatedAtUtc",
                table: "Questions",
                columns: new[] { "SchoolId", "SubjectId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubjects_DepartmentId",
                table: "SchoolSubjects",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubjects_DepartmentId_SchoolId",
                table: "SchoolSubjects",
                columns: new[] { "DepartmentId", "SchoolId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchoolSubjects_SubjectId",
                table: "SchoolSubjects",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Code",
                table: "Subjects",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Topics_SubjectId_Code",
                table: "Topics",
                columns: new[] { "SubjectId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "QuestionRevisions");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropTable(
                name: "Topics");

            migrationBuilder.DropTable(
                name: "SchoolSubjects");

            migrationBuilder.DropTable(
                name: "Subjects");
        }
    }
}
