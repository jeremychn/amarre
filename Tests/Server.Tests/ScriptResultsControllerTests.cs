using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Remotely.Server.API;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using System.Linq;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class ScriptResultsControllerTests
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
    public void TestCleanup()
    {
        _testData.ClearData();
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Post_ScriptDeLaMemeOrganisation_Alerte()
    {
        var script = await AddScript(_testData.Org1Admin1.Id);

        var result = await Post(_testData.Org1Id, _testData.Org1Device1.ID, script.Id);

        Assert.IsNotInstanceOfType<NotFoundResult>(result.Result);
        Assert.AreEqual(1, _dataService.GetAlerts(_testData.Org1Admin1.Id).Count());
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task Post_ScriptDUneAutreOrganisation_Refuse()
    {
        var script = await AddScript(_testData.Org2Admin1.Id);

        var result = await Post(_testData.Org1Id, _testData.Org1Device1.ID, script.Id);

        Assert.IsInstanceOfType<NotFoundResult>(result.Result);
        Assert.AreEqual(0, _dataService.GetAlerts(_testData.Org2Admin1.Id).Count());
    }

    [TestMethod]
    [DoNotParallelize]
    public async Task AddScriptResult_ScriptDUneAutreOrganisation_PasRattache()
    {
        var autre = await AddScript(_testData.Org2Admin1.Id);
        var meme = await AddScript(_testData.Org1Admin1.Id);

        var refuse = await _dataService.AddScriptResult(new ScriptResultDto { DeviceID = _testData.Org1Device1.ID, SavedScriptId = autre.Id });
        var garde = await _dataService.AddScriptResult(new ScriptResultDto { DeviceID = _testData.Org1Device1.ID, SavedScriptId = meme.Id });

        Assert.IsNull((await _dataService.GetScriptResult(refuse.Value!.ID)).Value!.SavedScriptId);
        Assert.AreEqual(meme.Id, (await _dataService.GetScriptResult(garde.Value!.ID)).Value!.SavedScriptId);
    }

    private async Task<SavedScript> AddScript(string userId)
    {
        var script = new SavedScript
        {
            Name = "Script en erreur",
            Content = "exit 1",
            GenerateAlertOnError = true,
        };
        Assert.IsTrue((await _dataService.AddOrUpdateSavedScript(script, userId)).IsSuccess);
        return script;
    }

    private async Task<ActionResult<ScriptResultResponse>> Post(string orgId, string deviceId, System.Guid savedScriptId)
    {
        var controller = new ScriptResultsController(
            _dataService,
            new Mock<IEmailSenderEx>().Object,
            new Mock<ILogger<ScriptResultsController>>().Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
        // Posé par ExpiringTokenFilter d'après le jeton remis à l'agent.
        controller.Request.Headers["OrganizationID"] = orgId;

        return await controller.Post(new ScriptResultDto
        {
            DeviceID = deviceId,
            HadErrors = true,
            ErrorOutput = ["erreur"],
            SavedScriptId = savedScriptId,
        });
    }
}