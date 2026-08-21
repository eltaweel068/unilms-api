using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniLMS.API.Migrations
{
    /// <inheritdoc />
    public partial class AddSummaryTextToCourseMaterials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SummaryText",
                table: "CourseMaterials",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SummaryText",
                table: "CourseMaterials");
        }
    }
}
