using Forest.Minimal.Account.Models;
using Forest.Minimal.Common.ConfigContext;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forest.Minimal.Account.ConfigContext;

public sealed class TokenConfiguration : EntityTypeConfiguration<TokenModel>
{
    public override void Configure(EntityTypeBuilder<TokenModel> builder)
    {
        builder.ToTable("tokens");

        base.Configure(builder);

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.HasAlternateKey(x => x.RefreshId);

        builder.Property(x => x.RefreshId)
            .HasConversion(x => x.Value, x => x)
            .HasColumnName("refresh_id");

        builder.HasIndex(x => x.Exp);

        builder.Property(x => x.Exp)
            .HasColumnName("expire");

        builder.HasIndex(x => x.RefreshExp);

        builder.Property(x => x.RefreshExp)
            .HasColumnName("refresh_expire");
    }
}