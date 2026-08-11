using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignRouteDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreativeAngle",
                table: "content_ideas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Promise",
                table: "content_ideas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetAudience",
                table: "content_ideas",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VisualDirection",
                table: "content_ideas",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreativeAngle",
                table: "content_ideas");

            migrationBuilder.DropColumn(
                name: "Promise",
                table: "content_ideas");

            migrationBuilder.DropColumn(
                name: "TargetAudience",
                table: "content_ideas");

            migrationBuilder.DropColumn(
                name: "VisualDirection",
                table: "content_ideas");
        }
    }
}
