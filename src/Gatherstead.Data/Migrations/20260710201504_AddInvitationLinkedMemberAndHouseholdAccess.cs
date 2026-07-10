using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gatherstead.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationLinkedMemberAndHouseholdAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // New optional member link on invitations. Added as a fresh column — it is NOT a rename
            // of HouseholdId (they are semantically different: a member id vs a household id).
            migrationBuilder.AddColumn<Guid>(
                name: "LinkedMemberId",
                table: "Invitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InvitationHouseholdAccess",
                columns: table => new
                {
                    InvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_InvitationHouseholdAccess", x => new { x.InvitationId, x.HouseholdId });
                    table.ForeignKey(
                        name: "FK_InvitationHouseholdAccess_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvitationHouseholdAccess_Invitations_InvitationId",
                        column: x => x.InvitationId,
                        principalTable: "Invitations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvitationHouseholdAccess_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "InvitationHouseholdAccessHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationHouseholdAccess_HouseholdId",
                table: "InvitationHouseholdAccess",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationHouseholdAccess_TenantId",
                table: "InvitationHouseholdAccess",
                column: "TenantId");

            // Preserve existing single-grant invitations by copying them into the new child table,
            // inheriting the invitation's own audit stamps. Runs before the old columns are dropped.
            // A NULL HouseholdRole was valid under the old schema and granted Member on accept
            // (`householdRole ?? HouseholdRole.Member`), so coalesce to Member (1) rather than
            // dropping those rows and losing the grant.
            migrationBuilder.Sql(@"
                INSERT INTO [InvitationHouseholdAccess]
                    ([InvitationId], [HouseholdId], [TenantId], [Role], [CreatedByUserId], [CreatedAt], [UpdatedByUserId], [UpdatedAt], [IsDeleted])
                SELECT [Id], [HouseholdId], [TenantId], COALESCE([HouseholdRole], 1), [CreatedByUserId], [CreatedAt], [UpdatedByUserId], [UpdatedAt], 0
                FROM [Invitations]
                WHERE [HouseholdId] IS NOT NULL;");

            // Remove the old single-grant columns now that their data is preserved.
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_Households_HouseholdId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_HouseholdId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "HouseholdId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "HouseholdRole",
                table: "Invitations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HouseholdId",
                table: "Invitations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HouseholdRole",
                table: "Invitations",
                type: "int",
                nullable: true);

            // Best-effort restore of the single-grant columns from the first grant per invitation
            // before the multi-grant table is dropped. Additional grants cannot be represented.
            migrationBuilder.Sql(@"
                UPDATE inv
                SET inv.[HouseholdId] = g.[HouseholdId], inv.[HouseholdRole] = g.[Role]
                FROM [Invitations] inv
                CROSS APPLY (
                    SELECT TOP 1 [HouseholdId], [Role]
                    FROM [InvitationHouseholdAccess] x
                    WHERE x.[InvitationId] = inv.[Id] AND x.[IsDeleted] = 0
                    ORDER BY [HouseholdId]
                ) g;");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_HouseholdId",
                table: "Invitations",
                column: "HouseholdId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_Households_HouseholdId",
                table: "Invitations",
                column: "HouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropColumn(
                name: "LinkedMemberId",
                table: "Invitations");

            migrationBuilder.DropTable(
                name: "InvitationHouseholdAccess")
                .Annotation("SqlServer:IsTemporal", true)
                .Annotation("SqlServer:TemporalHistoryTableName", "InvitationHouseholdAccessHistory")
                .Annotation("SqlServer:TemporalHistoryTableSchema", null)
                .Annotation("SqlServer:TemporalPeriodEndColumnName", "PeriodEnd")
                .Annotation("SqlServer:TemporalPeriodStartColumnName", "PeriodStart");
        }
    }
}
