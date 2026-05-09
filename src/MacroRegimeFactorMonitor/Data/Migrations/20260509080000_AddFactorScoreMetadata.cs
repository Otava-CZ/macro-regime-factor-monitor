using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacroRegimeFactorMonitor.Data.Migrations;

/// <inheritdoc />
public partial class AddFactorScoreMetadata : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FactorScores_MacroFactorId_ScoreDate",
            table: "FactorScores");

        migrationBuilder.AlterColumn<string>(
            name: "RegimeImpact",
            table: "FactorScores",
            type: "character varying(120)",
            maxLength: 120,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(80)",
            oldMaxLength: 80);

        migrationBuilder.AddColumn<DateTime>(
            name: "CalculatedAtUtc",
            table: "FactorScores",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CalculationNotes",
            table: "FactorScores",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DataMode",
            table: "FactorScores",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "Sample");

        migrationBuilder.AddColumn<string>(
            name: "ScoringModelVersion",
            table: "FactorScores",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true,
            defaultValue: "sample-v0");

        migrationBuilder.AddColumn<int>(
            name: "SourceObservationCount",
            table: "FactorScores",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateIndex(
            name: "IX_FactorScores_MacroFactorId_ScoreDate_DataMode_ScoringModelVersion",
            table: "FactorScores",
            columns: new[] { "MacroFactorId", "ScoreDate", "DataMode", "ScoringModelVersion" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_FactorScores_MacroFactorId_ScoreDate_DataMode_ScoringModelVersion",
            table: "FactorScores");

        migrationBuilder.DropColumn(
            name: "CalculatedAtUtc",
            table: "FactorScores");

        migrationBuilder.DropColumn(
            name: "CalculationNotes",
            table: "FactorScores");

        migrationBuilder.DropColumn(
            name: "DataMode",
            table: "FactorScores");

        migrationBuilder.DropColumn(
            name: "ScoringModelVersion",
            table: "FactorScores");

        migrationBuilder.DropColumn(
            name: "SourceObservationCount",
            table: "FactorScores");

        migrationBuilder.AlterColumn<string>(
            name: "RegimeImpact",
            table: "FactorScores",
            type: "character varying(80)",
            maxLength: 80,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(120)",
            oldMaxLength: 120);

        migrationBuilder.CreateIndex(
            name: "IX_FactorScores_MacroFactorId_ScoreDate",
            table: "FactorScores",
            columns: new[] { "MacroFactorId", "ScoreDate" },
            unique: true);
    }
}
