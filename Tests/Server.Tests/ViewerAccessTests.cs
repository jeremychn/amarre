using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Filters;
using Remotely.Server.Models;
using Remotely.Server.Services;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class ViewerAccessTests
{
    private static IDataService Settings(bool requireAuthentication)
    {
        var dataService = new Mock<IDataService>();
        dataService
            .Setup(x => x.GetSettings())
            .ReturnsAsync(new SettingsModel { RemoteControlRequiresAuthentication = requireAuthentication });
        return dataService.Object;
    }

    private static HttpContext WithOtp(string otp)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?otp={otp}");
        return context;
    }

    [TestMethod]
    public async Task Anonymous_AllowedWhenAuthenticationNotRequired()
    {
        Assert.IsTrue(await ViewerAccess.IsAuthorized(Settings(false), new OtpProvider(), new DefaultHttpContext()));
    }

    [TestMethod]
    public async Task Anonymous_RefusedWhenAuthenticationRequired()
    {
        Assert.IsFalse(await ViewerAccess.IsAuthorized(Settings(true), new OtpProvider(), new DefaultHttpContext()));
    }

    [TestMethod]
    public async Task AuthenticatedUser_Allowed()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "test"))
        };
        Assert.IsTrue(await ViewerAccess.IsAuthorized(Settings(true), new OtpProvider(), context));
    }

    [TestMethod]
    public async Task Otp_ValidAllowed_UnknownRefused()
    {
        var otpProvider = new OtpProvider();
        var otp = otpProvider.GetOtp("device-1");

        Assert.IsTrue(await ViewerAccess.IsAuthorized(Settings(true), otpProvider, WithOtp(otp)));
        Assert.IsFalse(await ViewerAccess.IsAuthorized(Settings(true), otpProvider, WithOtp("inconnu")));
    }
}
