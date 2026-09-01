using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rema.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class FloorPlanShapes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShapesJson",
                table: "FloorPlans",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShapesJson",
                table: "FloorPlans");
        }
    }
}
