using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using UniversityLeaders.Domain.Entities;

namespace UniversityLeaders.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey((AuditLog a) => a.Id);
        builder.Property((AuditLog a) => a.Id).UseIdentityAlwaysColumn();

        builder.Property((AuditLog a) => a.Action).HasMaxLength(100).IsRequired();
        builder.Property((AuditLog a) => a.EntityType).HasMaxLength(100).IsRequired();
        builder.Property((AuditLog a) => a.EntityId).HasMaxLength(64).IsRequired();
        builder.Property((AuditLog a) => a.Details).HasColumnType("jsonb");

        builder.HasIndex((AuditLog a) => new { a.EntityType, a.EntityId });
        builder.HasIndex((AuditLog a) => a.ActorId);
        builder.HasIndex((AuditLog a) => a.OccurredAt);

        builder.HasOne((AuditLog a) => a.Actor)
            .WithMany()
            .HasForeignKey((AuditLog a) => a.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
