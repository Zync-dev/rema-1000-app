using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rema.App.Data.Migrations
{
    /// <inheritdoc />
    public partial class FloorBoxSizesRotationSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrintHeadline",
                table: "FloorPlans",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintNotes",
                table: "FloorPlans",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OfferB",
                table: "FloorBoxes",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Split",
                table: "FloorBoxes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            // Omdøb de gamle typer til de nye faste typer.
            migrationBuilder.Sql("""
                UPDATE "FloorBoxes" SET "Kind" = CASE "Kind"
                    WHEN 'Palle'       THEN 'FuldPalle'
                    WHEN 'Halvpalle'   THEN 'HalvPalle'
                    WHEN 'Gondolender' THEN 'Endeboks'
                    WHEN 'Bordplads'   THEN 'Skraabord'
                    WHEN 'Stakke'      THEN 'Andet'
                    WHEN 'Koel'        THEN 'Andet'
                    ELSE 'Andet'
                END
                WHERE "Kind" NOT IN ('FuldPalle','HalvPalle','KvartPalle','Skraabord','Endeboks','Andet');
            """);

            // Sæt de faste typer til deres rigtige fysiske størrelse (bevar orientering).
            migrationBuilder.Sql("""
                UPDATE "FloorBoxes" SET
                    "Width"  = CASE WHEN "Height" > "Width" THEN d.h ELSE d.w END,
                    "Height" = CASE WHEN "Height" > "Width" THEN d.w ELSE d.h END
                FROM (VALUES
                    ('FuldPalle', 120, 80),
                    ('HalvPalle',  80, 60),
                    ('KvartPalle', 60, 40),
                    ('Skraabord', 240, 80),
                    ('Endeboks',  133, 90)
                ) AS d(kind, w, h)
                WHERE "FloorBoxes"."Kind" = d.kind;
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrintHeadline",
                table: "FloorPlans");

            migrationBuilder.DropColumn(
                name: "PrintNotes",
                table: "FloorPlans");

            migrationBuilder.DropColumn(
                name: "OfferB",
                table: "FloorBoxes");

            migrationBuilder.DropColumn(
                name: "Split",
                table: "FloorBoxes");
        }
    }
}
