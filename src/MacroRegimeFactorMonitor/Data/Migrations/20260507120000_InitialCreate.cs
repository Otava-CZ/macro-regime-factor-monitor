using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MacroRegimeFactorMonitor.Data.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "MacroFactors",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Description = table.Column<string>(type: "text", nullable: false),
                Weight = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                HigherIsRiskOn = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MacroFactors", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "TradeIdeas",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                IdeaDate = table.Column<DateOnly>(type: "date", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Thesis = table.Column<string>(type: "text", nullable: false),
                Instrument = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                EntryTrigger = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Invalidation = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Catalyst = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                MaxLoss = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                TimeHorizon = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                PostMortem = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                RiskNotes = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TradeIdeas", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "WeeklyReviews",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                WeekEnding = table.Column<DateOnly>(type: "date", nullable: false),
                RegimeAssessment = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                KeyDevelopments = table.Column<string>(type: "text", nullable: false),
                RisksToWatch = table.Column<string>(type: "text", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WeeklyReviews", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "FactorScores",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                MacroFactorId = table.Column<int>(type: "integer", nullable: false),
                ScoreDate = table.Column<DateOnly>(type: "date", nullable: false),
                RawScore = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                WeightedScore = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                RegimeImpact = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Notes = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FactorScores", x => x.Id);
                table.ForeignKey(
                    name: "FK_FactorScores_MacroFactors_MacroFactorId",
                    column: x => x.MacroFactorId,
                    principalTable: "MacroFactors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "Indicators",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                MacroFactorId = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Baseline = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                Volatility = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Indicators", x => x.Id);
                table.ForeignKey(
                    name: "FK_Indicators_MacroFactors_MacroFactorId",
                    column: x => x.MacroFactorId,
                    principalTable: "MacroFactors",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "IndicatorObservations",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                IndicatorId = table.Column<int>(type: "integer", nullable: false),
                ObservationDate = table.Column<DateOnly>(type: "date", nullable: false),
                Value = table.Column<decimal>(type: "numeric(12,4)", precision: 12, scale: 4, nullable: false),
                Notes = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IndicatorObservations", x => x.Id);
                table.ForeignKey(
                    name: "FK_IndicatorObservations_Indicators_IndicatorId",
                    column: x => x.IndicatorId,
                    principalTable: "Indicators",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FactorScores_MacroFactorId_ScoreDate",
            table: "FactorScores",
            columns: new[] { "MacroFactorId", "ScoreDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_IndicatorObservations_IndicatorId_ObservationDate",
            table: "IndicatorObservations",
            columns: new[] { "IndicatorId", "ObservationDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Indicators_MacroFactorId",
            table: "Indicators",
            column: "MacroFactorId");

        migrationBuilder.CreateIndex(
            name: "IX_MacroFactors_Name",
            table: "MacroFactors",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WeeklyReviews_WeekEnding",
            table: "WeeklyReviews",
            column: "WeekEnding",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "FactorScores");
        migrationBuilder.DropTable(name: "IndicatorObservations");
        migrationBuilder.DropTable(name: "TradeIdeas");
        migrationBuilder.DropTable(name: "WeeklyReviews");
        migrationBuilder.DropTable(name: "Indicators");
        migrationBuilder.DropTable(name: "MacroFactors");
    }
}
