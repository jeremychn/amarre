using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class AccountSignInManagerTests
{
    // Compte inconnu : un calcul de hachage, comme pour un compte existant (pas d'écart de temps).
    [TestMethod]
    public async Task CompteInconnu_HachageFactice_Echec()
    {
        var hasher = new Mock<IPasswordHasher<RemotelyUser>>();
        hasher.Setup(x => x.HashPassword(It.IsAny<RemotelyUser>(), It.IsAny<string>())).Returns("hachage");
        hasher
            .Setup(x => x.VerifyHashedPassword(It.IsAny<RemotelyUser>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Failed);

        var userManager = new UserManager<RemotelyUser>(
            new Mock<IUserStore<RemotelyUser>>().Object,
            null!,
            hasher.Object,
            [],
            [],
            null!,
            null!,
            null!,
            new Mock<ILogger<UserManager<RemotelyUser>>>().Object);

        var signInManager = new AccountSignInManager(
            userManager,
            new Mock<IHttpContextAccessor>().Object,
            new Mock<IUserClaimsPrincipalFactory<RemotelyUser>>().Object,
            null!,
            new Mock<ILogger<SignInManager<RemotelyUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<RemotelyUser>>().Object);

        var result = await signInManager.PasswordSignInAsync("inconnu@exemple.test", "mot-de-passe", false, true);

        Assert.AreEqual(SignInResult.Failed, result);
        hasher.Verify(
            x => x.VerifyHashedPassword(It.IsAny<RemotelyUser>(), It.IsAny<string>(), "mot-de-passe"),
            Times.Once);
    }
}
