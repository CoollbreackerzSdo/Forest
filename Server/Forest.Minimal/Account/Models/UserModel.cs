using Forest.Minimal.Common.Models;

namespace Forest.Minimal.Account.Models;

public sealed class UserModel : EntityBase, IAccount
{
    public required string UserName { get; set; }
    public Details? Details { get; set; }
    public required Security Security { get; init; }
    public ICollection<TokenModel> Tokens { get; set; } = new HashSet<TokenModel>();
}