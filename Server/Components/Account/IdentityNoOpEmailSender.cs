using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Remotely.Shared.Entities;

namespace Remotely.Server.Components.Account;

// Remove the "else if (EmailSender is IdentityNoOpEmailSender)" block from RegisterConfirmation.razor after updating with a real implementation.
internal sealed class IdentityNoOpEmailSender : IEmailSender<RemotelyUser>
{
    private readonly IEmailSender emailSender = new NoOpEmailSender();

    public Task SendConfirmationLinkAsync(RemotelyUser user, string email, string confirmationLink) =>
        emailSender.SendEmailAsync(email, "Confirmez votre adresse e-mail", $"Veuillez confirmer votre compte en <a href='{confirmationLink}'>cliquant ici</a>.");

    public Task SendPasswordResetLinkAsync(RemotelyUser user, string email, string resetLink) =>
        emailSender.SendEmailAsync(email, "Réinitialisez votre mot de passe", $"Veuillez réinitialiser votre mot de passe en <a href='{resetLink}'>cliquant ici</a>.");

    public Task SendPasswordResetCodeAsync(RemotelyUser user, string email, string resetCode) =>
        emailSender.SendEmailAsync(email, "Réinitialisez votre mot de passe", $"Veuillez réinitialiser votre mot de passe avec le code suivant : {resetCode}");
}
