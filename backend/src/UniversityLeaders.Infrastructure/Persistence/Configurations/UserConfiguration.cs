using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    private const string UniversityLeader = nameof(UserRole.UniversityLeader);
    private const string FacultyLeader = nameof(UserRole.FacultyLeader);
    private const string UniversityCoordinator = nameof(UserRole.UniversityCoordinator);
    private const string FacultyCoordinator = nameof(UserRole.FacultyCoordinator);

    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", (TableBuilder<User> table) =>
        {
            // Role decides placement: leaders/coordinators of a faculty need FacultyId, coordinators need DirectionId.
            table.HasCheckConstraint(
                "ck_users_role_placement",
                $"(role = '{UniversityLeader}' AND faculty_id IS NULL AND direction_id IS NULL) OR " +
                $"(role = '{FacultyLeader}' AND faculty_id IS NOT NULL AND direction_id IS NULL) OR " +
                $"(role = '{UniversityCoordinator}' AND faculty_id IS NULL AND direction_id IS NOT NULL) OR " +
                $"(role = '{FacultyCoordinator}' AND faculty_id IS NOT NULL AND direction_id IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_users_deactivation",
                "(is_active AND deactivated_at IS NULL) OR (NOT is_active AND deactivated_at IS NOT NULL)");
            table.HasCheckConstraint("ck_users_user_name_length", "char_length(user_name) BETWEEN 3 AND 64");
        });

        builder.HasKey((User u) => u.Id);

        builder.Property((User u) => u.UserName).HasColumnType("citext").IsRequired();
        builder.Property((User u) => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property((User u) => u.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property((User u) => u.PasswordHash).HasMaxLength(512);
        builder.Property((User u) => u.SecurityStamp).HasMaxLength(64).IsRequired();
        builder.Property((User u) => u.Version).IsRowVersion();
        builder.Property((User u) => u.MustChangePassword).HasDefaultValue(true);
        builder.Property((User u) => u.IsActive).HasDefaultValue(true);
        builder.Property((User u) => u.FailedLoginCount).HasDefaultValue(0);

        // citext: unique regardless of case ("Ali" and "ali" collide). Covers inactive accounts too, so names are never reused.
        builder.HasIndex((User u) => u.UserName).IsUnique();

        // Capacity rules: one ACTIVE holder per seat. Deactivated users free the seat but keep their history.
        builder.HasIndex((User u) => u.Role, "ux_users_active_university_leader")
            .HasDatabaseName("ux_users_active_university_leader")
            .IsUnique()
            .HasFilter($"role = '{UniversityLeader}' AND is_active");
        builder.HasIndex((User u) => u.FacultyId, "ux_users_active_faculty_leader")
            .HasDatabaseName("ux_users_active_faculty_leader")
            .IsUnique()
            .HasFilter($"role = '{FacultyLeader}' AND is_active");
        builder.HasIndex((User u) => u.DirectionId, "ux_users_active_university_coordinator")
            .HasDatabaseName("ux_users_active_university_coordinator")
            .IsUnique()
            .HasFilter($"role = '{UniversityCoordinator}' AND is_active");
        builder.HasIndex((User u) => new { u.FacultyId, u.DirectionId }, "ux_users_active_faculty_coordinator")
            .HasDatabaseName("ux_users_active_faculty_coordinator")
            .IsUnique()
            .HasFilter($"role = '{FacultyCoordinator}' AND is_active");

        builder.HasOne((User u) => u.Faculty).WithMany().HasForeignKey((User u) => u.FacultyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne((User u) => u.Direction).WithMany().HasForeignKey((User u) => u.DirectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
