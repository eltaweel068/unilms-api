using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniLMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseArabicFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""Courses"" ADD COLUMN IF NOT EXISTS ""TitleAr"" character varying(200);
                ALTER TABLE ""Courses"" ADD COLUMN IF NOT EXISTS ""DescriptionAr"" character varying(1000);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionAr",
                table: "Courses");

            migrationBuilder.DropColumn(
                name: "TitleAr",
                table: "Courses");
        }
    }
}
