using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("announcements");
        builder.HasKey((Announcement a) => a.Id);

        builder.Property((Announcement a) => a.Title).HasMaxLength(300).IsRequired();
        builder.Property((Announcement a) => a.Body).HasMaxLength(10000).IsRequired();

        builder.HasIndex((Announcement a) => a.CreatedAt);

        builder.HasOne((Announcement a) => a.Author)
            .WithMany()
            .HasForeignKey((Announcement a) => a.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
