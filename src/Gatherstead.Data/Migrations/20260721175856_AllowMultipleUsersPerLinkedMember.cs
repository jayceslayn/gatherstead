using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gatherstead.Data.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleUsersPerLinkedMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantUser_LinkedMemberId",
                table: "TenantUsers");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUser_LinkedMemberId",
                table: "TenantUsers",
                column: "LinkedMemberId",
                filter: "[LinkedMemberId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TenantUser_LinkedMemberId",
                table: "TenantUsers");

            migrationBuilder.CreateIndex(
                name: "IX_TenantUser_LinkedMemberId",
                table: "TenantUsers",
                column: "LinkedMemberId",
                unique: true,
                filter: "[LinkedMemberId] IS NOT NULL");
        }
    }
}
