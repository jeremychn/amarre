using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;

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

    [HttpGet("{id}")]
    [ServiceFilter(typeof(ExpiringTokenFilter))]
    public async Task<IActionResult> Get(string id)
    {
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
