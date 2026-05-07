using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MacroRegimeFactorMonitor.Data.Migrations;

/// <inheritdoc />
public partial class AddStartupSyncRuns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StartupSyncRuns",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Message = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                AppliedMigrations = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                SeededDataSources = table.Column<int>(type: "integer", nullable: false),
                SeededMacroFactors = table.Column<int>(type: "integer", nullable: false),
                SeededIndicators = table.Column<int>(type: "integer", nullable: false),
                SeededObservations = table.Column<int>(type: "integer", nullable: false),
                SeededFactorScores = table.Column<int>(type: "integer", nullable: false),
                SeededWeeklyReviews = table.Column<int>(type: "integer", nullable: false),
                SeededTradeIdeas = table.Column<int>(type: "integer", nullable: false),
                ErrorMessage = table.Column<string>(type: "text", nullable: false, defaultValue: "")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StartupSyncRuns", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StartupSyncRuns_StartedAtUtc",
            table: "StartupSyncRuns",
            column: "StartedAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_StartupSyncRuns_Status",
            table: "StartupSyncRuns",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "StartupSyncRuns");
    }
}
