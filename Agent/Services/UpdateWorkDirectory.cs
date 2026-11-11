using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Remotely.Agent.Services;

/// <summary>
/// Dossier de travail d'une mise à jour : nom aléatoire, créé à neuf, réservé au compte de l'agent
/// (root sous Linux et macOS, SYSTEM et Administrateurs sous Windows).
/// </summary>
public static class UpdateWorkDirectory
{
    private const string Prefix = "amarre-maj-";

    public static string Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return CreateWindows();
        }

        // mkdtemp : nom aléatoire, droits 0700
        return Directory.CreateTempSubdirectory(Prefix).FullName;
    }

    public static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Dossier temporaire : le système le nettoiera.
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static string CreateWindows()
    {
        var security = new DirectorySecurity();
        // Pas d'héritage du dossier temporaire parent (C:\Windows\Temp laisse les utilisateurs y créer des fichiers).
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sidType in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
        {
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(sidType, null),
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        var directory = new DirectoryInfo(Path.Combine(Path.GetTempPath(), Prefix + Guid.NewGuid().ToString("N")));
        if (directory.Exists)
        {
            throw new IOException($"Le dossier de mise à jour existe déjà : {directory.FullName}");
        }

        directory.Create(security);
        return directory.FullName;
    }
}
