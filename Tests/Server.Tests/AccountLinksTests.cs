using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Models;
using Remotely.Server.Services;
using System;
using System.ComponentModel.DataAnnotations;

namespace Remotely.Server.Tests;

[TestClass]
public class AccountLinksTests
{
    private const string RequestBase = "https://hote-de-la-requete.example/";

    [TestMethod]
    [DataRow("https://amarre.example", "https://amarre.example/")]
    [DataRow("https://amarre.example/", "https://amarre.example/")]
    [DataRow(" https://amarre.example:8443 ", "https://amarre.example:8443/")]
    [DataRow("https://amarre.example/sous-dossier", "https://amarre.example/sous-dossier/")]
    [DataRow("http://10.0.0.5:5000", "http://10.0.0.5:5000/")]
    [DataRow("https://amarre.example/?x=1#y", "https://amarre.example/")]
    public void Resolve_UrlPubliqueRenseignee(string publicUrl, string expected) =>
        Assert.AreEqual(expected, PublicUrl.Resolve(publicUrl, RequestBase).AbsoluteUri);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("amarre.example")]
    [DataRow("ftp://amarre.example")]
    [DataRow("javascript:alert(1)")]
    [DataRow("https://user:pass@amarre.example")]
    public void Resolve_UrlPubliqueAbsenteOuInvalide_AdresseDeLaRequete(string? publicUrl) =>
        Assert.AreEqual(RequestBase, PublicUrl.Resolve(publicUrl, RequestBase).AbsoluteUri);

    [TestMethod]
    [DataRow("https://publique.example", "https://serveur.example", "https://publique.example/")]
    [DataRow("", "https://serveur.example", "https://serveur.example/")]
    [DataRow("", "", RequestBase)]
    public void Resolve_RepliSurUrlDuServeur(string publicUrl, string serverUrl, string expected) =>
        Assert.AreEqual(expected, PublicUrl.Resolve(publicUrl, serverUrl, RequestBase).AbsoluteUri);

    [TestMethod]
    public void AttemptLimiter_PlafondParCle()
    {
        var limiter = new AttemptLimiter(2, TimeSpan.FromMinutes(5));
        Assert.IsTrue(limiter.TryRecord("a"));
        Assert.IsFalse(limiter.IsBlocked("a"));
        Assert.IsTrue(limiter.TryRecord("a"));
        Assert.IsTrue(limiter.IsBlocked("a"));
        Assert.IsFalse(limiter.TryRecord("a"));
        Assert.IsFalse(limiter.IsBlocked("b"));
    }

    [TestMethod]
    public void Resolve_CheminRelatifAjoute() =>
        Assert.AreEqual(
            "https://amarre.example/sous-dossier/Account/ResetPassword",
            new Uri(PublicUrl.Resolve("https://amarre.example/sous-dossier", RequestBase), "Account/ResetPassword").AbsoluteUri);

    [TestMethod]
    [DataRow("", true)]
    [DataRow("https://amarre.example", true)]
    [DataRow("amarre.example", false)]
    [DataRow("ftp://amarre.example", false)]
    public void Reglage_Validation(string publicUrl, bool expected)
    {
        var settings = new SettingsModel { PublicUrl = publicUrl };
        var context = new ValidationContext(settings) { MemberName = nameof(SettingsModel.PublicUrl) };
        Assert.AreEqual(expected, Validator.TryValidateProperty(settings.PublicUrl, context, []));
    }

}