using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppDemo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChatMessageStatusAndTemplateName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TemplateName",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "TemplateName",
                table: "ChatMessages");
        }
    }
}
