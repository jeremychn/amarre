using Remotely.Shared.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Remotely.Server.Auth;
using Remotely.Server.Extensions;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;
using System.Text;

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
public class ScriptResultsController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly IEmailSenderEx _emailSender;
    private readonly ILogger<ScriptResultsController> _logger;

    public ScriptResultsController(
        IDataService dataService, 
        IEmailSenderEx emailSenderEx,
        ILogger<ScriptResultsController> logger)
    {
        _dataService = dataService;
        _emailSender = emailSenderEx;
        _logger = logger;
    }

    [HttpGet]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public ActionResult DownloadAll()
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        var commandResults = _dataService.GetAllCommandResults(orgId);
        var content = System.Text.Json.JsonSerializer.Serialize(commandResults);
        return File(Encoding.UTF8.GetBytes(content), "application/octet-stream", "ScriptHistory.json");       
    }

    [HttpGet("{scriptId}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public ActionResult<FileResult> DownloadResults(string scriptId)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }

        var commandResult = _dataService.GetScriptResult(scriptId, orgId);
        var content = System.Text.Json.JsonSerializer.Serialize(commandResult);
        return File(Encoding.UTF8.GetBytes(content), "application/octet-stream", "ScriptResults.json");
    }


    [HttpPost]
    [ServiceFilter(typeof(ExpiringTokenFilter))]
    public async Task<ActionResult<ScriptResultResponse>> Post([FromBody] ScriptResultDto result)
    {
        var scriptResult = await _dataService.AddScriptResult(result);

        if (!scriptResult.IsSuccess)
        {
            _logger.LogResult(scriptResult);
            return BadRequest();
        }

        var errorOut = result.ErrorOutput ?? Array.Empty<string>();

        if (result.HadErrors && result.SavedScriptId.HasValue)
        {
            var savedScriptResult = await _dataService.GetSavedScript(result.SavedScriptId.Value);
            if (!savedScriptResult.IsSuccess)
            {
                return NotFound();
            }

            var savedScript = savedScriptResult.Value;
            if (savedScript.GenerateAlertOnError)
            {
                await _dataService.AddAlert(result.DeviceID,
                    savedScript.OrganizationID,
                    $"Alert triggered while running script {savedScript.Name}.",
                    string.Join("\n", errorOut));
            }

            if (savedScript.SendEmailOnError)
            {
                var deviceResult = await _dataService.GetDevice(
                    result.DeviceID, 
                    query => query.Include(x => x.DeviceGroup));

                if (!deviceResult.IsSuccess)
                {
                    return NotFound();
                }

                var device = deviceResult.Value;

                if (!string.IsNullOrWhiteSpace(savedScript.SendErrorEmailTo))
                {
                    // Device fields and script output come from the endpoint: encode them for the HTML email.
                    static string H(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);

                    await _emailSender.SendEmailAsync(savedScript.SendErrorEmailTo,
                        "Script Run Alert",
                        $"An alert was triggered while running script {H(savedScript.Name)} on device {H(device.DeviceName)}. <br /><br />" +
                            $"Device ID: {H(device.ID)} <br /> " +
                            $"Device Name: {H(device.DeviceName)} <br /> " +
                            $"Device Alias: {H(device.Alias)} <br /> " +
                            $"Device Group (if any): {H(device.DeviceGroup?.Name)} <br /> " +

                            $"Error Output: <br /> <br /> " +
                            $"{string.Join("<br /> <br />", errorOut.Select(H))}");
                }
            }
        }

        if (result.ScriptRunId.HasValue)
        {
            await _dataService.AddScriptResultToScriptRun(scriptResult.Value.ID, result.ScriptRunId.Value);
        }

        return new ScriptResultResponse()
        {
            Id = scriptResult.Value.ID
        };
    }
}
