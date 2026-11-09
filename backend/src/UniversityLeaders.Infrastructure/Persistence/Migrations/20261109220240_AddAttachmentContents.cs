using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityLeaders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAttachmentContents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "attachment_contents",
                columns: table => new
                {
                    storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachment_contents", x => x.storage_key);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attachment_contents");
        }
    }
}
