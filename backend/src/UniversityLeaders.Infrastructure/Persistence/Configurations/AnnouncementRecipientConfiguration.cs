using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class AnnouncementRecipientConfiguration : IEntityTypeConfiguration<AnnouncementRecipient>
{
    public void Configure(EntityTypeBuilder<AnnouncementRecipient> builder)
    {
        builder.ToTable("announcement_recipients");

        // Composite key: an announcement is delivered to each recipient at most once.
        builder.HasKey((AnnouncementRecipient r) => new { r.AnnouncementId, r.RecipientId });
        builder.HasIndex((AnnouncementRecipient r) => new { r.RecipientId, r.ReadAt });

        // Deliveries belong to their announcement; users are never deleted (soft deactivation).
        builder.HasOne((AnnouncementRecipient r) => r.Announcement)
            .WithMany((Announcement a) => a.Recipients)
            .HasForeignKey((AnnouncementRecipient r) => r.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne((AnnouncementRecipient r) => r.Recipient)
            .WithMany()
            .HasForeignKey((AnnouncementRecipient r) => r.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
