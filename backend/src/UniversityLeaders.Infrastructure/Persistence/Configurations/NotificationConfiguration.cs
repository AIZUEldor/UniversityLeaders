using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey((Notification n) => n.Id);

        builder.Property((Notification n) => n.Title).HasMaxLength(300).IsRequired();
        builder.Property((Notification n) => n.Message).HasMaxLength(2000).IsRequired();

        builder.Property((Notification n) => n.Type).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex((Notification n) => new { n.RecipientId, n.ReadAt });
        builder.HasIndex((Notification n) => new { n.RecipientId, n.CreatedAt });

        builder.HasOne<Report>()
            .WithMany()
            .HasForeignKey((Notification n) => n.ReportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne((Notification n) => n.Recipient)
            .WithMany()
            .HasForeignKey((Notification n) => n.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
