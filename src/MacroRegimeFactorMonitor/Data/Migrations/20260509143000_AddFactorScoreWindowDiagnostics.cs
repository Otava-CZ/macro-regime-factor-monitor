using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacroRegimeFactorMonitor.Data.Migrations;

/// <inheritdoc />
public partial class AddFactorScoreWindowDiagnostics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ScoringConfidence", table: "FactorScores", type: "character varying(40)", maxLength: 40, nullable: true);
        migrationBuilder.AddColumn<string>(name: "ScoringConfidenceNotes", table: "FactorScores", type: "text", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "WindowAcceleration", table: "FactorScores", type: "numeric(18,6)", precision: 18, scale: 6, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "WindowAverageValue", table: "FactorScores", type: "numeric(18,6)", precision: 18, scale: 6, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "WindowChange", table: "FactorScores", type: "numeric(18,6)", precision: 18, scale: 6, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "WindowChangePercent", table: "FactorScores", type: "numeric(18,6)", precision: 18, scale: 6, nullable: true);
        migrationBuilder.AddColumn<DateOnly>(name: "WindowEndDate", table: "FactorScores", type: "date", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "WindowFirstValue", table: "FactorScores", type: "numeric(18,6)", precision: 18, scale: 6, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "WindowLastValue", table: "FactorScores", type: "numeric(18,6)", precision: 18, scale: 6, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "WindowMaxValue", table: "FactorScores", type: "numeric(18,6)", precision: 18, scale: 6, nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "WindowMinValue", table: "FactorScores", type: "numeric(18,6)", precision: 18, scale: 6, nullable: true);
        migrationBuilder.AddColumn<int>(name: "WindowObservationCount", table: "FactorScores", type: "integer", nullable: true);
        migrationBuilder.AddColumn<decimal>(name: "WindowSlope", table: "FactorScores", type: "numeric(18,6)", precision: 18, scale: 6, nullable: true);
        migrationBuilder.AddColumn<DateOnly>(name: "WindowStartDate", table: "FactorScores", type: "date", nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ScoringConfidence", table: "FactorScores");
        migrationBuilder.DropColumn(name: "ScoringConfidenceNotes", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowAcceleration", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowAverageValue", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowChange", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowChangePercent", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowEndDate", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowFirstValue", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowLastValue", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowMaxValue", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowMinValue", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowObservationCount", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowSlope", table: "FactorScores");
        migrationBuilder.DropColumn(name: "WindowStartDate", table: "FactorScores");
    }
}
