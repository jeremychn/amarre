using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.API;
using Remotely.Server.Hubs;
using Remotely.Server.Models;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using Remotely.Shared.Interfaces;
using Remotely.Shared.Primitives;
using System;
using System.Linq;
using System.Threading.Tasks;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Remotely.Server.Tests;

// Points d'entrée hérités, marqués obsolètes : testés tant qu'ils existent.
#pragma warning disable CS0618

[TestClass]
public class ApiLoginTests
{
    private Mock<IDataService> _dataService = null!;
    private Mock<SignInManager<RemotelyUser>> _signInManager = null!;

    [TestInitialize]
    public void Init()
    {
        _dataService = new Mock<IDataService>();
        _dataService.Setup(x => x.GetSettings()).ReturnsAsync(new SettingsModel { AllowApiLogin = true });

        var userManager = new Mock<UserManager<RemotelyUser>>(
            Mock.Of<IUserStore<RemotelyUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        _signInManager = new Mock<SignInManager<RemotelyUser>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<RemotelyUser>>(),
            null!, null!, null!, null!);
    }

    public static System.Collections.Generic.IEnumerable<object[]> FailedResults =>
    [
        [SignInResult.Failed],
        [SignInResult.LockedOut],
        [SignInResult.TwoFactorRequired],
        [SignInResult.NotAllowed],
    ];

    [TestMethod]
    [DynamicData(nameof(FailedResults))]
    public async Task LoginPost_AnyFailure_SameUnauthorizedResponse(SignInResult signInResult)
    {
        SetupSignIn(signInResult);
        var controller = new LoginController(
            _signInManager.Object,
            _dataService.Object,
            Mock.Of<IHubContext<DesktopHub>>(),
            Mock.Of<IRemoteControlSessionCache>(),
            Mock.Of<IHubContext<ViewerHub>>(),
            NullLogger<LoginController>.Instance);

        var result = await controller.Post(new ApiLogin { Email = "a@exemple.test", Password = "x" });

        Assert.IsInstanceOfType<UnauthorizedResult>(result);
    }

    [TestMethod]
    [DynamicData(nameof(FailedResults))]
    public async Task RemoteControlPost_AnyFailure_SameUnauthorizedResponse(SignInResult signInResult)
    {
        SetupSignIn(signInResult);

        var result = await CreateRemoteControlController().Post(NewRequest());

        Assert.IsInstanceOfType<UnauthorizedResult>(result);
        // Le compte n'est cherché qu'après un mot de passe juste.
        _dataService.Verify(x => x.GetUserByName(It.IsAny<string>(), It.IsAny<Action<IQueryable<RemotelyUser>>?>()), Times.Never);
    }

    [TestMethod]
    public async Task RemoteControlPost_DeviceNotAccessible_UnauthorizedAndSignedOut()
    {
        SetupSignIn(SignInResult.Success);
        var user = new RemotelyUser { UserName = "a@exemple.test", OrganizationID = "org" };
        _dataService
            .Setup(x => x.GetUserByName("a@exemple.test", It.IsAny<Action<IQueryable<RemotelyUser>>?>()))
            .ReturnsAsync(Result.Ok(user));
        _dataService.Setup(x => x.DoesUserHaveAccessToDevice("poste", user)).Returns(false);

        var result = await CreateRemoteControlController().Post(NewRequest());

        Assert.IsInstanceOfType<UnauthorizedResult>(result);
        _signInManager.Verify(x => x.SignOutAsync(), Times.Once);
    }

    [TestMethod]
    public async Task RemoteControlPost_ApiLoginDisabled_NotFound()
    {
        _dataService.Setup(x => x.GetSettings()).ReturnsAsync(new SettingsModel { AllowApiLogin = false });

        var result = await CreateRemoteControlController().Post(NewRequest());

        Assert.IsInstanceOfType<NotFoundResult>(result);
        _signInManager.Verify(
            x => x.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }

    private void SetupSignIn(SignInResult signInResult)
    {
        _signInManager
            .Setup(x => x.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), false, true))
            .ReturnsAsync(signInResult);
        _signInManager.Setup(x => x.SignOutAsync()).Returns(Task.CompletedTask);
    }

    private static RemoteControlRequest NewRequest() =>
        new() { Email = "a@exemple.test", Password = "x", DeviceID = "poste" };

    private RemoteControlController CreateRemoteControlController() =>
        new(
            _signInManager.Object,
            _dataService.Object,
            Mock.Of<IRemoteControlSessionCache>(),
            Mock.Of<IHubContext<AgentHub, IAgentHubClient>>(),
            Mock.Of<IAgentHubSessionCache>(),
            Mock.Of<IOtpProvider>(),
            NullLogger<RemoteControlController>.Instance);
}
