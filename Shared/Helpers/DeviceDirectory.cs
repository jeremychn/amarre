using Remotely.Shared.Dtos;

namespace Remotely.Shared.Helpers;

/// <summary>
/// Lecture d'un dossier du poste pour l'explorateur de fichiers de la console (côté agent).
/// Dossiers d'abord, puis fichiers, par nom ; éléments illisibles ignorés ; liste plafonnée.
/// </summary>
public static class DeviceDirectory
{
    public static DeviceFileListDto List(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ListRoots();
        }

        var result = new DeviceFileListDto { Path = path };
        try
        {
            var directory = new DirectoryInfo(path);
            if (!directory.Exists)
            {
                result.Error = "Dossier introuvable.";
                return result;
            }

            result.Path = directory.FullName;
            result.ParentPath = directory.Parent?.FullName ?? string.Empty;

            var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = 0 };
            var entries = directory.EnumerateFileSystemInfos("*", options)
                .Select(ToEntry)
                .OrderByDescending(x => x.IsDirectory)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(DeviceFileListDto.MaxEntries + 1)
                .ToList();

            result.Truncated = entries.Count > DeviceFileListDto.MaxEntries;
            result.Entries = entries.Take(DeviceFileListDto.MaxEntries).ToList();
        }
        catch (UnauthorizedAccessException)
        {
            result.Error = "Accès refusé.";
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or System.Security.SecurityException)
        {
            result.Error = ex.Message;
        }
        return result;
    }

    // Racine : les lecteurs prêts sous Windows, « / » ailleurs.
    private static DeviceFileListDto ListRoots()
    {
        if (!OperatingSystem.IsWindows())
        {
            return List("/");
        }

        return new DeviceFileListDto
        {
            Entries = DriveInfo.GetDrives()
                .Where(x => x.IsReady)
                .Select(x => new DeviceFileEntryDto
                {
                    Name = x.Name,
                    FullPath = x.RootDirectory.FullName,
                    IsDirectory = true,
                    Size = x.TotalSize,
                })
                .ToList(),
        };
    }

    private static DeviceFileEntryDto ToEntry(FileSystemInfo info) => new()
    {
        Name = info.Name,
        FullPath = info.FullName,
        IsDirectory = info is DirectoryInfo,
        Size = info is FileInfo file ? file.Length : 0,
        LastModified = info.LastWriteTimeUtc,
    };

    /// <summary>
    /// Chemin libre dans <paramref name="folder"/> pour <paramref name="fileName"/> : « nom (2).ext »,
    /// « nom (3).ext »… si le nom est déjà pris (rien n'est écrasé).
    /// </summary>
    public static string GetFreePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var i = 2; File.Exists(path) || Directory.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{stem} ({i}){extension}");
        }
        return path;
    }
}
