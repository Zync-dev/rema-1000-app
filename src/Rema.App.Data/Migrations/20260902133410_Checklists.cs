using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rema.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class Checklists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Recurrence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checklists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistDays_Checklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalTable: "Checklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistItems_Checklists_ChecklistId",
                        column: x => x.ChecklistId,
                        principalTable: "Checklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChecklistTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssigneeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Done = table.Column<bool>(type: "boolean", nullable: false),
                    DoneByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DoneUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChecklistTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChecklistTasks_ChecklistDays_ChecklistDayId",
                        column: x => x.ChecklistDayId,
                        principalTable: "ChecklistDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistDays_ChecklistId_Date",
                table: "ChecklistDays",
                columns: new[] { "ChecklistId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistDays_StoreId_Date",
                table: "ChecklistDays",
                columns: new[] { "StoreId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistItems_ChecklistId",
                table: "ChecklistItems",
                column: "ChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_Checklists_StoreId_IsArchived",
                table: "Checklists",
                columns: new[] { "StoreId", "IsArchived" });

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTasks_ChecklistDayId",
                table: "ChecklistTasks",
                column: "ChecklistDayId");

            migrationBuilder.CreateIndex(
                name: "IX_ChecklistTasks_StoreId_Done",
                table: "ChecklistTasks",
                columns: new[] { "StoreId", "Done" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChecklistItems");

            migrationBuilder.DropTable(
                name: "ChecklistTasks");

            migrationBuilder.DropTable(
                name: "ChecklistDays");

            migrationBuilder.DropTable(
                name: "Checklists");
        }
    }
}
