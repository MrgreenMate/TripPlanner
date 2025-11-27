using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TripPlanner.Api.Migrations
{
    /// <inheritdoc />
    public partial class ItineraryEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Itineraries_Users_UserId",
                table: "Itineraries");

            migrationBuilder.DropIndex(
                name: "IX_Itineraries_UserId",
                table: "Itineraries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Itineraries_UserId",
                table: "Itineraries",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Itineraries_Users_UserId",
                table: "Itineraries",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
