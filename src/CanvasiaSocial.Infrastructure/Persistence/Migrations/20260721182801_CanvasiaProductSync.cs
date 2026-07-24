using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanvasiaSocial.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CanvasiaProductSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDiscounted",
                table: "ProductCaches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CanvasiaSyncStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LastStartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSuccessfulAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProcessedProductCount = table.Column<int>(type: "integer", nullable: false),
                    SourceProductCount = table.Column<int>(type: "integer", nullable: true),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasiaSyncStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductCaches_CategoryName",
                table: "ProductCaches",
                column: "CategoryName");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCaches_InStock_IsDiscounted",
                table: "ProductCaches",
                columns: new[] { "InStock", "IsDiscounted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanvasiaSyncStates");

            migrationBuilder.DropIndex(
                name: "IX_ProductCaches_CategoryName",
                table: "ProductCaches");

            migrationBuilder.DropIndex(
                name: "IX_ProductCaches_InStock_IsDiscounted",
                table: "ProductCaches");

            migrationBuilder.DropColumn(
                name: "IsDiscounted",
                table: "ProductCaches");
        }
    }
}
