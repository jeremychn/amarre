using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;

namespace Remotely.Server.Tests;

[TestClass]
public class SameHostRedirectTests
{
    [TestMethod]
    [DataRow("https://hote-forge.example/Account/ForgotPasswordConfirmation", "https", "hote-forge.example", "/Account/ForgotPasswordConfirmation")]
    [DataRow("https://HOTE-forge.example/sous-dossier/Account/Login?ReturnUrl=%2F#x", "https", "hote-forge.example", "/sous-dossier/Account/Login?ReturnUrl=%2F#x")]
    [DataRow("http://localhost:5000/Account/Login", "http", "localhost:5000", "/Account/Login")]
    public void MemeHote_CheminRelatif(string location, string scheme, string host, string expected)
    {
        Assert.AreEqual(expected, SameHostRedirect.Rewrite(location, scheme, new HostString(host)));
    }

    [TestMethod]
    [DataRow("https://autre.example/Account/Login", "https", "hote.example")]
    [DataRow("https://hote.example/Account/Login", "http", "hote.example")]
    [DataRow("http://localhost:5001/Account/Login", "http", "localhost:5000")]
    [DataRow("/Account/Login", "https", "hote.example")]
    [DataRow("Account/Login", "https", "hote.example")]
    public void AutreCible_Inchangee(string location, string scheme, string host)
    {
        Assert.AreEqual(location, SameHostRedirect.Rewrite(location, scheme, new HostString(host)));
    }

    [TestMethod]
    public void SansHote_Inchangee()
    {
        Assert.AreEqual("https://hote.example/x", SameHostRedirect.Rewrite("https://hote.example/x", "https", default));
    }
}
