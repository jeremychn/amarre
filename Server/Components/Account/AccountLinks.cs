using Microsoft.AspNetCore.Components;
using Remotely.Server.Services;

namespace Remotely.Server.Components.Account;

/// <summary>
/// Liens envoyés par e-mail (réinitialisation du mot de passe, confirmation d'adresse).
/// Ils partent de l'URL publique du serveur (<c>PublicUrl</c>) quand elle est renseignée :
/// l'hôte de la requête vient de l'en-tête <c>Host</c>, qu'un client anonyme peut choisir
/// pour faire pointer le lien vers un autre site.
/// </summary>
internal static class AccountLinks
{
    public static string Build(
        NavigationManager navigationManager,
        string? publicUrl,
        string relativePath,
        IReadOnlyDictionary<string, object?> query)
    {
        var target = new Uri(PublicUrl.Resolve(publicUrl, navigationManager.BaseUri), relativePath);
        return navigationManager.GetUriWithQueryParameters(target.AbsoluteUri, query);
    }
}