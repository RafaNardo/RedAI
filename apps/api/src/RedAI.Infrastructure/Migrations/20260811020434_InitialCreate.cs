using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RedAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "brand_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileJson = table.Column<string>(type: "text", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "brand_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    OriginalFilename = table.Column<string>(type: "text", nullable: true),
                    StorageKey = table.Column<string>(type: "text", nullable: true),
                    MimeType = table.Column<string>(type: "text", nullable: true),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_brand_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "campaign_strategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrategyJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "content_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceIdeaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ApprovedRevisionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "content_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    Caption = table.Column<string>(type: "text", nullable: false),
                    SupportingText = table.Column<string>(type: "text", nullable: true),
                    Cta = table.Column<string>(type: "text", nullable: true),
                    HashtagsJson = table.Column<string>(type: "text", nullable: false),
                    VisualDirection = table.Column<string>(type: "text", nullable: true),
                    Instruction = table.Column<string>(type: "text", nullable: true),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_revisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creative_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    SourceContentRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LayoutJson = table.Column<string>(type: "text", nullable: false),
                    ImageStorageKey = table.Column<string>(type: "text", nullable: true),
                    ThumbnailStorageKey = table.Column<string>(type: "text", nullable: true),
                    RevisionInstruction = table.Column<string>(type: "text", nullable: true),
                    IsSelected = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creative_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    InstagramHandle = table.Column<string>(type: "text", nullable: true),
                    WebsiteUrl = table.Column<string>(type: "text", nullable: true),
                    ManualContext = table.Column<string>(type: "text", nullable: true),
                    CurrentStep = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Objective = table.Column<string>(type: "text", nullable: false),
                    TargetCount = table.Column<int>(type: "integer", nullable: false),
                    Context = table.Column<string>(type: "text", nullable: true),
                    StrategyApproved = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaigns_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "content_ideas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Pillar = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Selected = table.Column<bool>(type: "boolean", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_ideas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_content_ideas_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_brand_profiles_ProjectId",
                table: "brand_profiles",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaign_strategies_CampaignId",
                table: "campaign_strategies",
                column: "CampaignId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_ProjectId",
                table: "campaigns",
                column: "ProjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_ideas_CampaignId",
                table: "content_ideas",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_content_ideas_Ordinal_Id",
                table: "content_ideas",
                columns: new[] { "Ordinal", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_content_items_CampaignId_Sequence",
                table: "content_items",
                columns: new[] { "CampaignId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_content_revisions_ContentItemId_Version",
                table: "content_revisions",
                columns: new[] { "ContentItemId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_creative_versions_ContentItemId_Version",
                table: "creative_versions",
                columns: new[] { "ContentItemId", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "brand_profiles");

            migrationBuilder.DropTable(
                name: "brand_sources");

            migrationBuilder.DropTable(
                name: "campaign_strategies");

            migrationBuilder.DropTable(
                name: "content_ideas");

            migrationBuilder.DropTable(
                name: "content_items");

            migrationBuilder.DropTable(
                name: "content_revisions");

            migrationBuilder.DropTable(
                name: "creative_versions");

            migrationBuilder.DropTable(
                name: "campaigns");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
