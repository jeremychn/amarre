using System.Diagnostics.CodeAnalysis;

namespace Remotely.Server.Services;

/// <summary>
/// URL publique du serveur (réglage <c>PublicUrl</c>) : base des liens envoyés à l'extérieur
/// (e-mails de compte). Sans effet sur les agents, contrairement à <c>ServerUrl</c>.
/// </summary>
public static class PublicUrl
{
    /// <summary>
    /// Vrai si <paramref name="value"/> est une adresse http(s) absolue, sans identifiants.
    /// <paramref name="baseUri"/> : schéma, hôte, port et chemin terminé par « / »
    /// (requête et fragment ignorés).
    /// </summary>
    public static bool TryParse(string? value, [NotNullWhen(true)] out Uri? baseUri)
    {
        baseUri = null;
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        var path = uri.AbsolutePath.EndsWith('/') ? uri.AbsolutePath : uri.AbsolutePath + "/";
        baseUri = new UriBuilder(uri.Scheme, uri.Host, uri.Port, path).Uri;
        return true;
    }

    /// <summary>
    /// L'URL publique si elle est valide, sinon l'adresse de la requête (en-tête Host).
    /// </summary>
    public static Uri Resolve(string? publicUrl, string requestBaseUri) =>
        Resolve(publicUrl, null, requestBaseUri);

    /// <summary>
    /// L'URL publique, sinon l'URL du serveur (<c>ServerUrl</c>, réglée par l'admin pour les agents),
    /// sinon seulement l'adresse de la requête : l'en-tête Host vient du client.
    /// </summary>
    public static Uri Resolve(string? publicUrl, string? serverUrl, string requestBaseUri) =>
        TryParse(publicUrl, out var baseUri) || TryParse(serverUrl, out baseUri) ? baseUri : new Uri(requestBaseUri);
}