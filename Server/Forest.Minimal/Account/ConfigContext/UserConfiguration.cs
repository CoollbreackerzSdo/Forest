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

        builder.HasIndex(x => x.UserName)
            .IsUnique();

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
            details.ToTable("user_details");

            details.Property(x => x.BannerId)
                .HasColumnName("banner_id");

            details.Property(x => x.PhotoId)
            .HasColumnName("photo_id");

            details.Property(x => x.FirstName)
            .HasColumnName("first_name");

            details.Property(x => x.LastName)
            .HasColumnName("last_name");
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