namespace Remotely.Shared.Helpers;

/// <summary>
/// Clé d'accès de la session sans surveillance, transmise par l'agent au client de bureau
/// hors de la ligne de commande (visible de tout utilisateur du poste dans la liste des processus,
/// et recopiée par les journaux d'audit de création de processus).
/// Windows : variable d'environnement du seul processus enfant. Linux : entrée standard
/// (<c>sudo</c> ne transmet pas les variables d'environnement), annoncée par <c>--access-key -</c>.
/// </summary>
public static class DesktopAccessKey
{
    public const string EnvironmentVariable = "AMARRE_ACCESS_KEY";

    /// <summary>Valeur de <c>--access-key</c> qui demande de lire la clé sur l'entrée standard.</summary>
    public const string FromStandardInput = "-";

    /// <summary>
    /// Clé passée en option (anciens agents), sinon lue sur l'entrée standard (<c>-</c>),
    /// sinon dans la variable d'environnement.
    /// </summary>
    public static string Resolve(string? optionValue, Func<string?> readEnvironment, Func<string?> readStandardInput)
    {
        if (optionValue == FromStandardInput)
        {
            return readStandardInput()?.Trim() ?? string.Empty;
        }

        if (!string.IsNullOrEmpty(optionValue))
        {
            return optionValue;
        }

        return readEnvironment() ?? string.Empty;
    }
}
