using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xbim.Server.Domain.Entities;

namespace Xbim.Server.Persistence.EfCore.Configurations;

public class IfcPropertySetConfiguration : IEntityTypeConfiguration<IfcPropertySet>
{
    public void Configure(EntityTypeBuilder<IfcPropertySet> builder)
    {
        builder.ToTable("IfcPropertySets");

        builder.HasKey(ps => ps.Id);

        builder.Property(ps => ps.ElementId)
            .IsRequired();

        builder.Property(ps => ps.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(ps => ps.GlobalId)
            .HasMaxLength(64);

        builder.Property(ps => ps.IsTypePropertySet)
            .IsRequired();

        // Relationship to Element
        builder.HasOne(ps => ps.Element)
            .WithMany(e => e.PropertySets)
            .HasForeignKey(ps => ps.ElementId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(ps => ps.ElementId);
        builder.HasIndex(ps => new { ps.ElementId, ps.Name });
    }
}
