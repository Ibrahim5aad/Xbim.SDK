using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xbim.Server.Domain.Entities;

namespace Xbim.Server.Persistence.EfCore.Configurations;

public class ModelConfiguration : IEntityTypeConfiguration<Model>
{
    public void Configure(EntityTypeBuilder<Model> builder)
    {
        builder.ToTable("Models");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.ProjectId)
            .IsRequired();

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.Description)
            .HasMaxLength(2000);

        builder.Property(m => m.CreatedAt)
            .IsRequired();

        // Relationship to Project
        builder.HasOne(m => m.Project)
            .WithMany(p => p.Models)
            .HasForeignKey(m => m.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // One model has many versions
        builder.HasMany(m => m.Versions)
            .WithOne(v => v.Model)
            .HasForeignKey(v => v.ModelId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(m => m.ProjectId);
        builder.HasIndex(m => new { m.ProjectId, m.Name });
    }
}
