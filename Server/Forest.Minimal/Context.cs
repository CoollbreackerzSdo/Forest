using Forest.Minimal.Account.ConfigContext;
using Forest.Minimal.Account.Models;

namespace Forest.Minimal;

public sealed class Context(DbContextOptions options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new TokenConfiguration());
    }
    public DbSet<UserModel> Users { get; init; }
    public DbSet<TokenModel> Tokens { get; init; }
}