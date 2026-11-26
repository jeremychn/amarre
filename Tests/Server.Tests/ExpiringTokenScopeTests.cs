using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;
using System;

namespace Remotely.Server.Tests;

// L1 : le jeton remis à l'agent ne vaut plus 30 minutes pour toute l'organisation, mais pour un
// script (ou des fichiers), un poste et un seul résultat.
[TestClass]
public class ExpiringTokenScopeTests
{
    private const string OrgId = "org-1";
    private const string DeviceId = "poste-1";
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ScriptId = Guid.NewGuid();

    [TestMethod]
    public void EnvoiDuPoste_UneFois_CinqMinutes_RienDAutre()
    {
        var scope = ExpiringTokenScope.ForDeviceUpload(OrgId, DeviceId, Now);

        Assert.IsFalse(scope.CanReadFile("fichier", Now));
        Assert.IsFalse(scope.CanReadScript(ScriptId, Now));
        Assert.IsFalse(ExpiringTokenScope.ForDeviceUpload(OrgId, DeviceId, Now).TryUseForUpload(Now.AddMinutes(6)));
        Assert.IsTrue(scope.TryUseForUpload(Now.AddMinutes(1)));
        Assert.IsFalse(scope.TryUseForUpload(Now.AddMinutes(1)));
        Assert.IsFalse(ExpiringTokenScope.ForFiles(OrgId, DeviceId, ["f"], Now).TryUseForUpload(Now));
    }

    [TestMethod]
    public void Script_SeulementLeScriptDuJeton_PendantDeuxMinutes()
    {
        var scope = ExpiringTokenScope.ForScriptRun(OrgId, DeviceId, ScriptId, 7, Now);

        Assert.IsTrue(scope.CanReadScript(ScriptId, Now.AddSeconds(5)));
        Assert.IsFalse(scope.CanReadScript(Guid.NewGuid(), Now.AddSeconds(5)));
        Assert.IsFalse(scope.CanReadScript(ScriptId, Now.AddMinutes(3)));
        Assert.IsFalse(scope.CanReadFile("fichier", Now));
    }

    [TestMethod]
    public void Script_UnSeulResultat_PourCePosteCeScriptCetteExecution()
    {
        var now = Now.AddMinutes(20);

        Assert.IsFalse(Scope().TryUseForResult(Result("autre-poste", ScriptId, 7), now));
        Assert.IsFalse(Scope().TryUseForResult(Result(DeviceId, Guid.NewGuid(), 7), now));
        Assert.IsFalse(Scope().TryUseForResult(Result(DeviceId, ScriptId, 8), now));
        Assert.IsFalse(Scope().TryUseForResult(Result(DeviceId, ScriptId, 7), Now.AddMinutes(31)));

        var scope = Scope();
        Assert.IsTrue(scope.TryUseForResult(Result(DeviceId, ScriptId, 7), now));
        Assert.IsFalse(scope.TryUseForResult(Result(DeviceId, ScriptId, 7), now));

        static ExpiringTokenScope Scope() => ExpiringTokenScope.ForScriptRun(OrgId, DeviceId, ScriptId, 7, Now);
    }

    [TestMethod]
    public void Commande_ResultatSansScript_RienALire()
    {
        var scope = ExpiringTokenScope.ForCommand(OrgId, DeviceId, ExpiringTokenScope.TerminalResultWindow, Now);

        Assert.IsFalse(scope.CanReadScript(ScriptId, Now));
        Assert.IsFalse(scope.TryUseForResult(Result(DeviceId, ScriptId, null), Now));
        Assert.IsFalse(scope.TryUseForResult(Result(DeviceId, null, null), Now.AddMinutes(6)));
        Assert.IsTrue(scope.TryUseForResult(Result(DeviceId, null, null), Now.AddMinutes(1)));
    }

    [TestMethod]
    public void Fichiers_SeulementCeuxDuJeton_SansResultat()
    {
        var scope = ExpiringTokenScope.ForFiles(OrgId, DeviceId, ["f1", "f2"], Now);

        Assert.IsTrue(scope.CanReadFile("f1", Now.AddMinutes(1)));
        Assert.IsTrue(scope.CanReadFile("f2", Now.AddMinutes(4)));
        Assert.IsFalse(scope.CanReadFile("f3", Now));
        Assert.IsFalse(scope.CanReadFile("f1", Now.AddMinutes(6)));
        Assert.IsFalse(scope.CanReadScript(ScriptId, Now));
        Assert.IsFalse(scope.TryUseForResult(Result(DeviceId, null, null), Now));
    }

    [TestMethod]
    public void Service_JetonRetrouve_InconnuRefuse()
    {
        var service = new ExpiringTokenService();
        var scope = ExpiringTokenScope.ForFiles(OrgId, DeviceId, ["f1"], DateTimeOffset.Now);

        var secret = service.GetToken(scope);

        Assert.IsTrue(service.TryGetToken(secret, out var found));
        Assert.AreSame(scope, found);
        Assert.IsFalse(service.TryGetToken("inconnu", out _));
    }

    private static ScriptResultDto Result(string deviceId, Guid? savedScriptId, int? scriptRunId) => new()
    {
        DeviceID = deviceId,
        SavedScriptId = savedScriptId,
        ScriptRunId = scriptRunId,
    };
}
