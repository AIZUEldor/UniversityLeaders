using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityLeaders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "report_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "notifications",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_recipient_id_created_at",
                table: "notifications",
                columns: new[] { "recipient_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_report_id",
                table: "notifications",
                column: "report_id");

            migrationBuilder.AddForeignKey(
                name: "fk_notifications_reports_report_id",
                table: "notifications",
                column: "report_id",
                principalTable: "reports",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notifications_reports_report_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_recipient_id_created_at",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_report_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "report_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "type",
                table: "notifications");
        }
    }
}
