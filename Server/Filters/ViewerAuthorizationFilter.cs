using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Remotely.Server.Services;

namespace Remotely.Server.Filters;

internal class ViewerAuthorizationFilter(
    IDataService _dataService,
    IOtpProvider _otpProvider):  IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (await ViewerAccess.IsAuthorized(_dataService, _otpProvider, context.HttpContext))
        {
            return;
        }

        context.Result = new RedirectResult("/Account/Login");
    }
}
