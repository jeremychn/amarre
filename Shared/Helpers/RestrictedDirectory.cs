using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Remotely.Shared.Helpers;

/// <summary>
/// Dossier réservé au compte qui l'ouvre (root sous Linux et macOS ; SYSTEM, Administrateurs
/// et le compte courant sous Windows), créé au besoin. Un chemin déjà pris par un lien, une
/// jonction, un fichier ou (Windows) un dossier d'un autre propriétaire est refusé.
/// </summary>
public static class RestrictedDirectory
{
    public static string GetOrCreate(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows())
        {
            GetOrCreateWindows(fullPath);
        }
        else
        {
            GetOrCreateUnix(fullPath);
        }
        return fullPath;
    }

    private static void EnsureRealDirectory(string path)
    {
        if (File.Exists(path))
        {
            throw new IOException($"Un fichier occupe déjà l'emplacement du dossier : {path}");
        }

        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
        {
            return;
        }

        if (directory.LinkTarget is not null || directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IOException($"Le dossier est un lien ou une jonction : {path}");
        }
    }

    [UnsupportedOSPlatform("windows")]
    private static void GetOrCreateUnix(string path)
    {
        const UnixFileMode othersWrite = UnixFileMode.GroupWrite | UnixFileMode.OtherWrite;
        const UnixFileMode ownerOnly = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

        // Sans appel système pour lire le propriétaire, on exige un parent où seul son
        // propriétaire peut créer ou renommer des entrées : personne d'autre n'a pu y poser le dossier.
        var parent = Path.GetDirectoryName(path)
            ?? throw new IOException($"Dossier parent introuvable : {path}");
        if ((File.GetUnixFileMode(parent) & othersWrite) != 0)
        {
            throw new IOException($"Le dossier parent est modifiable par d'autres comptes : {parent}");
        }

        EnsureRealDirectory(path);
        Directory.CreateDirectory(path, ownerOnly);
        EnsureRealDirectory(path);
        File.SetUnixFileMode(path, ownerOnly);
    }

    [SupportedOSPlatform("windows")]
    private static void GetOrCreateWindows(string path)
    {
        var trusted = GetTrustedSids();
        var security = new DirectorySecurity();
        // Pas d'héritage du parent.
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (var sid in trusted)
        {
            security.AddAccessRule(new FileSystemAccessRule(
                sid,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        EnsureRealDirectory(path);
        var directory = new DirectoryInfo(path);
        if (!directory.Exists)
        {
            directory.Create(security);
            directory.Refresh();
        }
        EnsureRealDirectory(path);

        var owner = directory.GetAccessControl(AccessControlSections.Owner).GetOwner(typeof(SecurityIdentifier));
        if (owner is not SecurityIdentifier ownerSid || !trusted.Contains(ownerSid))
        {
            throw new IOException($"Le propriétaire du dossier n'est pas un compte de confiance ({owner}) : {path}");
        }

        // Dossier préexistant : droits remis à neuf.
        directory.SetAccessControl(security);
    }

    [SupportedOSPlatform("windows")]
    private static HashSet<SecurityIdentifier> GetTrustedSids()
    {
        var sids = new HashSet<SecurityIdentifier>
        {
            new(WellKnownSidType.LocalSystemSid, null),
            new(WellKnownSidType.BuiltinAdministratorsSid, null),
        };
        // Service : SYSTEM, déjà présent. Agent lancé à la main : le compte courant.
        if (WindowsIdentity.GetCurrent().User is { } current)
        {
            sids.Add(current);
        }
        return sids;
    }
}
