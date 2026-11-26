using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.API;
using Remotely.Server.Services;
using Remotely.Shared.Services;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class CustomBinariesControllerTests
{
    private IDataService _dataService = null!;
    private TestData _testData = null!;
    private string _contentRoot = null!;

    [TestInitialize]
    public async Task TestInit()
    {
        _testData = new TestData();
        await _testData.Init();
        _dataService = IoCActivator.ServiceProvider.GetRequiredService<IDataService>();

        _contentRoot = Path.Combine(Path.GetTempPath(), $"amarre-custom-binaries-{Guid.NewGuid()}");
        var folder = Directory.CreateDirectory(Path.Combine(_contentRoot, "AppData", "Win-x64"));
        await File.WriteAllBytesAsync(Path.Combine(folder.FullName, "Remotely_Desktop.exe"), [0x4D, 0x5A]);
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        var settings = await _dataService.GetSettings();
        settings.PublicUrl = string.Empty;
        await _dataService.SaveSettings(settings);
        _testData.ClearData();
        Directory.Delete(_contentRoot, true);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task OrganisationInconnue_Refusee()
    {
        var result = await GetWinX64Desktop(Guid.NewGuid().ToString(), "hote-de-la-requete.example");

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task SansUrlPublique_AdresseDeLaRequete()
    {
        await SetPublicUrl(string.Empty);

        var serverUrl = await GetEmbeddedServerUrl(_testData.Org2Id, "hote-de-la-requete.example");

        Assert.AreEqual("https://hote-de-la-requete.example/", serverUrl.AbsoluteUri);
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task AvecUrlPublique_HoteForgeIgnore()
    {
        await SetPublicUrl("https://amarre.example");

        var serverUrl = await GetEmbeddedServerUrl(_testData.Org2Id, "hote-forge.example");

        Assert.AreEqual("https://amarre.example/", serverUrl.AbsoluteUri);
    }

    private async Task SetPublicUrl(string publicUrl)
    {
        var settings = await _dataService.GetSettings();
        settings.PublicUrl = publicUrl;
        await _dataService.SaveSettings(settings);
    }

    private async Task<Uri> GetEmbeddedServerUrl(string organizationId, string host)
    {
        var result = await GetWinX64Desktop(organizationId, host);
        var file = result as FileStreamResult;
        Assert.IsNotNull(file);
        await file.FileStream.DisposeAsync();

        var embedded = EmbeddedServerDataProvider.Instance.TryGetEmbeddedData(file.FileDownloadName);
        Assert.IsTrue(embedded.IsSuccess);
        Assert.AreEqual(organizationId, embedded.Value.OrganizationId);
        return embedded.Value.ServerUrl;
    }

    private Task<IActionResult> GetWinX64Desktop(string organizationId, string host)
    {
        var hostEnv = new Mock<IWebHostEnvironment>();
        hostEnv.Setup(x => x.ContentRootPath).Returns(_contentRoot);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString(host);

        var controller = new CustomBinariesController(
            _dataService,
            hostEnv.Object,
            EmbeddedServerDataProvider.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
        return controller.GetWinX64Desktop(organizationId);
    }
}
