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
            // === Add new columns first (before the data migration + drops below) ===
            // Source defaults to 0 (IntentSource.Volunteered) for all existing rows.
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

            // === Data migration (runs after adds, before drops) ===
            // Intents: withdrawal now deletes the row, so a persisted Volunteered=0 row is a stale
            // withdrawal — soft-delete it. Rows with Volunteered=1 keep the default Source=Volunteered.
            migrationBuilder.Sql(@"
UPDATE [TaskIntents] SET [IsDeleted] = 1, [DeletedAt] = SYSDATETIMEOFFSET()
WHERE [Volunteered] = 0 AND [IsDeleted] = 0;");
            migrationBuilder.Sql(@"
UPDATE [MealIntents] SET [IsDeleted] = 1, [DeletedAt] = SYSDATETIMEOFFSET()
WHERE [Volunteered] = 0 AND [IsDeleted] = 0;");

            // AccommodationIntents: fold the old Decision into the merged Status enum. Old Decision
            // values Pending=0/Approved=1/Declined=2; a Declined decision becomes Status=Declined(3).
            // Non-declined rows keep their Status (old Intent=0/Hold=1/Confirmed=2 map 1:1 onto
            // Requested=0/Hold=1/Confirmed=2), so no other update is needed.
            migrationBuilder.Sql(@"
UPDATE [AccommodationIntents] SET [Status] = 3 WHERE [Decision] = 2;");

            // HouseholdMembers: preserve the adult signal where derivable before dropping IsAdult.
            // AgeBand is persisted as its enum NAME (string conversion), so set 'Age18To64'. Child rows
            // (IsAdult=0) without a band can't be mapped to a specific child band — accepted data loss.
            migrationBuilder.Sql(@"
UPDATE [HouseholdMembers] SET [AgeBand] = 'Age18To64'
WHERE [IsAdult] = 1 AND [AgeBand] IS NULL AND [BirthDate] IS NULL;");

            // === Drop replaced/dead columns (after the data migration above) ===
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
            // Re-adds the dropped columns but does NOT restore their data: the Decision→Status fold,
            // the IsAdult→AgeBand backfill, and the stale-withdrawal soft-deletes are one-way.
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
