using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gatherstead.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddErasedAccountTombstones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ErasedAccounts",
                schema: "security",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalIdHash = table.Column<byte[]>(type: "varbinary(32)", maxLength: 32, nullable: false),
                    ErasedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ErasedAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ErasedAccount_ExternalIdHash",
                schema: "security",
                table: "ErasedAccounts",
                column: "ExternalIdHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ErasedAccounts",
                schema: "security");
        }
    }
}
