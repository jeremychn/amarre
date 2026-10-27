using Remotely.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace Remotely.Server.Services;

public interface IExpiringTokenService
{
    /// <summary>
    /// Jeton remis à un agent : il ne donne accès qu'aux données de cette organisation.
    /// </summary>
    string GetToken(DateTimeOffset expiration, string organizationId);
    bool TryGetToken(string secret, out DateTimeOffset tokenExpiration, out string organizationId);
}

public class ExpiringTokenService : IExpiringTokenService
{
    private static readonly MemoryCache _tokenCache = new(new MemoryCacheOptions());

    public string GetToken(DateTimeOffset expiration, string organizationId)
    {
        var secret = RandomGenerator.GenerateString(36);
        _tokenCache.Set(secret, (expiration, organizationId), expiration);
        return secret;
    }

    public bool TryGetToken(string secret, out DateTimeOffset tokenExpiration, out string organizationId)
    {
        if (_tokenCache.TryGetValue(secret, out (DateTimeOffset Expiration, string OrganizationId) token))
        {
            tokenExpiration = token.Expiration;
            organizationId = token.OrganizationId;
            return true;
        }

        tokenExpiration = default;
        organizationId = string.Empty;
        return false;
    }
}
