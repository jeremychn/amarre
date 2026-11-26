using Remotely.Desktop.Native.Linux;
using System.Security.Principal;

namespace Remotely.Desktop.Shared.Services;

public interface IDesktopEnvironment
{
    bool IsElevated { get; }
    bool IsDebug { get; }
}

internal class DesktopEnvironment : IDesktopEnvironment
{
    public bool IsDebug
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public bool IsElevated
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            if (OperatingSystem.IsLinux())
            {
                return Libc.geteuid() == 0;
            }
            return false;
        }
    }
}

/// <summary>
/// Vrai si le client tourne sous SYSTEM (Windows, lancé par l'agent avec le jeton de winlogon)
/// ou root (Linux, aucun utilisateur trouvé) : il s'affiche alors sur le bureau d'un utilisateur
/// qui n'a pas ces droits, et ne doit rien lui ouvrir qui permette de lancer un programme
/// (sélecteur de fichiers, explorateur).
/// </summary>
public static class SystemAccount
{
    public static bool IsCurrent
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                using var identity = WindowsIdentity.GetCurrent();
                return identity.IsSystem;
            }
            return OperatingSystem.IsLinux() && Libc.geteuid() == 0;
        }
    }
}
