using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppDemo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppBusinessNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WhatsAppBusinessNumberId",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WhatsAppBusinessNumbers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SalesUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhoneNumberId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayPhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WabaId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppBusinessNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppBusinessNumbers_SalesUsers_SalesUserId",
                        column: x => x.SalesUserId,
                        principalTable: "SalesUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AssignedUserId",
                table: "Customers",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_AssignedUserId",
                table: "Conversations",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_CustomerId",
                table: "Conversations",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_WhatsAppBusinessNumberId",
                table: "Conversations",
                column: "WhatsAppBusinessNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationId",
                table: "ChatMessages",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBusinessNumbers_PhoneNumberId",
                table: "WhatsAppBusinessNumbers",
                column: "PhoneNumberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppBusinessNumbers_SalesUserId",
                table: "WhatsAppBusinessNumbers",
                column: "SalesUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_Conversations_ConversationId",
                table: "ChatMessages",
                column: "ConversationId",
                principalTable: "Conversations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_Customers_CustomerId",
                table: "Conversations",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_SalesUsers_AssignedUserId",
                table: "Conversations",
                column: "AssignedUserId",
                principalTable: "SalesUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_WhatsAppBusinessNumbers_WhatsAppBusinessNumberId",
                table: "Conversations",
                column: "WhatsAppBusinessNumberId",
                principalTable: "WhatsAppBusinessNumbers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_SalesUsers_AssignedUserId",
                table: "Customers",
                column: "AssignedUserId",
                principalTable: "SalesUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_Conversations_ConversationId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_Customers_CustomerId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_SalesUsers_AssignedUserId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_WhatsAppBusinessNumbers_WhatsAppBusinessNumberId",
                table: "Conversations");

            migrationBuilder.DropForeignKey(
                name: "FK_Customers_SalesUsers_AssignedUserId",
                table: "Customers");

            migrationBuilder.DropTable(
                name: "WhatsAppBusinessNumbers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_AssignedUserId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_AssignedUserId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_CustomerId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_WhatsAppBusinessNumberId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ConversationId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "WhatsAppBusinessNumberId",
                table: "Conversations");
        }
    }
}
