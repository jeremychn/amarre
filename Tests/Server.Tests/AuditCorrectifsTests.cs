using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Auth;
using Remotely.Server.Enums;
using Remotely.Server.Models;
using Remotely.Server.Services;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class AuditCorrectifsTests
{
    private TestData _testData = null!;
    private IDataService _dataService = null!;

    [TestInitialize]
    public async Task Init()
    {
        _testData = new TestData();
        await _testData.Init();
        _dataService = IoCActivator.ServiceProvider.GetRequiredService<IDataService>();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        var settings = await _dataService.GetSettings();
        settings.Require2FA = false;
        await _dataService.SaveSettings(settings);
        _testData.ClearData();
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Api_2FAExigeeNonActivee_CookieRefuse()
    {
        Assert.IsNull(await AuthorizeAdminCookie());

        var settings = await _dataService.GetSettings();
        settings.Require2FA = true;
        await _dataService.SaveSettings(settings);

        Assert.IsInstanceOfType<UnauthorizedResult>(await AuthorizeAdminCookie());
    }

    [TestMethod]
    public async Task CreateNew_SignalPropreAuClone()
    {
        var original = new RemoteControlSession { Mode = RemoteControlMode.Unattended };
        var clone = original.CreateNew();
        original.Dispose();

        clone.SetSessionReadyState(true);
        Assert.IsTrue(await clone.WaitForSessionReady(TimeSpan.FromSeconds(1)));
    }

    private async Task<IActionResult?> AuthorizeAdminCookie()
    {
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, _testData.Org1Admin1.UserName!)], "Cookies")),
        };
        var context = new AuthorizationFilterContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>());

        await new ApiAuthorizationFilter(_dataService, NullLogger<ApiAuthorizationFilter>.Instance)
            .OnAuthorizationAsync(context);
        return context.Result;
    }
}
