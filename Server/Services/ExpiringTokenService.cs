using Remotely.Shared;
using Remotely.Shared.Dtos;
using Remotely.Shared.Helpers;
using Microsoft.Extensions.Caching.Memory;
using System.Diagnostics.CodeAnalysis;

namespace Remotely.Server.Services;

/// <summary>
/// Ce qu'un jeton remis à un agent permet : lire un script ou des fichiers précis pendant
/// quelques minutes, et envoyer un seul résultat pour ce poste (et ce script, cette exécution).
/// Les agents ne lisent pas le jeton : ils le renvoient tel quel, rien ne change pour eux.
/// </summary>
public sealed class ExpiringTokenScope
{
    /// <summary>Lecture du script : l'agent le télécharge dès qu'il reçoit l'ordre.</summary>
    public static readonly TimeSpan ScriptReadWindow = TimeSpan.FromMinutes(2);

    /// <summary>Fichiers envoyés au poste : téléchargés l'un après l'autre.</summary>
    public static readonly TimeSpan FileReadWindow = TimeSpan.FromMinutes(5);

    /// <summary>Résultat d'un script ou d'une commande d'API : après exécution (30 min au plus).</summary>
    public static readonly TimeSpan ScriptResultWindow = TimeSpan.FromMinutes(AppConstants.ScriptRunExpirationMinutes);

    /// <summary>Résultat d'une commande du terminal (30 s au plus côté agent).</summary>
    public static readonly TimeSpan TerminalResultWindow = TimeSpan.FromMinutes(5);

    private int _resultUsed;

    private ExpiringTokenScope(string organizationId, string deviceId)
    {
        OrganizationId = organizationId;
        DeviceId = deviceId;
    }

    public string OrganizationId { get; }
    public string DeviceId { get; }
    public Guid? SavedScriptId { get; private init; }
    public int? ScriptRunId { get; private init; }
    public IReadOnlyCollection<string> SharedFileIds { get; private init; } = [];
    public DateTimeOffset ReadUntil { get; private init; }
    public DateTimeOffset ResultUntil { get; private init; }

    public DateTimeOffset Expiration => ReadUntil > ResultUntil ? ReadUntil : ResultUntil;

    public static ExpiringTokenScope ForScriptRun(string organizationId, string deviceId, Guid savedScriptId, int scriptRunId, DateTimeOffset now) =>
        new(organizationId, deviceId)
        {
            SavedScriptId = savedScriptId,
            ScriptRunId = scriptRunId,
            ReadUntil = now + ScriptReadWindow,
            ResultUntil = now + ScriptResultWindow,
        };

    public static ExpiringTokenScope ForCommand(string organizationId, string deviceId, TimeSpan resultWindow, DateTimeOffset now) =>
        new(organizationId, deviceId)
        {
            ResultUntil = now + resultWindow,
        };

    public static ExpiringTokenScope ForFiles(string organizationId, string deviceId, IEnumerable<string> sharedFileIds, DateTimeOffset now) =>
        new(organizationId, deviceId)
        {
            SharedFileIds = sharedFileIds.ToHashSet(),
            ReadUntil = now + FileReadWindow,
        };

    public bool CanReadScript(Guid savedScriptId, DateTimeOffset now) =>
        now < ReadUntil && SavedScriptId == savedScriptId;

    public bool CanReadFile(string sharedFileId, DateTimeOffset now) =>
        now < ReadUntil && SharedFileIds.Contains(sharedFileId);

    /// <summary>
    /// Vrai une seule fois, pour un résultat de ce poste, de ce script et de cette exécution.
    /// </summary>
    public bool TryUseForResult(ScriptResultDto result, DateTimeOffset now) =>
        now < ResultUntil &&
        result.DeviceID == DeviceId &&
        result.SavedScriptId == SavedScriptId &&
        result.ScriptRunId == ScriptRunId &&
        Interlocked.Exchange(ref _resultUsed, 1) == 0;
}

public interface IExpiringTokenService
{
    /// <summary>
    /// Jeton remis à un agent : il ne vaut que pour ce que décrit <paramref name="scope"/>.
    /// </summary>
    string GetToken(ExpiringTokenScope scope);
    bool TryGetToken(string secret, [NotNullWhen(true)] out ExpiringTokenScope? scope);
}

public class ExpiringTokenService : IExpiringTokenService
{
    private static readonly MemoryCache _tokenCache = new(new MemoryCacheOptions());

    public string GetToken(ExpiringTokenScope scope)
    {
        var secret = RandomGenerator.GenerateString(36);
        _tokenCache.Set(secret, scope, scope.Expiration);
        return secret;
    }

    public bool TryGetToken(string secret, [NotNullWhen(true)] out ExpiringTokenScope? scope) =>
        _tokenCache.TryGetValue(secret, out scope) && scope is not null;
}
