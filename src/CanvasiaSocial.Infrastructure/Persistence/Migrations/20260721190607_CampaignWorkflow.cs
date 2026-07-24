using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanvasiaSocial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CampaignWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SocialAccountId",
                table: "ScheduledPosts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "GeneratedContents",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "GeneratedContents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "GeneratedContents",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SocialAccountId",
                table: "Campaigns",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "IncludePrice",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IncludeProductLink",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignItemId",
                table: "AiGenerationJobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_Status_CreatedAtUtc",
                table: "Campaigns",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignItems_CampaignId_ProductCacheId",
                table: "CampaignItems",
                columns: new[] { "CampaignId", "ProductCacheId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiGenerationJobs_CampaignItemId",
                table: "AiGenerationJobs",
                column: "CampaignItemId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AiGenerationJobs_CampaignItems_CampaignItemId",
                table: "AiGenerationJobs",
                column: "CampaignItemId",
                principalTable: "CampaignItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiGenerationJobs_CampaignItems_CampaignItemId",
                table: "AiGenerationJobs");

            migrationBuilder.DropIndex(
                name: "IX_Campaigns_Status_CreatedAtUtc",
                table: "Campaigns");

            migrationBuilder.DropIndex(
                name: "IX_CampaignItems_CampaignId_ProductCacheId",
                table: "CampaignItems");

            migrationBuilder.DropIndex(
                name: "IX_AiGenerationJobs_CampaignItemId",
                table: "AiGenerationJobs");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "GeneratedContents");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "GeneratedContents");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "GeneratedContents");

            migrationBuilder.DropColumn(
                name: "IncludePrice",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "IncludeProductLink",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "CampaignItemId",
                table: "AiGenerationJobs");

            migrationBuilder.AlterColumn<Guid>(
                name: "SocialAccountId",
                table: "ScheduledPosts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "SocialAccountId",
                table: "Campaigns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
