using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rema.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class FacebookPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FacebookPosts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Brief = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Text = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    EditedByUser = table.Column<bool>(type: "boolean", nullable: false),
                    Model = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacebookPosts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoreAiSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiKeyProtected = table.Column<string>(type: "text", nullable: true),
                    ApiKeyHint = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Model = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Tone = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    EmojiUsage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SignOff = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Hashtags = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    OpeningHours = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    ExtraGuidance = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    CompetitionRulesText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoreAiSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FacebookStyleExamples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreAiSettingsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacebookStyleExamples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacebookStyleExamples_StoreAiSettings_StoreAiSettingsId",
                        column: x => x.StoreAiSettingsId,
                        principalTable: "StoreAiSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacebookPosts_StoreId_CreatedUtc",
                table: "FacebookPosts",
                columns: new[] { "StoreId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FacebookStyleExamples_StoreAiSettingsId",
                table: "FacebookStyleExamples",
                column: "StoreAiSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_StoreAiSettings_StoreId",
                table: "StoreAiSettings",
                column: "StoreId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacebookPosts");

            migrationBuilder.DropTable(
                name: "FacebookStyleExamples");

            migrationBuilder.DropTable(
                name: "StoreAiSettings");
        }
    }
}
