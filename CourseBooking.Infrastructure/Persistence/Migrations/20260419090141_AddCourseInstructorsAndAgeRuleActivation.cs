using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CourseBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseInstructorsAndAgeRuleActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CourseInstructorId",
                table: "CourseOfferings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AgeRules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CourseInstructors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseInstructors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseOfferings_CourseInstructorId",
                table: "CourseOfferings",
                column: "CourseInstructorId");

            migrationBuilder.AddForeignKey(
                name: "FK_CourseOfferings_CourseInstructors_CourseInstructorId",
                table: "CourseOfferings",
                column: "CourseInstructorId",
                principalTable: "CourseInstructors",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseOfferings_CourseInstructors_CourseInstructorId",
                table: "CourseOfferings");

            migrationBuilder.DropTable(
                name: "CourseInstructors");

            migrationBuilder.DropIndex(
                name: "IX_CourseOfferings_CourseInstructorId",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "CourseInstructorId",
                table: "CourseOfferings");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AgeRules");
        }
    }
}
