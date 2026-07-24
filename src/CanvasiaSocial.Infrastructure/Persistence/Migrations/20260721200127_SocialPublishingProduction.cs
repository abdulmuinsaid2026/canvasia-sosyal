using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CanvasiaSocial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SocialPublishingProduction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductPublicationHistories_ScheduledPostId",
                table: "ProductPublicationHistories");

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OAuthStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Platform = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    EncryptedCodeVerifier = table.Column<string>(type: "text", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OAuthStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledPosts_Status_NextRetryAtUtc_ScheduledAtUtc",
                table: "ScheduledPosts",
                columns: new[] { "Status", "NextRetryAtUtc", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductPublicationHistories_ScheduledPostId",
                table: "ProductPublicationHistories",
                column: "ScheduledPostId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OAuthStates_ExpiresAtUtc",
                table: "OAuthStates",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_OAuthStates_StateHash",
                table: "OAuthStates",
                column: "StateHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "OAuthStates");

            migrationBuilder.DropIndex(
                name: "IX_ScheduledPosts_Status_NextRetryAtUtc_ScheduledAtUtc",
                table: "ScheduledPosts");

            migrationBuilder.DropIndex(
                name: "IX_ProductPublicationHistories_ScheduledPostId",
                table: "ProductPublicationHistories");

            migrationBuilder.CreateIndex(
                name: "IX_ProductPublicationHistories_ScheduledPostId",
                table: "ProductPublicationHistories",
                column: "ScheduledPostId");
        }
    }
}
