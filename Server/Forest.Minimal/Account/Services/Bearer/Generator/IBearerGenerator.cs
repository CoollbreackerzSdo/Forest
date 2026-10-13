using System.Security.Claims;
using Forest.Minimal.Account.Services.Bearer.Models;

namespace Forest.Minimal.Account.Services.Bearer.Generator;

public interface IBearerGenerator
{
    Task<BearerTransport> GenerateAsync(IDictionary<string, object> claims, IDictionary<string, object> refreshClaims, CancellationToken token = default);
}