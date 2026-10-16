using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiExamBank.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SchoolScopeAndRoleAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RoleAssignments_UserId",
                table: "RoleAssignments");

            migrationBuilder.AddColumn<Guid>(
                name: "SchoolId",
                table: "AspNetUsers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoleAssignmentAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleAssignmentAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleAssignmentAudits_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleAssignmentAudits_AspNetUsers_TargetUserId",
                        column: x => x.TargetUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Schools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schools", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SchoolId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.UniqueConstraint("AK_Departments_Id_SchoolId", x => new { x.Id, x.SchoolId });
                    table.ForeignKey(
                        name: "FK_Departments_Schools_SchoolId",
                        column: x => x.SchoolId,
                        principalTable: "Schools",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_DepartmentId_SchoolId",
                table: "RoleAssignments",
                columns: new[] { "DepartmentId", "SchoolId" });

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_SchoolId",
                table: "RoleAssignments",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_UserId_Role_SchoolId_DepartmentId",
                table: "RoleAssignments",
                columns: new[] { "UserId", "Role", "SchoolId", "DepartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SchoolId",
                table: "AspNetUsers",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_SchoolId",
                table: "Departments",
                column: "SchoolId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignmentAudits_ActorUserId",
                table: "RoleAssignmentAudits",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignmentAudits_TargetUserId",
                table: "RoleAssignmentAudits",
                column: "TargetUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Schools_SchoolId",
                table: "AspNetUsers",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleAssignments_Departments_DepartmentId_SchoolId",
                table: "RoleAssignments",
                columns: new[] { "DepartmentId", "SchoolId" },
                principalTable: "Departments",
                principalColumns: new[] { "Id", "SchoolId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RoleAssignments_Schools_SchoolId",
                table: "RoleAssignments",
                column: "SchoolId",
                principalTable: "Schools",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Schools_SchoolId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleAssignments_Departments_DepartmentId_SchoolId",
                table: "RoleAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_RoleAssignments_Schools_SchoolId",
                table: "RoleAssignments");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "RoleAssignmentAudits");

            migrationBuilder.DropTable(
                name: "Schools");

            migrationBuilder.DropIndex(
                name: "IX_RoleAssignments_DepartmentId_SchoolId",
                table: "RoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_RoleAssignments_SchoolId",
                table: "RoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_RoleAssignments_UserId_Role_SchoolId_DepartmentId",
                table: "RoleAssignments");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SchoolId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SchoolId",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_RoleAssignments_UserId",
                table: "RoleAssignments",
                column: "UserId");
        }
    }
}
