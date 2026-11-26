using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Components.Account;
using System;

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
    public void GetBaseUri_ServerUrlRenseignee(string serverUrl, string expected) =>
        Assert.AreEqual(expected, AccountLinks.GetBaseUri(serverUrl, RequestBase).AbsoluteUri);

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("amarre.example")]
    [DataRow("ftp://amarre.example")]
    [DataRow("javascript:alert(1)")]
    [DataRow("https://user:pass@amarre.example")]
    public void GetBaseUri_ServerUrlAbsenteOuInvalide_AdresseDeLaRequete(string? serverUrl) =>
        Assert.AreEqual(RequestBase, AccountLinks.GetBaseUri(serverUrl, RequestBase).AbsoluteUri);

    [TestMethod]
    public void GetBaseUri_CheminRelatifAjoute() =>
        Assert.AreEqual(
            "https://amarre.example/sous-dossier/Account/ResetPassword",
            new Uri(AccountLinks.GetBaseUri("https://amarre.example/sous-dossier", RequestBase), "Account/ResetPassword").AbsoluteUri);
}