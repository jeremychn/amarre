using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Remotely.Shared.Entities;

namespace Remotely.Server.Services;

/// <summary>
/// Connexion par mot de passe : un compte inconnu coûte un calcul de hachage, comme un compte
/// existant, pour que le temps de réponse ne dise pas si l'adresse a un compte.
/// Le verrouillage de compte reste celui d'Identity (compte existant seulement).
/// </summary>
public class AccountSignInManager(
    UserManager<RemotelyUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<RemotelyUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<RemotelyUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<RemotelyUser> confirmation)
    : SignInManager<RemotelyUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    private static string? _dummyHash;

    public override async Task<SignInResult> PasswordSignInAsync(
        string userName,
        string password,
        bool isPersistent,
        bool lockoutOnFailure)
    {
        var user = await UserManager.FindByNameAsync(userName);
        if (user is null)
        {
            VerifyDummyPassword(UserManager.PasswordHasher, password);
            return SignInResult.Failed;
        }

        return await PasswordSignInAsync(user, password, isPersistent, lockoutOnFailure);
    }

    /// <summary>
    /// Vérifie <paramref name="password"/> contre un hachage tiré au hasard (même algorithme et même
    /// nombre d'itérations que les vrais comptes) ; le résultat est ignoré.
    /// </summary>
    public static void VerifyDummyPassword(IPasswordHasher<RemotelyUser> hasher, string? password)
    {
        var dummyUser = new RemotelyUser();
        _dummyHash ??= hasher.HashPassword(dummyUser, Guid.NewGuid().ToString());
        _ = hasher.VerifyHashedPassword(dummyUser, _dummyHash, password ?? string.Empty);
    }
}
