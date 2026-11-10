using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class FacultyConfiguration : IEntityTypeConfiguration<Faculty>
{
    public void Configure(EntityTypeBuilder<Faculty> builder)
    {
        builder.ToTable("faculties");
        builder.HasKey((Faculty f) => f.Id);
        builder.Property((Faculty f) => f.Name).HasMaxLength(200).IsRequired();
        builder.Property((Faculty f) => f.Description).HasMaxLength(500);
        builder.Property((Faculty f) => f.Version).IsRowVersion();
        builder.HasIndex((Faculty f) => f.Name).IsUnique();
    }
}
