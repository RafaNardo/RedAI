using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistentJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_content_ideas_campaigns_CampaignId",
                table: "content_ideas");

            migrationBuilder.DropIndex(
                name: "IX_content_ideas_CampaignId",
                table: "content_ideas");

            migrationBuilder.DropIndex(
                name: "IX_content_ideas_Ordinal_Id",
                table: "content_ideas");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "projects",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignId",
                table: "content_ideas",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "campaigns",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "ai_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    InputJson = table.Column<string>(type: "text", nullable: false),
                    OutputJson = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    CompletedSteps = table.Column<int>(type: "integer", nullable: false),
                    TotalSteps = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_content_ideas_CampaignId_Ordinal",
                table: "content_ideas",
                columns: new[] { "CampaignId", "Ordinal" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_content_ideas_campaigns_CampaignId",
                table: "content_ideas",
                column: "CampaignId",
                principalTable: "campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_content_ideas_campaigns_CampaignId",
                table: "content_ideas");

            migrationBuilder.DropTable(
                name: "ai_runs");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropIndex(
                name: "IX_content_ideas_CampaignId_Ordinal",
                table: "content_ideas");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "projects",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<Guid>(
                name: "CampaignId",
                table: "content_ideas",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "campaigns",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_content_ideas_CampaignId",
                table: "content_ideas",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_content_ideas_Ordinal_Id",
                table: "content_ideas",
                columns: new[] { "Ordinal", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_content_ideas_campaigns_CampaignId",
                table: "content_ideas",
                column: "CampaignId",
                principalTable: "campaigns",
                principalColumn: "Id");
        }
    }
}
