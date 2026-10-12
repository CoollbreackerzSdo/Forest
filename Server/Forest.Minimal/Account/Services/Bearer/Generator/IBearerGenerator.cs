using System.Security.Claims;
using Forest.Minimal.Account.Services.Bearer.Models;

namespace Forest.Minimal.Account.Services.Bearer.Generator;

public interface IBearerGenerator
{
    Task<BearerTransport> GenerateAsync(IEnumerable<Claim> claims, IEnumerable<Claim>? refreshClaims = null, CancellationToken token = default);
}