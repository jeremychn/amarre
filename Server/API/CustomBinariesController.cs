using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Services;
using Remotely.Shared.Models;
using Remotely.Shared.Services;

namespace Remotely.Server.API;

[Route("api/custom-binaries")]
[ApiController]
public class CustomBinariesController(
    IDataService _dataService,
    IWebHostEnvironment _hostingEnvironment,
    IEmbeddedServerDataProvider _embeddedData) : ControllerBase
{
    [HttpGet("win-x86/desktop/{organizationId}")]
    public Task<IActionResult> GetWinX86Desktop(string organizationId) =>
        GetDesktop("Win-x86", organizationId);

    [HttpGet("win-x64/desktop/{organizationId}")]
    public Task<IActionResult> GetWinX64Desktop(string organizationId) =>
        GetDesktop("Win-x64", organizationId);

    private async Task<IActionResult> GetDesktop(string platformFolder, string organizationId)
    {
        // Page anonyme : l'ID est recopié dans le nom du fichier, seul celui d'une organisation existante passe.
        if (!(await _dataService.GetOrganizationNameById(organizationId)).IsSuccess)
        {
            return NotFound();
        }

        var embeddedData = await GetEmbeddedData(organizationId);
        var filePath = Path.Combine(_hostingEnvironment.ContentRootPath, "AppData", platformFolder, "Remotely_Desktop.exe");
        var fileName = _embeddedData.GetEncodedFileName(filePath, embeddedData);
        var rs = System.IO.File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(rs, "application/octet-stream", fileName);
    }

    private async Task<EmbeddedServerData> GetEmbeddedData(string? organizationId)
    {
        var defaultOrg = await _dataService.GetDefaultOrganization();

        // The default org will be used if unspecified, so might as well save the
        // space in the file name.
        if (defaultOrg.IsSuccess &&
            defaultOrg.Value.ID.Equals(organizationId, StringComparison.OrdinalIgnoreCase))
        {
            organizationId = null;
        }

        // Adresse inscrite dans le client : l'URL publique si elle est renseignée, sinon l'hôte de la requête.
        var settings = await _dataService.GetSettings();
        var effectiveScheme = settings.ForceClientHttps ? "https" : Request.Scheme;
        var serverUrl = PublicUrl.Resolve(settings.PublicUrl, $"{effectiveScheme}://{Request.Host}");
        return new EmbeddedServerData(serverUrl, organizationId);
    }
}
