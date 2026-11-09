using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;
using UniversityLeaders.Domain.Enums;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class ReportReviewConfiguration : IEntityTypeConfiguration<ReportReview>
{
    public void Configure(EntityTypeBuilder<ReportReview> builder)
    {
        builder.ToTable("report_reviews", (TableBuilder<ReportReview> table) =>
        {
            table.HasCheckConstraint("ck_report_reviews_score", "score IS NULL OR score BETWEEN 0 AND 10");
            table.HasCheckConstraint("ck_report_reviews_score_when_approved", $"(action = '{nameof(ReviewAction.Approved)}') = (score IS NOT NULL)");
            table.HasCheckConstraint("ck_report_reviews_rejection_comment", $"action <> '{nameof(ReviewAction.Rejected)}' OR comment IS NOT NULL");
        });
        builder.HasKey((ReportReview r) => r.Id);

        builder.Property((ReportReview r) => r.Action).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property((ReportReview r) => r.Comment).HasMaxLength(2000);
        builder.Property((ReportReview r) => r.ScoreBreakdown).HasColumnType("jsonb");

        builder.HasIndex((ReportReview r) => new { r.ReportId, r.CreatedAt });
        builder.HasIndex((ReportReview r) => r.ReviewerId);

        builder.HasOne((ReportReview r) => r.Report)
            .WithMany((Report r) => r.Reviews)
            .HasForeignKey((ReportReview r) => r.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne((ReportReview r) => r.Reviewer)
            .WithMany()
            .HasForeignKey((ReportReview r) => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
