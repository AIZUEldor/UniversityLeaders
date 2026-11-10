using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class ProfilePhotoConfiguration : IEntityTypeConfiguration<ProfilePhoto>
{
    public void Configure(EntityTypeBuilder<ProfilePhoto> builder)
    {
        builder.ToTable("profile_photos");
        builder.HasKey((ProfilePhoto p) => p.Id);
        builder.Property((ProfilePhoto p) => p.Content).IsRequired();
        builder.Property((ProfilePhoto p) => p.ContentType).HasMaxLength(127).IsRequired();
    }
}
