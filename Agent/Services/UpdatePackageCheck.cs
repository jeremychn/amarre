using Microsoft.Extensions.Logging;
using Remotely.Shared.Helpers;
using Remotely.Shared.Utilities;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Remotely.Agent.Services;

/// <summary>
/// Dernière étape avant de lancer un installeur : le paquet et le script téléchargés doivent figurer dans le
/// manifeste signé (voir <see cref="UpdateManifest"/>). Lève une exception sinon, et la mise à jour n'a pas lieu.
/// </summary>
public static class UpdatePackageCheck
{
    public static async Task EnsureSigned(
        IUpdateDownloader downloader,
        string serverUrl,
        string workDir,
        ILogger logger,
        params (string Name, string Path)[] files)
    {
        var installDir = AppContext.BaseDirectory;
        var manifestPath = Path.Combine(workDir, UpdateManifest.FileName);
        try
        {
            await downloader.DownloadFile($"{serverUrl}/Content/{UpdateManifest.FileName}", manifestPath);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning("Manifeste des mises à jour introuvable : {message}", ex.Message);
        }

        var bypassed = UpdateManifest.IsBypassed(installDir);
        var error = UpdateManifest.CheckPackage(manifestPath, installDir, AppVersionHelper.GetAppVersion(), files);
        if (error is not null)
        {
            // L'ETag du zip est déjà noté : sans ça, le poste ne réessaierait qu'au prochain zip, même une fois
            // le manifeste corrigé. Avec, il réessaie après le délai d'échec (24 h).
            File.Delete(Path.Combine(installDir, "etag.txt"));
            throw new InvalidOperationException($"Mise à jour refusée : {error}.");
        }

        if (bypassed)
        {
            // Un seul usage : l'installeur Linux ne vide pas le dossier de l'agent.
            File.Delete(Path.Combine(installDir, UpdateManifest.BypassFileName));
            logger.LogWarning("Mise à jour installée SANS vérification de signature ({file} présent, effacé).",
                UpdateManifest.BypassFileName);
            return;
        }

        logger.LogInformation("Mise à jour vérifiée : manifeste signé, empreintes conformes.");
    }
}
