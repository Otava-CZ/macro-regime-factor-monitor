using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MacroRegimeFactorMonitor.Data.Migrations;

/// <inheritdoc />
public partial class AddDataIngestionFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_IndicatorObservations_IndicatorId_ObservationDate",
            table: "IndicatorObservations");

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAtUtc",
            table: "IndicatorObservations",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(2026, 5, 7, 13, 30, 0, DateTimeKind.Utc));

        migrationBuilder.AddColumn<int>(
            name: "DataImportRunId",
            table: "IndicatorObservations",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ExternalSeriesId",
            table: "IndicatorObservations",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Source",
            table: "IndicatorObservations",
            type: "character varying(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateOnly>(
            name: "SourceReleaseDate",
            table: "IndicatorObservations",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAtUtc",
            table: "IndicatorObservations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "VintageDate",
            table: "IndicatorObservations",
            type: "date",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "DataSources",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                SourceType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                RequiresApiKey = table.Column<bool>(type: "boolean", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                Notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataSources", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "DataImportRuns",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                DataSourceId = table.Column<int>(type: "integer", nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                FinishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                RowsRead = table.Column<int>(type: "integer", nullable: false),
                RowsInserted = table.Column<int>(type: "integer", nullable: false),
                RowsUpdated = table.Column<int>(type: "integer", nullable: false),
                ErrorMessage = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                Notes = table.Column<string>(type: "text", nullable: false, defaultValue: "")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataImportRuns", x => x.Id);
                table.ForeignKey(
                    name: "FK_DataImportRuns_DataSources_DataSourceId",
                    column: x => x.DataSourceId,
                    principalTable: "DataSources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ExternalSeries",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                IndicatorId = table.Column<int>(type: "integer", nullable: false),
                DataSourceId = table.Column<int>(type: "integer", nullable: false),
                ExternalSeriesId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Frequency = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Units = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Transform = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                ObservationDateField = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                ValueField = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                LastSuccessfulImportUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExternalSeries", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExternalSeries_DataSources_DataSourceId",
                    column: x => x.DataSourceId,
                    principalTable: "DataSources",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ExternalSeries_Indicators_IndicatorId",
                    column: x => x.IndicatorId,
                    principalTable: "Indicators",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_IndicatorObservations_DataImportRunId",
            table: "IndicatorObservations",
            column: "DataImportRunId");

        migrationBuilder.CreateIndex(
            name: "IX_IndicatorObservations_ExternalSeriesId",
            table: "IndicatorObservations",
            column: "ExternalSeriesId");

        migrationBuilder.CreateIndex(
            name: "IX_IndicatorObservations_IndicatorId_ObservationDate",
            table: "IndicatorObservations",
            columns: new[] { "IndicatorId", "ObservationDate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_DataImportRuns_DataSourceId_StartedAtUtc",
            table: "DataImportRuns",
            columns: new[] { "DataSourceId", "StartedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_DataImportRuns_Status",
            table: "DataImportRuns",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_ExternalSeries_DataSourceId",
            table: "ExternalSeries",
            column: "DataSourceId");

        migrationBuilder.CreateIndex(
            name: "IX_ExternalSeries_DataSourceId_ExternalSeriesId_IndicatorId",
            table: "ExternalSeries",
            columns: new[] { "DataSourceId", "ExternalSeriesId", "IndicatorId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExternalSeries_IndicatorId",
            table: "ExternalSeries",
            column: "IndicatorId");

        migrationBuilder.AddForeignKey(
            name: "FK_IndicatorObservations_DataImportRuns_DataImportRunId",
            table: "IndicatorObservations",
            column: "DataImportRunId",
            principalTable: "DataImportRuns",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_IndicatorObservations_ExternalSeries_ExternalSeriesId",
            table: "IndicatorObservations",
            column: "ExternalSeriesId",
            principalTable: "ExternalSeries",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_IndicatorObservations_DataImportRuns_DataImportRunId", table: "IndicatorObservations");
        migrationBuilder.DropForeignKey(name: "FK_IndicatorObservations_ExternalSeries_ExternalSeriesId", table: "IndicatorObservations");
        migrationBuilder.DropTable(name: "DataImportRuns");
        migrationBuilder.DropTable(name: "ExternalSeries");
        migrationBuilder.DropTable(name: "DataSources");
        migrationBuilder.DropIndex(name: "IX_IndicatorObservations_DataImportRunId", table: "IndicatorObservations");
        migrationBuilder.DropIndex(name: "IX_IndicatorObservations_ExternalSeriesId", table: "IndicatorObservations");
        migrationBuilder.DropIndex(name: "IX_IndicatorObservations_IndicatorId_ObservationDate", table: "IndicatorObservations");
        migrationBuilder.DropColumn(name: "CreatedAtUtc", table: "IndicatorObservations");
        migrationBuilder.DropColumn(name: "DataImportRunId", table: "IndicatorObservations");
        migrationBuilder.DropColumn(name: "ExternalSeriesId", table: "IndicatorObservations");
        migrationBuilder.DropColumn(name: "Source", table: "IndicatorObservations");
        migrationBuilder.DropColumn(name: "SourceReleaseDate", table: "IndicatorObservations");
        migrationBuilder.DropColumn(name: "UpdatedAtUtc", table: "IndicatorObservations");
        migrationBuilder.DropColumn(name: "VintageDate", table: "IndicatorObservations");

        migrationBuilder.CreateIndex(
            name: "IX_IndicatorObservations_IndicatorId_ObservationDate",
            table: "IndicatorObservations",
            columns: new[] { "IndicatorId", "ObservationDate" },
            unique: true);
    }
}
