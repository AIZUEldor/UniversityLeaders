using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports", (TableBuilder<Report> table) =>
        {
            table.HasCheckConstraint("ck_reports_score", "score IS NULL OR score BETWEEN 0 AND 10");
            // A score exists exactly for approved reports; rejection or resubmission clears it.
            table.HasCheckConstraint("ck_reports_score_when_approved", $"(status = '{nameof(ReportStatus.Approved)}') = (score IS NOT NULL)");
            table.HasCheckConstraint("ck_reports_participant_count", "participant_count IS NULL OR participant_count >= 0");
            table.HasCheckConstraint(
                "ck_reports_submitted_at",
                $"(status = '{nameof(ReportStatus.Draft)}') = (submitted_at IS NULL)");
        });

        builder.HasKey((Report r) => r.Id);

        builder.Property((Report r) => r.Title).HasMaxLength(300).IsRequired();
        builder.Property((Report r) => r.Description).HasMaxLength(4000);
        builder.Property((Report r) => r.Outcome).HasMaxLength(4000);
        builder.Property((Report r) => r.Status).HasConversion<string>().HasMaxLength(16).IsRequired();

        // PostgreSQL xmin system column as optimistic concurrency token: of two concurrent approvals, the second fails.
        builder.Property((Report r) => r.Version).IsRowVersion();

        // Ranking and completed work read approved reports per faculty; dashboards read by author.
        builder.HasIndex((Report r) => new { r.FacultyId, r.Status });
        builder.HasIndex((Report r) => new { r.AuthorId, r.Status });
        builder.HasIndex((Report r) => new { r.Status, r.SubmittedAt });

        // History is never cascade-deleted.
        builder.HasOne((Report r) => r.Author).WithMany().HasForeignKey((Report r) => r.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne((Report r) => r.Faculty).WithMany().HasForeignKey((Report r) => r.FacultyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne((Report r) => r.Direction).WithMany().HasForeignKey((Report r) => r.DirectionId).OnDelete(DeleteBehavior.Restrict);
    }
}
