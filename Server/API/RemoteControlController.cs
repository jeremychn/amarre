using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Remotely.Server.Hubs;
using Remotely.Server.Models;
using Remotely.Server.Services;
using Remotely.Server.Auth;
using Remotely.Shared.Helpers;
using Remotely.Server.Extensions;
using Remotely.Shared.Interfaces;

namespace Remotely.Server.API;

[Route("api/[controller]")]
[ApiController]
public class RemoteControlController : ControllerBase
{
    private readonly IHubContext<AgentHub, IAgentHubClient> _agentHub;
    private readonly IRemoteControlSessionCache _remoteControlSessionCache;
    private readonly IAgentHubSessionCache _serviceSessionCache;
    private readonly IDataService _dataService;
    private readonly IOtpProvider _otpProvider;

    public RemoteControlController(
        IDataService dataService,
        IRemoteControlSessionCache remoteControlSessionCache,
        IHubContext<AgentHub, IAgentHubClient> agentHub,
        IAgentHubSessionCache serviceSessionCache,
        IOtpProvider otpProvider)
    {
        _dataService = dataService;
        _agentHub = agentHub;
        _remoteControlSessionCache = remoteControlSessionCache;
        _serviceSessionCache = serviceSessionCache;
        _otpProvider = otpProvider;
    }

    // Seul point d'entrée : clé d'API (ou administrateur déjà connecté). L'ancienne connexion
    // par e-mail et mot de passe (POST) a été retirée avec AllowApiLogin.
    [HttpGet("{deviceID}")]
    [ServiceFilter(typeof(ApiAuthorizationFilter))]
    public async Task<IActionResult> Get(string deviceID)
    {
        if (!Request.Headers.TryGetOrganizationId(out var orgId))
        {
            return Unauthorized();
        }
        
        return await InitiateRemoteControl(deviceID, orgId);
    }

    private async Task<IActionResult> InitiateRemoteControl(string deviceID, string orgId)
    {
        if (!_serviceSessionCache.TryGetByDeviceId(deviceID, out var targetDevice) ||
            !_serviceSessionCache.TryGetConnectionId(deviceID, out var serviceConnectionId))
        {
            return NotFound("The target device couldn't be found.");
        }

        // Poste d'une autre organisation : même réponse qu'un poste absent.
        if (targetDevice.OrganizationID != orgId)
        {
            return NotFound("The target device couldn't be found.");
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            var userResult = await _dataService.GetUserByName($"{User.Identity.Name}");

            if (!userResult.IsSuccess)
            {
                return Unauthorized();
            }

            if (!_dataService.DoesUserHaveAccessToDevice(targetDevice.ID, userResult.Value))
            {
                return Unauthorized();
            }
        }

        var sessionCount = _remoteControlSessionCache.Sessions.Count(x => x.OrganizationId == orgId);

        var sessionId = Guid.NewGuid();
        var accessKey = RandomGenerator.GenerateAccessKey();

        // Clé rangée dans la session (voir DesktopHub.ReceiveUnattendedSessionInfo).
        var session = new RemoteControlSession()
        {
            UnattendedSessionId = sessionId,
            AccessKey = accessKey,
            UserConnectionId = HttpContext.Connection.Id,
            AgentConnectionId = serviceConnectionId,
            DeviceId = deviceID,
            OrganizationId = orgId
        };

        _remoteControlSessionCache.AddOrUpdate($"{sessionId}", session, (k, v) =>
        {
            v.AgentConnectionId = HttpContext.Connection.Id;
            return v;
        });

        var orgNameResult = await _dataService.GetOrganizationNameById(orgId);

        if (!orgNameResult.IsSuccess)
        {
            return BadRequest("Failed to resolve organization name.");
        }

        await _agentHub.Clients.Client(serviceConnectionId).RemoteControl(
            sessionId,
            accessKey,
            HttpContext.Connection.Id,
            string.Empty,
            orgNameResult.Value,
            orgId);

        var waitResult = await session.WaitForSessionReady(TimeSpan.FromSeconds(30));
        if (!waitResult)
        {
            return StatusCode(408, "The remote control process failed to start in time on the remote device.");
        }
       
        var otp = _otpProvider.GetOtp(targetDevice.ID);

        return Ok($"{HttpContext.Request.Scheme}://{Request.Host}/Viewer?mode=Unattended&sessionId={sessionId}&accessKey={accessKey}&otp={otp}");
    }
}
