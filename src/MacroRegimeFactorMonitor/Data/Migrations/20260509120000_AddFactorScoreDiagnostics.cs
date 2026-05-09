using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MacroRegimeFactorMonitor.Data.Migrations;

/// <inheritdoc />
public partial class AddFactorScoreDiagnostics : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DataQualityNotes",
            table: "FactorScores",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DataQualityStatus",
            table: "FactorScores",
            type: "character varying(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DaysSinceSourceObservation",
            table: "FactorScores",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "ObservationChange",
            table: "FactorScores",
            type: "numeric(18,6)",
            precision: 18,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "ObservationChangePercent",
            table: "FactorScores",
            type: "numeric(18,6)",
            precision: 18,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "PreviousObservationDate",
            table: "FactorScores",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "PreviousObservationValue",
            table: "FactorScores",
            type: "numeric(18,6)",
            precision: 18,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "SourceObservationDate",
            table: "FactorScores",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "SourceObservationValue",
            table: "FactorScores",
            type: "numeric(18,6)",
            precision: 18,
            scale: 6,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "DataQualityNotes", table: "FactorScores");
        migrationBuilder.DropColumn(name: "DataQualityStatus", table: "FactorScores");
        migrationBuilder.DropColumn(name: "DaysSinceSourceObservation", table: "FactorScores");
        migrationBuilder.DropColumn(name: "ObservationChange", table: "FactorScores");
        migrationBuilder.DropColumn(name: "ObservationChangePercent", table: "FactorScores");
        migrationBuilder.DropColumn(name: "PreviousObservationDate", table: "FactorScores");
        migrationBuilder.DropColumn(name: "PreviousObservationValue", table: "FactorScores");
        migrationBuilder.DropColumn(name: "SourceObservationDate", table: "FactorScores");
        migrationBuilder.DropColumn(name: "SourceObservationValue", table: "FactorScores");
    }
}
