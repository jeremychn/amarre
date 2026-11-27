using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.API;
using Remotely.Server.Services;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class BrandingControllerTests
{
    private IDataService _dataService = null!;
    private TestData _testData = null!;

    [TestInitialize]
    public async Task TestInit()
    {
        _testData = new TestData();
        await _testData.Init();
        _dataService = IoCActivator.ServiceProvider.GetRequiredService<IDataService>();
    }

    [TestCleanup]
    public void TestCleanup() => _testData.ClearData();

    // L'ID d'organisation est le secret d'enrôlement (voir M3) : les endpoints anonymes de branding
    // ne doivent jamais le renvoyer, sinon un anonyme l'obtient en une requête.
    [TestMethod]
    [DoNotParallelize]
    public async Task Get_NeDivulguePasLIdDOrganisation()
    {
        var controller = new BrandingController(_dataService, NullLogger<BrandingController>.Instance);

        var result = await controller.Get(_testData.Org1Id);

        Assert.IsNotNull(result.Value);
        Assert.IsTrue(string.IsNullOrEmpty(result.Value.OrganizationId));
        Assert.IsTrue(string.IsNullOrEmpty(result.Value.Id));
        Assert.AreEqual("Assistance à distance", result.Value.Product);
    }
}
