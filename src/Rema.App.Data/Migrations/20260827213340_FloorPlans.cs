using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rema.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class FloorPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FloorPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CanvasWidth = table.Column<int>(type: "integer", nullable: false),
                    CanvasHeight = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FloorBoxes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    FloorPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Offer = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Highlight = table.Column<bool>(type: "boolean", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    Width = table.Column<int>(type: "integer", nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FloorBoxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FloorBoxes_FloorPlans_FloorPlanId",
                        column: x => x.FloorPlanId,
                        principalTable: "FloorPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FloorBoxes_FloorPlanId",
                table: "FloorBoxes",
                column: "FloorPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_FloorBoxes_StoreId_FloorPlanId",
                table: "FloorBoxes",
                columns: new[] { "StoreId", "FloorPlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_FloorPlans_StoreId_Name",
                table: "FloorPlans",
                columns: new[] { "StoreId", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FloorBoxes");

            migrationBuilder.DropTable(
                name: "FloorPlans");
        }
    }
}
