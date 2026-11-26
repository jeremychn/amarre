using Microsoft.AspNetCore.Mvc;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Utilities;

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
public class SavedScriptsController : ControllerBase
{
    private readonly IDataService _dataService;

    public SavedScriptsController(IDataService dataService)
    {
        _dataService = dataService;
    }

    [ServiceFilter(typeof(ExpiringTokenFilter))]
    [HttpGet("{scriptId}")]
    public async Task<ActionResult<SavedScript>> GetScript(Guid scriptId)
    {
        // Jeton d'agent : seulement le script de l'exécution pour laquelle il a été remis.
        if (ExpiringTokenFilter.TryGetScope(HttpContext, out var scope) &&
            !scope.CanReadScript(scriptId, Time.Now))
        {
            return NotFound();
        }

        var result =  await _dataService.GetSavedScript(scriptId);
        if (!result.IsSuccess ||
            !Request.Headers.TryGetOrganizationId(out var orgId) ||
            result.Value.OrganizationID != orgId)
        {
            return NotFound();
        }

        return result.Value;
    }
}
