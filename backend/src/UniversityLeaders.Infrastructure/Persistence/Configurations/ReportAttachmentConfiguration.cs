using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class ReportAttachmentConfiguration : IEntityTypeConfiguration<ReportAttachment>
{
    public void Configure(EntityTypeBuilder<ReportAttachment> builder)
    {
        builder.ToTable("report_attachments", (TableBuilder<ReportAttachment> table) =>
            table.HasCheckConstraint("ck_report_attachments_size_bytes", "size_bytes > 0"));

        builder.HasKey((ReportAttachment a) => a.Id);

        builder.Property((ReportAttachment a) => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property((ReportAttachment a) => a.ContentType).HasMaxLength(127).IsRequired();
        builder.Property((ReportAttachment a) => a.StorageKey).HasMaxLength(512).IsRequired();
        builder.HasIndex((ReportAttachment a) => a.StorageKey).IsUnique();

        builder.HasOne((ReportAttachment a) => a.Report)
            .WithMany((Report r) => r.Attachments)
            .HasForeignKey((ReportAttachment a) => a.ReportId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne((ReportAttachment a) => a.UploadedBy)
            .WithMany()
            .HasForeignKey((ReportAttachment a) => a.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
