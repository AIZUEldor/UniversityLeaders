using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniversityLeaders.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetireUniversityCoordinators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // University coordinators are retired. Soft-deactivate any still active (rows, reports, reviews and audit stay) and
            // record it in the audit log, like a deactivation by the University Leader would be. No-op when none exist.
            migrationBuilder.Sql(@"
WITH retired AS (
    UPDATE users
    SET is_active = FALSE, deactivated_at = now(), security_stamp = md5(random()::text || clock_timestamp()::text) || md5(random()::text || id::text)
    WHERE role = 'UniversityCoordinator' AND is_active
    RETURNING id, user_name, direction_id
)
INSERT INTO audit_logs (actor_id, action, entity_type, entity_id, details, occurred_at)
SELECT NULL, 'user.deactivated', 'User', id::text,
       jsonb_build_object('UserName', user_name, 'Role', 'UniversityCoordinator', 'DirectionId', direction_id, 'Reason', 'University coordinators retired'),
       now()
FROM retired;
");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reverted: deactivations are history and stay recorded.

        }
    }
}
