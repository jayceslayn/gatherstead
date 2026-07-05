using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gatherstead.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccommodationIntentUniqueStayIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AccommodationIntent_UniqueStay",
                table: "AccommodationIntents",
                columns: new[] { "TenantId", "AccommodationId", "HouseholdMemberId", "StartNight", "EndNight" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccommodationIntent_UniqueStay",
                table: "AccommodationIntents");
        }
    }
}
