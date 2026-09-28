using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppDemo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessNumberTokenAndTestFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessToken",
                table: "WhatsAppBusinessNumbers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTestNumber",
                table: "WhatsAppBusinessNumbers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessToken",
                table: "WhatsAppBusinessNumbers");

            migrationBuilder.DropColumn(
                name: "IsTestNumber",
                table: "WhatsAppBusinessNumbers");
        }
    }
}
