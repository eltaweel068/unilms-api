using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniLMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddLectureNumberToCourseMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "CourseMaterials",
                type: "character varying(350)",
                maxLength: 350,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<int>(
                name: "LectureNumber",
                table: "CourseMaterials",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CourseMaterials_CourseId_LectureNumber",
                table: "CourseMaterials",
                columns: new[] { "CourseId", "LectureNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CourseMaterials_CourseId_LectureNumber",
                table: "CourseMaterials");

            migrationBuilder.DropColumn(
                name: "LectureNumber",
                table: "CourseMaterials");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "CourseMaterials",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(350)",
                oldMaxLength: 350);
        }
    }
}
