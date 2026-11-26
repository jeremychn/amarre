using Remotely.Shared.Enums;

namespace Remotely.Server.Services;

/// <summary>
/// Scripts prêts à l'emploi, ajoutés à la demande dans le dossier « Bibliothèque Amarre » des scripts
/// enregistrés de l'organisation (voir <see cref="IDataService.InstallScriptLibrary"/>).
/// Windows : Windows PowerShell 5.1 (présent sur tous les postes), sans module à installer.
/// Rien n'est supprimé définitivement : Windows Update renomme ses dossiers, le nettoyage ne touche
/// qu'aux fichiers temporaires de plus de 7 jours.
/// </summary>
public static class ScriptLibrary
{
    public const string FolderPath = "Bibliothèque Amarre";

    public sealed record LibraryScript(string Name, ScriptingShell Shell, string Content);

    public static IReadOnlyList<LibraryScript> All { get; } =
    [
        new("Windows - Durée de fonctionnement et redémarrage en attente", ScriptingShell.WinPS, """
            $boot = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
            $uptime = (Get-Date) - $boot
            "Dernier démarrage : {0:dd/MM/yyyy HH:mm} (il y a {1} j {2} h)" -f $boot, $uptime.Days, $uptime.Hours
            $keys = @(
                'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending',
                'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
            )
            $pending = $keys | Where-Object { Test-Path $_ }
            $renames = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -Name PendingFileRenameOperations -ErrorAction SilentlyContinue).PendingFileRenameOperations
            if ($pending -or $renames) { "Redémarrage en attente : OUI" } else { "Redémarrage en attente : non" }
            """),

        new("Windows - Redémarrer dans 10 minutes (avec message)", ScriptingShell.WinPS, """
            # L'utilisateur voit le message ; « Windows - Annuler le redémarrage programmé » l'annule.
            shutdown.exe /r /t 600 /c "Votre ordinateur va redémarrer dans 10 minutes pour maintenance. Enregistrez votre travail."
            if ($LASTEXITCODE -eq 0) { "Redémarrage programmé dans 10 minutes." } else { "Échec (code $LASTEXITCODE) : un arrêt est peut-être déjà programmé." }
            """),

        new("Windows - Annuler le redémarrage programmé", ScriptingShell.WinPS, """
            shutdown.exe /a
            if ($LASTEXITCODE -eq 0) { "Redémarrage annulé." } else { "Aucun arrêt programmé (code $LASTEXITCODE)." }
            """),

        new("Windows - Nettoyer les fichiers temporaires (plus de 7 jours)", ScriptingShell.WinPS, """
            $limit = (Get-Date).AddDays(-7)
            $folders = @("$env:SystemRoot\Temp") + (Get-ChildItem 'C:\Users' -Directory -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName 'AppData\Local\Temp' })
            $freed = 0
            foreach ($folder in $folders) {
                if (-not (Test-Path $folder)) { continue }
                Get-ChildItem $folder -Recurse -File -Force -ErrorAction SilentlyContinue |
                    Where-Object { $_.LastWriteTime -lt $limit } |
                    ForEach-Object {
                        $size = $_.Length
                        Remove-Item $_.FullName -Force -ErrorAction SilentlyContinue
                        if (-not (Test-Path $_.FullName)) { $freed += $size }
                    }
            }
            "Espace libéré : {0:N0} Mo" -f ($freed / 1MB)
            """),

        new("Windows - Réparer Windows Update", ScriptingShell.WinPS, """
            # Services arrêtés, cache renommé (pas supprimé), services relancés. Un redémarrage aide ensuite.
            $services = 'wuauserv', 'bits', 'cryptsvc'
            Stop-Service $services -Force -ErrorAction SilentlyContinue
            $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
            foreach ($path in "$env:SystemRoot\SoftwareDistribution", "$env:SystemRoot\System32\catroot2") {
                if (Test-Path $path) {
                    Rename-Item $path "$(Split-Path $path -Leaf).old-$stamp" -ErrorAction Continue
                }
            }
            Start-Service $services -ErrorAction Continue
            Get-Service $services | Format-Table Name, Status -AutoSize | Out-String
            "Terminé. Relancer la recherche de mises à jour sur le poste."
            """),

        new("Windows - Espace disque par lecteur", ScriptingShell.WinPS, """
            Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" |
                Select-Object DeviceID,
                    @{ n = 'Taille (Go)'; e = { [math]::Round($_.Size / 1GB, 1) } },
                    @{ n = 'Libre (Go)'; e = { [math]::Round($_.FreeSpace / 1GB, 1) } },
                    @{ n = 'Libre (%)'; e = { [math]::Round(100 * $_.FreeSpace / $_.Size) } } |
                Format-Table -AutoSize | Out-String
            """),

        new("Windows - Logiciels installés", ScriptingShell.WinPS, """
            $paths = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
                     'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
            Get-ItemProperty $paths -ErrorAction SilentlyContinue |
                Where-Object { $_.DisplayName -and -not $_.SystemComponent } |
                Sort-Object DisplayName -Unique |
                Select-Object DisplayName, DisplayVersion, Publisher |
                Format-Table -AutoSize | Out-String -Width 200
            """),

        new("Windows - État BitLocker", ScriptingShell.WinPS, """
            manage-bde.exe -status
            """),

        new("Windows - Administrateurs locaux", ScriptingShell.WinPS, """
            # Groupe désigné par son SID : même nom quelle que soit la langue de Windows.
            $group = Get-CimInstance Win32_Group -Filter "LocalAccount=True AND SID='S-1-5-32-544'"
            Get-CimAssociatedInstance -InputObject $group -ResultClassName Win32_Account |
                Select-Object Domain, Name, SID | Format-Table -AutoSize | Out-String
            """),

        new("Windows - Forcer la mise à jour des stratégies (gpupdate)", ScriptingShell.WinPS, """
            gpupdate.exe /force
            """),

        new("Windows - Vider le cache DNS", ScriptingShell.WinPS, """
            Clear-DnsClientCache
            "Cache DNS vidé."
            """),

        new("Linux - Durée de fonctionnement et redémarrage en attente", ScriptingShell.Bash, """
            echo "En marche depuis : $(uptime -p)"
            if [ -f /var/run/reboot-required ]; then
                echo "Redémarrage en attente : OUI"
                cat /var/run/reboot-required.pkgs 2>/dev/null
            else
                echo "Redémarrage en attente : non"
            fi
            """),

        new("Linux - Espace disque", ScriptingShell.Bash, """
            df -h -x tmpfs -x devtmpfs -x squashfs
            """),
    ];
}
