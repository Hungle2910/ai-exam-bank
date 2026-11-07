using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiExamBank.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BlueprintArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAtUtc",
                table: "Blueprints",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "Blueprints");
        }
    }
}
