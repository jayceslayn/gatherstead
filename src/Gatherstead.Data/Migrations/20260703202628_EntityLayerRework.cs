using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gatherstead.Data.Migrations
{
    /// <inheritdoc />
    public partial class EntityLayerRework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // === Add new columns (before the drops below) ===
            // No data backfill is performed: this reworks pre-go-live schema with no legacy production
            // data to migrate, and the CI migration identity is deliberately no-read (see ci-grant.sql),
            // so WHERE-filtered backfill UPDATEs can't run under it. Dev/demo databases are recreated
            // from scratch. Source defaults to 0 (IntentSource.Volunteered) for all rows.
            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "TaskIntents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "MealIntents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "AreaSqMeters",
                table: "Accommodations",
                type: "decimal(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DepthMeters",
                table: "Accommodations",
                type: "decimal(6,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WidthMeters",
                table: "Accommodations",
                type: "decimal(6,2)",
                nullable: true);

            // === Drop replaced/dead columns ===
            migrationBuilder.DropColumn(
                name: "Volunteered",
                table: "TaskIntents");

            migrationBuilder.DropColumn(
                name: "Volunteered",
                table: "MealIntents");

            migrationBuilder.DropColumn(
                name: "IsAdult",
                table: "HouseholdMembers");

            migrationBuilder.DropColumn(
                name: "ArrivalWindowEnd",
                table: "EventAttendances");

            migrationBuilder.DropColumn(
                name: "ArrivalWindowStart",
                table: "EventAttendances");

            migrationBuilder.DropColumn(
                name: "DepartureWindowEnd",
                table: "EventAttendances");

            migrationBuilder.DropColumn(
                name: "DepartureWindowStart",
                table: "EventAttendances");

            migrationBuilder.DropColumn(
                name: "CapacityAdults",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "CapacityChildren",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "Decision",
                table: "AccommodationIntents");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "AccommodationIntents");

            migrationBuilder.CreateTable(
                name: "AccommodationBeds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccommodationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodEndColumn", true),
                    PeriodStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                        .Annotation("SqlServer:TemporalIsPeriodStartColumn", true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationBeds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccommodationBeds_Accommodations_AccommodationId",
                        column: x => x.AccommodationId,
                        principalTable: "Accommodations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccommodationBeds_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AccommodationBedsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationBeds_AccommodationId",
                table: "AccommodationBeds",
                column: "AccommodationId");

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationBeds_TenantId_AccommodationId",
                table: "AccommodationBeds",
                columns: new[] { "TenantId", "AccommodationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationBeds_TenantId_AccommodationId_Size",
                table: "AccommodationBeds",
                columns: new[] { "TenantId", "AccommodationId", "Size" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-adds the dropped columns as empty (Up performed no data backfill, so there is
            // nothing to restore); the merged-enum reworks are one-way at the data level.
            migrationBuilder.DropTable(
                name: "AccommodationBeds")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "AccommodationBedsHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "TaskIntents");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "MealIntents");

            migrationBuilder.DropColumn(
                name: "AreaSqMeters",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "DepthMeters",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "WidthMeters",
                table: "Accommodations");

            migrationBuilder.AddColumn<bool>(
                name: "Volunteered",
                table: "TaskIntents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Volunteered",
                table: "MealIntents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdult",
                table: "HouseholdMembers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArrivalWindowEnd",
                table: "EventAttendances",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArrivalWindowStart",
                table: "EventAttendances",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DepartureWindowEnd",
                table: "EventAttendances",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DepartureWindowStart",
                table: "EventAttendances",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CapacityAdults",
                table: "Accommodations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CapacityChildren",
                table: "Accommodations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Decision",
                table: "AccommodationIntents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "AccommodationIntents",
                type: "int",
                nullable: true);
        }
    }
}
