using System.Text;

namespace Remotely.Shared.Helpers;

/// <summary>
/// Construit une ligne de commande Windows à partir d'une liste d'arguments, avec les règles
/// de découpage de <c>CommandLineToArgvW</c> (celles de <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>).
/// Sert quand l'API attend une seule chaîne (<c>CreateProcessAsUser</c>) : un guillemet dans
/// une valeur ne peut pas ajouter d'argument.
/// </summary>
public static class WindowsCommandLine
{
    public static string Build(string fileName, IEnumerable<string> arguments)
    {
        var sb = new StringBuilder();
        // Le nom du programme suit d'autres règles (pas d'échappement) : toujours entre guillemets,
        // sinon un chemin avec des espaces est découpé.
        if (fileName.Contains('"'))
        {
            throw new ArgumentException("Nom de programme invalide.", nameof(fileName));
        }
        sb.Append('"').Append(fileName).Append('"');

        foreach (var argument in arguments)
        {
            sb.Append(' ');
            AppendArgument(sb, argument);
        }
        return sb.ToString();
    }

    private static void AppendArgument(StringBuilder sb, string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny([' ', '\t', '\n', '\v', '"']) < 0)
        {
            sb.Append(argument);
            return;
        }

        sb.Append('"');
        var backslashes = 0;
        foreach (var c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                // Barres obliques inverses doublées, puis guillemet échappé.
                sb.Append('\\', backslashes * 2 + 1);
            }
            else
            {
                sb.Append('\\', backslashes);
            }
            backslashes = 0;
            sb.Append(c);
        }
        // Avant le guillemet fermant, les barres obliques inverses sont doublées.
        sb.Append('\\', backslashes * 2);
        sb.Append('"');
    }
}