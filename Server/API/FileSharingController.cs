using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared.Utilities;
using Remotely.Shared.Helpers;
using Remotely.Shared;

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
public class FileSharingController : ControllerBase
{
    private readonly IDataService _dataService;

    public FileSharingController(IDataService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>
    /// Fichier envoyé par l'agent pour l'explorateur de fichiers (jeton à usage unique de ce poste).
    /// Rangé comme fichier partagé ; le navigateur le récupère par <see cref="Download"/>.
    /// </summary>
    [HttpPost("from-device")]
    [ServiceFilter(typeof(ExpiringTokenFilter))]
    [RequestSizeLimit(AppConstants.MaxUploadFileSize)]
    public async Task<IActionResult> FromDevice([FromQuery] string name)
    {
        if (!ExpiringTokenFilter.TryGetScope(HttpContext, out var scope) ||
            !scope.TryUseForUpload(Time.Now))
        {
            return NotFound();
        }

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer);
        var fileId = await _dataService.AddSharedFile(
            PathSanitizer.GetSafeFileName(name),
            buffer.ToArray(),
            "application/octet-stream",
            scope.OrganizationId);

        scope.UploadedFileId = fileId;
        return Ok();
    }

    /// <summary>Fichier partagé téléchargé par un utilisateur connecté de la même organisation.</summary>
    [HttpGet("download/{id}")]
    [Authorize]
    public async Task<IActionResult> Download(string id)
    {
        var userResult = await _dataService.GetUserByName($"{User.Identity?.Name}");
        var fileResult = await _dataService.GetSharedFiled(id);
        if (!userResult.IsSuccess ||
            !fileResult.IsSuccess ||
            fileResult.Value.OrganizationID != userResult.Value.OrganizationID)
        {
            return NotFound();
        }

        var file = fileResult.Value;
        return File(file.FileContents, file.ContentType ?? "application/octet-stream", file.FileName);
    }

    [HttpGet("{id}")]
    [ServiceFilter(typeof(ExpiringTokenFilter))]
    public async Task<IActionResult> Get(string id)
    {
        // Jeton d'agent : seulement les fichiers de l'envoi pour lequel il a été remis.
        if (ExpiringTokenFilter.TryGetScope(HttpContext, out var scope) &&
            !scope.CanReadFile(id, Time.Now))
        {
            return NotFound();
        }

        var sharedFileResult = await _dataService.GetSharedFiled(id);

        if (!sharedFileResult.IsSuccess ||
            !Request.Headers.TryGetOrganizationId(out var orgId) ||
            sharedFileResult.Value.OrganizationID != orgId)
        {
            return NotFound();
        }

        var sharedFile = sharedFileResult.Value;
        var contentType = sharedFile.ContentType ?? "application/octet-stream";
        return File(sharedFile.FileContents, contentType, sharedFile.FileName);
    }
}
