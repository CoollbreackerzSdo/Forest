using Forest.Minimal.Common.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forest.Minimal.Common.ConfigContext;

public abstract class EntityTypeConfiguration<T> : IEntityTypeConfiguration<T>
    where T : class, IEntity<EntityId>
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.Property(x => x.Key)
            .HasConversion(x => x.Value, x => x)
            .HasColumnName("id");

        builder.HasKey(x => x.Key);
    }
}