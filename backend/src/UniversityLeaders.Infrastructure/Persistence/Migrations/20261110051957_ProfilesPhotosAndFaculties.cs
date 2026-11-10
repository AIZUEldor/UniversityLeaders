using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityLeaders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProfilesPhotosAndFaculties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "phone_is_public",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "photo_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "faculties",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "profile_photos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    content_type = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_profile_photos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_photo_id",
                table: "users",
                column: "photo_id",
                unique: true,
                filter: "photo_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_users_profile_photos_photo_id",
                table: "users",
                column: "photo_id",
                principalTable: "profile_photos",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
            // The 13 faculties confirmed by the client, in display order (ids follow insertion order on an empty table).
            // Idempotent: a faculty is inserted only when no faculty with that name exists (case-insensitive), and a
            // description is filled only when empty. Existing faculties, leaders and reports are never changed or deleted.
            migrationBuilder.Sql(@"
INSERT INTO faculties (name, description) SELECT 'Matematika fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Matematika fakulteti'));
INSERT INTO faculties (name, description) SELECT 'Amaliy matematika va intellektual texnologiyalar fakulteti', 'Sun''iy intellekt va axborot xavfsizligi yo‘nalishlari.' WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Amaliy matematika va intellektual texnologiyalar fakulteti'));
UPDATE faculties SET description = 'Sun''iy intellekt va axborot xavfsizligi yo‘nalishlari.' WHERE lower(name) = lower('Amaliy matematika va intellektual texnologiyalar fakulteti') AND description IS NULL;
INSERT INTO faculties (name, description) SELECT 'Fizika fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Fizika fakulteti'));
INSERT INTO faculties (name, description) SELECT 'Kimyo fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Kimyo fakulteti'));
INSERT INTO faculties (name, description) SELECT 'Biologiya va ekologiya fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Biologiya va ekologiya fakulteti'));
INSERT INTO faculties (name, description) SELECT 'Geologiya va muhandislik geologiyasi fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Geologiya va muhandislik geologiyasi fakulteti'));
INSERT INTO faculties (name, description) SELECT 'Geografiya va tabiiy resurslar fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Geografiya va tabiiy resurslar fakulteti'));
INSERT INTO faculties (name, description) SELECT 'Tarix fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Tarix fakulteti'));
INSERT INTO faculties (name, description) SELECT 'Iqtisodiyot fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Iqtisodiyot fakulteti'));
INSERT INTO faculties (name, description) SELECT 'Ijtimoiy fanlar fakulteti', 'Falsafa, sotsiologiya, psixologiya va siyosatshunoslik.' WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Ijtimoiy fanlar fakulteti'));
UPDATE faculties SET description = 'Falsafa, sotsiologiya, psixologiya va siyosatshunoslik.' WHERE lower(name) = lower('Ijtimoiy fanlar fakulteti') AND description IS NULL;
INSERT INTO faculties (name, description) SELECT 'Xorijiy filologiya fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Xorijiy filologiya fakulteti'));
INSERT INTO faculties (name, description) SELECT 'Jurnalistika va oʻzbek filologiyasi fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Jurnalistika va oʻzbek filologiyasi fakulteti'));
INSERT INTO faculties (name, description) SELECT 'Taekvondo va sport faoliyati fakulteti', NULL WHERE NOT EXISTS (SELECT 1 FROM faculties WHERE lower(name) = lower('Taekvondo va sport faoliyati fakulteti'));
");

            // Coordinators become profiles without a login: drop any credentials and end their sessions. The rows, their
            // reports, reviews and audit history stay untouched.
            migrationBuilder.Sql(@"
UPDATE users
SET password_hash = NULL, must_change_password = FALSE, failed_login_count = 0, lockout_ends_at = NULL,
    security_stamp = md5(random()::text || clock_timestamp()::text) || md5(random()::text || id::text)
WHERE role IN ('FacultyCoordinator', 'UniversityCoordinator');
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data steps are not reverted: seeded faculties and removed coordinator credentials stay as they are.
            migrationBuilder.DropForeignKey(
                name: "fk_users_profile_photos_photo_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "profile_photos");

            migrationBuilder.DropIndex(
                name: "ix_users_photo_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "users");

            migrationBuilder.DropColumn(
                name: "phone_is_public",
                table: "users");

            migrationBuilder.DropColumn(
                name: "photo_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "description",
                table: "faculties");
        }
    }
}
