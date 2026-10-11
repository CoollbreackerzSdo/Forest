using Forest.Minimal.Account.Models;
using Forest.Minimal.Common.ConfigContext;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Forest.Minimal.Account.ConfigContext;

public sealed class UserConfiguration : EntityTypeConfiguration<UserModel>
{
    public override void Configure(EntityTypeBuilder<UserModel> builder)
    {
        builder.ToTable("users");

        base.Configure(builder);

        builder.HasAlternateKey(x => x.UserName);

        builder.Property(user => user.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.Register);

        builder.Property(user => user.Register)
            .HasColumnName("register");

        builder.Property(user => user.IsEnable)
            .HasColumnName("is_enable");

        builder.OwnsOne(user => user.Details, details =>
        {
            details.ToJson("details");

            details.Property(value => value.FirstName)
                .HasColumnName("first_name")
                .HasMaxLength(100);

            details.Property(value => value.LastName)
                .HasColumnName("last_name")
                .HasMaxLength(100);
        });

        builder.OwnsOne(user => user.Security, security =>
        {
            security.HasIndex(x => x.Email)
                .IsUnique();

            security.Property(value => value.Email)
                .HasColumnName("email");

            security.Property(value => value.Password)
                .HasColumnName("password");
        });

        builder.HasMany(x => x.Tokens)
            .WithOne()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}