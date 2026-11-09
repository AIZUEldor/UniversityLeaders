using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class DirectionConfiguration : IEntityTypeConfiguration<Direction>
{
    public void Configure(EntityTypeBuilder<Direction> builder)
    {
        builder.ToTable("directions");
        builder.HasKey((Direction d) => d.Id);
        builder.Property((Direction d) => d.Id).ValueGeneratedNever();
        builder.Property((Direction d) => d.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex((Direction d) => d.Name).IsUnique();

        // The 7 official directions; must match frontend/src/lib/data.ts exactly (including typographic quotes).
        // Migration seed with fixed ids: idempotent, and changes produce update migrations instead of duplicates.
        builder.HasData(
            new Direction { Id = 1, Name = "“Yoshlar daftari” va metsenatlik dasturlari" },
            new Direction { Id = 2, Name = "Harbiy-vatanparvarlik loyihalari" },
            new Direction { Id = 3, Name = "Talaba-qizlar" },
            new Direction { Id = 4, Name = "“Besh tashabbus” loyihalari" },
            new Direction { Id = 5, Name = "Intellektual loyihalar" },
            new Direction { Id = 6, Name = "Tadbirkorlik va innovatsion loyihalar" },
            new Direction { Id = 7, Name = "“Social Media”" });
    }
}
