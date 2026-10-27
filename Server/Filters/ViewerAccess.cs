using Remotely.Server.Services;

namespace Remotely.Server.Filters;

/// <summary>
/// Règle d'accès au contrôle à distance, commune à la page Viewer et au hub /hubs/viewer
/// (les filtres MVC ne s'appliquent pas aux hubs SignalR).
/// </summary>
public static class ViewerAccess
{
    public static async Task<bool> IsAuthorized(IDataService dataService, IOtpProvider otpProvider, HttpContext httpContext)
    {
        var settings = await dataService.GetSettings();
        if (!settings.RemoteControlRequiresAuthentication)
        {
            return true;
        }

        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            return true;
        }

        return httpContext.Request.Query.TryGetValue("otp", out var otp) &&
            otpProvider.Exists($"{otp}");
    }
}
