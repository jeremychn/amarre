using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;
using System.Linq;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class ScriptLibraryTests
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

    [TestMethod]
    public void Bibliotheque_NomsUniques_ContenuNonVide()
    {
        Assert.AreEqual(ScriptLibrary.All.Count, ScriptLibrary.All.Select(x => x.Name).Distinct().Count());
        Assert.IsTrue(ScriptLibrary.All.All(x => !string.IsNullOrWhiteSpace(x.Content)));
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Installer_UneFois_PuisRien_PublicsDansLeDossier()
    {
        var first = await _dataService.InstallScriptLibrary(_testData.Org1Admin1.Id);
        var second = await _dataService.InstallScriptLibrary(_testData.Org1Admin1.Id);

        Assert.AreEqual(ScriptLibrary.All.Count, first.Value);
        Assert.AreEqual(0, second.Value);

        var scripts = await _dataService.GetSavedScriptsWithoutContent(_testData.Org1User1.Id, _testData.Org1Id);
        var library = scripts.Where(x => x.FolderPath == ScriptLibrary.FolderPath).ToList();
        Assert.AreEqual(ScriptLibrary.All.Count, library.Count);
        Assert.IsTrue(library.All(x => x.IsPublic));

        // Autre organisation : rien de visible, sa propre installation part de zéro.
        Assert.AreEqual(ScriptLibrary.All.Count, (await _dataService.InstallScriptLibrary(_testData.Org2Admin1.Id)).Value);
    }
}
