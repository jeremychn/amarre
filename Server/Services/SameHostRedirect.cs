namespace Remotely.Server.Services;

/// <summary>
/// Les redirections de Blazor (pages de compte) partent de l'hôte de la requête, que le client
/// choisit (en-tête <c>Host</c>). Une redirection vers ce même hôte est réécrite en chemin relatif :
/// le navigateur reste sur l'adresse qu'il a réellement appelée.
/// </summary>
public static class SameHostRedirect
{
    /// <summary>
    /// Chemin relatif (chemin, requête, fragment) si <paramref name="location"/> vise le schéma et
    /// l'hôte de la requête ; sinon <paramref name="location"/> inchangée.
    /// </summary>
    public static string Rewrite(string location, string requestScheme, HostString requestHost)
    {
        if (!requestHost.HasValue ||
            !Uri.TryCreate(location, UriKind.Absolute, out var target) ||
            !Uri.TryCreate($"{requestScheme}://{requestHost.Value}", UriKind.Absolute, out var request) ||
            Uri.Compare(target, request, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return location;
        }

        return target.PathAndQuery + target.Fragment;
    }

    public static void Apply(HttpContext context)
    {
        var headers = context.Response.Headers;
        var location = headers.Location.ToString();
        if (!string.IsNullOrEmpty(location))
        {
            headers.Location = Rewrite(location, context.Request.Scheme, context.Request.Host);
        }
    }
}
