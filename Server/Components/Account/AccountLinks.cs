using Microsoft.AspNetCore.Components;

namespace Remotely.Server.Components.Account;

/// <summary>
/// Liens envoyés par e-mail (réinitialisation du mot de passe, confirmation d'adresse).
/// Ils partent de l'URL publique du serveur (<c>ServerUrl</c>) quand elle est renseignée :
/// l'hôte de la requête vient de l'en-tête <c>Host</c>, qu'un client anonyme peut choisir
/// pour faire pointer le lien vers un autre site.
/// </summary>
internal static class AccountLinks
{
    public static string Build(
        NavigationManager navigationManager,
        string? serverUrl,
        string relativePath,
        IReadOnlyDictionary<string, object?> query)
    {
        var target = new Uri(GetBaseUri(serverUrl, navigationManager.BaseUri), relativePath);
        return navigationManager.GetUriWithQueryParameters(target.AbsoluteUri, query);
    }

    /// <summary>
    /// <paramref name="serverUrl"/> si c'est une adresse http(s) absolue, sinon l'adresse
    /// de la requête (comportement d'avant, quand <c>ServerUrl</c> n'est pas renseignée).
    /// </summary>
    public static Uri GetBaseUri(string? serverUrl, string requestBaseUri)
    {
        if (Uri.TryCreate(serverUrl?.Trim(), UriKind.Absolute, out var configured) &&
            (configured.Scheme == Uri.UriSchemeHttps || configured.Scheme == Uri.UriSchemeHttp) &&
            string.IsNullOrEmpty(configured.UserInfo))
        {
            var path = configured.AbsolutePath.EndsWith('/')
                ? configured.AbsolutePath
                : configured.AbsolutePath + "/";
            return new UriBuilder(configured.Scheme, configured.Host, configured.Port, path).Uri;
        }

        return new Uri(requestBaseUri);
    }
}