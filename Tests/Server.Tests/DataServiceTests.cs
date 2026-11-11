using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Services;
using Remotely.Shared.Dtos;
using Remotely.Shared.Entities;
using Remotely.Shared.Enums;
using Remotely.Shared.Models;
using Remotely.Shared.Utilities;
using Remotely.Shared.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Remotely.Server.Tests;

[TestClass]
public class DataServiceTests
{
    private readonly string _newDeviceID = "NewDeviceName";
    private IDataService _dataService = null!;
    private TestData _testData = null!;

    [TestMethod]
    public async Task AddAlert()
    {
        await _dataService.AddAlert(_testData.Org1Device1.ID, _testData.Org1Id, "Test Message");

        var alerts = _dataService.GetAlerts(_testData.Org1Admin1.Id);

        Assert.AreEqual("Test Message", alerts.First().Message);
    }

    [TestMethod]
    public async Task AddOrUpdateDevice()
    {
        var storedDevice = (await _dataService.GetDevice(_newDeviceID)).Value;

        Assert.IsNull(storedDevice);

        var newDevice = new DeviceClientDto()
        {
            ID = _newDeviceID,
            OrganizationID = _testData.Org1Id,
            DeviceName = Environment.MachineName,
            Is64Bit = Environment.Is64BitOperatingSystem
        };

        var result = await _dataService.AddOrUpdateDevice(newDevice);
        Assert.IsTrue(result.IsSuccess);

        storedDevice = (await _dataService.GetDevice(_newDeviceID)).Value;

        Assert.AreEqual(_newDeviceID, storedDevice!.ID);
        Assert.AreEqual(Environment.MachineName, storedDevice.DeviceName);
        Assert.AreEqual(Environment.Is64BitOperatingSystem, storedDevice.Is64Bit);
    }

    [TestMethod]
    public async Task CreateDevice()
    {
        var deviceOptions = new DeviceSetupOptions()
        {
            DeviceID = Guid.NewGuid().ToString(),
            DeviceAlias = "Spare Laptop",
            OrganizationID = _testData.Org1Id
        };

        // First call should create and return device.
        var savedDevice = (await _dataService.CreateDevice(deviceOptions)).Value!;
        Assert.IsInstanceOfType(savedDevice, typeof(Device));

        // Second call with same DeviceUuid should fail.
        var secondSave = await _dataService.CreateDevice(deviceOptions);
        Assert.IsFalse(secondSave.IsSuccess);
    }

    [TestMethod]
    public async Task DeviceGroupPermissions()
    {
        // Postes et utilisateurs non admins dans le groupe par défaut de leur organisation.
        Assert.AreEqual(2, _dataService.GetDevicesForUser(_testData.Org1Admin1.UserName!).Length);
        Assert.AreEqual(2, _dataService.GetDevicesForUser(_testData.Org1Admin2.UserName!).Length);
        Assert.AreEqual(2, _dataService.GetDevicesForUser(_testData.Org1User1.UserName!).Length);
        Assert.AreEqual(2, _dataService.GetDevicesForUser(_testData.Org1User2.UserName!).Length);
        Assert.IsFalse(_dataService.GetDevicesForUser(_testData.Org2User1.UserName!).Any(x => x.OrganizationID == _testData.Org1Id));
        Assert.IsFalse(_dataService.GetDevicesForUser(_testData.Org2User2.UserName!).Any(x => x.OrganizationID == _testData.Org1Id));

        Assert.IsTrue(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1Admin1));
        Assert.IsTrue(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1Admin2));
        Assert.IsTrue(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1User1));
        Assert.IsTrue(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1User2));
        Assert.IsFalse(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org2User1));
        Assert.IsFalse(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org2User2));

        var groupID = _testData.Org1Group1.ID;
        _dataService.AddUserToDeviceGroup(_testData.Org1Id, groupID, _testData.Org1User1.UserName!, out _);
        _testData.Org1Device1.DeviceGroupID = groupID;
        await _dataService.UpdateDevice(_testData.Org1Device1.ID, "", "", groupID, "");

        // Device1 passe dans Group1 (User1 seul) ; Device2 reste dans le groupe par défaut.
        Assert.AreEqual(2, _dataService.GetDevicesForUser(_testData.Org1Admin1.UserName!).Length);
        Assert.AreEqual(2, _dataService.GetDevicesForUser(_testData.Org1Admin2.UserName!).Length);
        Assert.AreEqual(2, _dataService.GetDevicesForUser(_testData.Org1User1.UserName!).Length);
        Assert.AreEqual(1, _dataService.GetDevicesForUser(_testData.Org1User2.UserName!).Length);
        Assert.IsFalse(_dataService.GetDevicesForUser(_testData.Org2User1.UserName!).Any(x => x.OrganizationID == _testData.Org1Id));
        Assert.IsFalse(_dataService.GetDevicesForUser(_testData.Org2User2.UserName!).Any(x => x.OrganizationID == _testData.Org1Id));

        Assert.IsTrue(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1Admin1));
        Assert.IsTrue(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1Admin2));
        Assert.IsTrue(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1User1));
        Assert.IsFalse(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org1User2));
        Assert.IsFalse(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org2User1));
        Assert.IsFalse(_dataService.DoesUserHaveAccessToDevice(_testData.Org1Device1.ID, _testData.Org2User2));

        var allDevices = _dataService.GetAllDevices(_testData.Org1Id).Select(x => x.ID).ToArray();
        Assert.AreEqual(2, _dataService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org1Admin1).Length);
        Assert.AreEqual(2, _dataService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org1Admin2).Length);
        Assert.AreEqual(2, _dataService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org1User1).Length);
        Assert.AreEqual(1, _dataService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org1User2).Length);
        Assert.AreEqual(0, _dataService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org2User1).Length);
        Assert.AreEqual(0, _dataService.FilterDeviceIdsByUserPermission(allDevices, _testData.Org2User2).Length);
    }

    [TestMethod]
    public async Task AddAlert_DeviceWithoutGroup_OnlyAdminsReceiveAlert()
    {
        await RemoveDeviceFromGroup(_testData.Org1Device1.ID);

        await _dataService.AddAlert(_testData.Org1Device1.ID, _testData.Org1Id, "Ungrouped");

        Assert.AreEqual(1, _dataService.GetAlerts(_testData.Org1Admin1.Id).Length);
        Assert.AreEqual(1, _dataService.GetAlerts(_testData.Org1Admin2.Id).Length);
        Assert.AreEqual(0, _dataService.GetAlerts(_testData.Org1User1.Id).Length);
        Assert.AreEqual(0, _dataService.GetAlerts(_testData.Org1User2.Id).Length);
        Assert.AreEqual(0, _dataService.GetAlerts(_testData.Org2Admin1.Id).Length);
    }

    [TestMethod]
    public async Task FilterUsersByDevicePermission_DeviceWithoutGroup_ReturnsOnlyOrgAdmins()
    {
        await RemoveDeviceFromGroup(_testData.Org1Device1.ID);

        var filtered = _dataService.FilterUsersByDevicePermission(AllUserIds(), _testData.Org1Device1.ID);

        CollectionAssert.AreEquivalent(
            new[] { _testData.Org1Admin1.Id, _testData.Org1Admin2.Id },
            filtered);
    }

    [TestMethod]
    public async Task FilterUsersByDevicePermission_DeviceInGroup_ReturnsAdminsAndGroupMembers()
    {
        var groupID = _testData.Org1Group1.ID;
        _dataService.AddUserToDeviceGroup(_testData.Org1Id, groupID, _testData.Org1User1.UserName!, out _);
        await _dataService.UpdateDevice(_testData.Org1Device1.ID, "", "", groupID, "");

        var filtered = _dataService.FilterUsersByDevicePermission(AllUserIds(), _testData.Org1Device1.ID);

        CollectionAssert.AreEquivalent(
            new[] { _testData.Org1Admin1.Id, _testData.Org1Admin2.Id, _testData.Org1User1.Id },
            filtered);
    }

    [TestMethod]
    public void FilterUsersByDevicePermission_UnknownDevice_ReturnsEmpty()
    {
        var filtered = _dataService.FilterUsersByDevicePermission(AllUserIds(), "UnknownDeviceId");

        Assert.AreEqual(0, filtered.Length);
    }

    [TestMethod]
    public async Task FilterUsersByDevicePermission_MatchesDeviceList()
    {
        // Les notifications doivent suivre exactement la règle de la liste des appareils.
        AssertNotificationsMatchDeviceList();

        var groupID = _testData.Org1Group1.ID;
        _dataService.AddUserToDeviceGroup(_testData.Org1Id, groupID, _testData.Org1User1.UserName!, out _);
        await _dataService.UpdateDevice(_testData.Org1Device1.ID, "", "", groupID, "");

        AssertNotificationsMatchDeviceList();

        // Poste retiré de tout groupe : admins seuls, liste et notifications comprises.
        await RemoveDeviceFromGroup(_testData.Org1Device2.ID);

        AssertNotificationsMatchDeviceList();
    }

    [TestMethod]
    public void DefaultGroup_CreatedWithEachOrganization()
    {
        foreach (var orgId in new[] { _testData.Org1Id, _testData.Org2Id })
        {
            var defaultGroups = _dataService
                .GetDeviceGroupsForOrganization(orgId)
                .Where(x => x.IsDefault)
                .ToArray();

            Assert.AreEqual(1, defaultGroups.Length);
            Assert.AreEqual(DeviceGroup.DefaultGroupName, defaultGroups[0].Name);
        }

        // Les groupes créés depuis la console ou l'API ne sont jamais le groupe par défaut.
        Assert.IsFalse(_testData.Org1Group1.IsDefault);
        Assert.IsFalse(_testData.Org1Group2.IsDefault);
    }

    [TestMethod]
    public async Task DefaultGroup_CannotBeMarkedFromAddDeviceGroup()
    {
        var result = await _dataService.AddDeviceGroup(_testData.Org1Id, new DeviceGroup()
        {
            Name = "Autre",
            IsDefault = true
        });

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value!.IsDefault);
        Assert.AreEqual(1, _dataService.GetDeviceGroupsForOrganization(_testData.Org1Id).Count(x => x.IsDefault));
    }

    [TestMethod]
    public async Task AddOrUpdateDevice_NewDeviceWithoutGroup_GoesToDefaultGroup()
    {
        var defaultGroup = GetDefaultGroup(_testData.Org1Id);

        Assert.AreEqual(defaultGroup.ID, _testData.Org1Device1.DeviceGroupID);
        Assert.AreEqual(defaultGroup.ID, _testData.Org1Device2.DeviceGroupID);
        Assert.AreEqual(GetDefaultGroup(_testData.Org2Id).ID, _testData.Org2Device1.DeviceGroupID);

        var newDevice = new DeviceClientDto()
        {
            ID = _newDeviceID,
            OrganizationID = _testData.Org1Id,
            DeviceName = "NouveauPoste"
        };
        var result = await _dataService.AddOrUpdateDevice(newDevice);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(defaultGroup.ID, (await _dataService.GetDevice(_newDeviceID)).Value!.DeviceGroupID);

        // Retiré du groupe par défaut par l'admin : la reconnexion suivante ne l'y remet pas.
        await RemoveDeviceFromGroup(_newDeviceID);
        result = await _dataService.AddOrUpdateDevice(newDevice);
        Assert.IsTrue(result.IsSuccess);
        Assert.IsNull((await _dataService.GetDevice(_newDeviceID)).Value!.DeviceGroupID);
    }

    [TestMethod]
    public async Task CreateDevice_WithoutGroupOrUnknownGroup_GoesToDefaultGroup()
    {
        var defaultGroup = GetDefaultGroup(_testData.Org1Id);

        var withoutGroup = await _dataService.CreateDevice(new DeviceSetupOptions()
        {
            DeviceID = Guid.NewGuid().ToString(),
            OrganizationID = _testData.Org1Id
        });
        Assert.IsTrue(withoutGroup.IsSuccess);
        Assert.AreEqual(defaultGroup.ID, withoutGroup.Value!.DeviceGroupID);

        var unknownGroup = await _dataService.CreateDevice(new DeviceSetupOptions()
        {
            DeviceID = Guid.NewGuid().ToString(),
            DeviceGroupName = "Groupe inexistant",
            OrganizationID = _testData.Org1Id
        });
        Assert.IsTrue(unknownGroup.IsSuccess);
        Assert.AreEqual(defaultGroup.ID, unknownGroup.Value!.DeviceGroupID);

        var knownGroup = await _dataService.CreateDevice(new DeviceSetupOptions()
        {
            DeviceID = Guid.NewGuid().ToString(),
            DeviceGroupName = _testData.Org1Group1.Name,
            OrganizationID = _testData.Org1Id
        });
        Assert.IsTrue(knownGroup.IsSuccess);
        Assert.AreEqual(_testData.Org1Group1.ID, knownGroup.Value!.DeviceGroupID);
    }

    [TestMethod]
    public async Task CreateUser_NonAdminInDefaultGroup_AdminNot()
    {
        Assert.IsTrue((await _dataService.CreateUser("nouveau@test.com", false, _testData.Org1Id)).IsSuccess);
        Assert.IsTrue((await _dataService.CreateUser("nouvel.admin@test.com", true, _testData.Org1Id)).IsSuccess);

        var newUser = (await _dataService.GetUserByName("nouveau@test.com")).Value!;
        var newAdmin = (await _dataService.GetUserByName("nouvel.admin@test.com")).Value!;

        var members = await GetDefaultGroupMemberIds(_testData.Org1Id);
        Assert.Contains(newUser.Id, members);
        Assert.Contains(_testData.Org1User1.Id, members);
        Assert.Contains(_testData.Org1User2.Id, members);
        Assert.DoesNotContain(newAdmin.Id, members);
        Assert.DoesNotContain(_testData.Org1Admin1.Id, members);
        Assert.DoesNotContain(_testData.Org1Admin2.Id, members);

        // Le nouvel utilisateur voit les postes du groupe par défaut.
        Assert.AreEqual(2, _dataService.GetDevicesForUser(newUser.UserName!).Length);
    }

    [TestMethod]
    public async Task ChangeUserIsAdmin_DemotedAdminJoinsDefaultGroup()
    {
        await _dataService.ChangeUserIsAdmin(_testData.Org1Id, _testData.Org1Admin2.Id, false);

        Assert.Contains(_testData.Org1Admin2.Id, await GetDefaultGroupMemberIds(_testData.Org1Id));
        Assert.AreEqual(2, _dataService.GetDevicesForUser(_testData.Org1Admin2.UserName!).Length);
    }

    [TestMethod]
    public async Task JoinViaInvitation_NonAdmin_LeavesOldGroupsAndJoinsDefaultGroup()
    {
        var invite = await _dataService.AddInvite(_testData.Org1Id, new InviteViewModel()
        {
            InvitedUser = _testData.Org2User1.UserName,
            IsAdmin = false
        });
        Assert.IsTrue(invite.IsSuccess);

        var result = await _dataService.JoinViaInvitation(_testData.Org2User1.UserName!, invite.Value!.ID);
        Assert.IsTrue(result.IsSuccess);

        Assert.Contains(_testData.Org2User1.Id, await GetDefaultGroupMemberIds(_testData.Org1Id));
        Assert.DoesNotContain(_testData.Org2User1.Id, await GetDefaultGroupMemberIds(_testData.Org2Id));

        var devices = _dataService.GetDevicesForUser(_testData.Org2User1.UserName!);
        Assert.AreEqual(2, devices.Length);
        Assert.IsTrue(devices.All(x => x.OrganizationID == _testData.Org1Id));
    }

    [TestMethod]
    public async Task DeleteDeviceGroup_DefaultGroupRefused()
    {
        var defaultGroup = GetDefaultGroup(_testData.Org1Id);

        var result = await _dataService.DeleteDeviceGroup(_testData.Org1Id, defaultGroup.ID);

        Assert.IsFalse(result.IsSuccess);
        Assert.Contains("groupe par défaut", result.Reason);
        Assert.IsTrue((await _dataService.GetDeviceGroup(defaultGroup.ID)).IsSuccess);
        Assert.AreEqual(defaultGroup.ID, (await _dataService.GetDevice(_testData.Org1Device1.ID)).Value!.DeviceGroupID);

        // Les autres groupes se suppriment toujours.
        Assert.IsTrue((await _dataService.DeleteDeviceGroup(_testData.Org1Id, _testData.Org1Group1.ID)).IsSuccess);
    }

    [TestMethod]
    public async Task AddAlert_DeviceInDefaultGroup_NonAdminMemberReceivesAlert()
    {
        await _dataService.AddAlert(_testData.Org1Device1.ID, _testData.Org1Id, "Groupe par défaut");

        Assert.AreEqual(1, _dataService.GetAlerts(_testData.Org1Admin1.Id).Length);
        Assert.AreEqual(1, _dataService.GetAlerts(_testData.Org1User1.Id).Length);
        Assert.AreEqual(1, _dataService.GetAlerts(_testData.Org1User2.Id).Length);
        Assert.AreEqual(0, _dataService.GetAlerts(_testData.Org2User1.Id).Length);

        // Membre retiré du groupe par défaut : plus d'alerte pour lui.
        Assert.IsTrue(await _dataService.RemoveUserFromDeviceGroup(_testData.Org1Id, GetDefaultGroup(_testData.Org1Id).ID, _testData.Org1User2.Id));
        await _dataService.AddAlert(_testData.Org1Device1.ID, _testData.Org1Id, "Après retrait");

        Assert.AreEqual(2, _dataService.GetAlerts(_testData.Org1User1.Id).Length);
        Assert.AreEqual(1, _dataService.GetAlerts(_testData.Org1User2.Id).Length);
    }

    private DeviceGroup GetDefaultGroup(string orgId)
    {
        return _dataService.GetDeviceGroupsForOrganization(orgId).Single(x => x.IsDefault);
    }

    private async Task<string[]> GetDefaultGroupMemberIds(string orgId)
    {
        var group = await _dataService.GetDeviceGroup(GetDefaultGroup(orgId).ID, includeUsers: true);
        return group.Value!.Users.Select(x => x.Id).ToArray();
    }

    private Task RemoveDeviceFromGroup(string deviceId)
    {
        return _dataService.UpdateDevice(deviceId, null, null, null, null);
    }

    private string[] AllUserIds()
    {
        return AllUsers().Select(x => x.Id).ToArray();
    }

    private RemotelyUser[] AllUsers()
    {
        return new[]
        {
            _testData.Org1Admin1,
            _testData.Org1Admin2,
            _testData.Org1User1,
            _testData.Org1User2,
            _testData.Org2Admin1,
            _testData.Org2Admin2,
            _testData.Org2User1,
            _testData.Org2User2
        };
    }

    private void AssertNotificationsMatchDeviceList()
    {
        var devices = new[]
        {
            _testData.Org1Device1,
            _testData.Org1Device2,
            _testData.Org2Device1,
            _testData.Org2Device2
        };

        foreach (var device in devices)
        {
            var notified = _dataService.FilterUsersByDevicePermission(AllUserIds(), device.ID);

            foreach (var user in AllUsers())
            {
                var listed = _dataService
                    .GetDevicesForUser(user.UserName!)
                    .Any(x => x.ID == device.ID);

                Assert.AreEqual(
                    listed,
                    notified.Contains(user.Id),
                    $"Appareil {device.ID}, utilisateur {user.UserName}");

                Assert.AreEqual(
                    listed,
                    _dataService.DoesUserHaveAccessToDevice(device.ID, user),
                    $"Appareil {device.ID}, utilisateur {user.UserName}");
            }
        }
    }

    [TestMethod]
    public async Task GetPendingScriptRuns_GivenMultipleRunsQueued_ReturnsOnlyLatest()
    {
        var now = Time.Now;

        var savedScript = new SavedScript()
        {
            Content = "Get-ChildItem",
            Creator = _testData.Org1Admin1,
            CreatorId = _testData.Org1Admin1.Id,
            Name = "GCI",
            Organization = _testData.Org1Admin1.Organization,
            OrganizationID = _testData.Org1Id,
            Shell = ScriptingShell.PSCore
        };

        await _dataService.AddOrUpdateSavedScript(savedScript, _testData.Org1Admin1.Id);

        var scriptRun = new ScriptRun()
        {
            Devices = new() { _testData.Org1Device1 },
            InputType = ScriptInputType.ScheduledScript,
            SavedScriptId = savedScript.Id,
            Initiator = _testData.Org1Admin1.UserName,
            RunAt = now,
            OrganizationID = _testData.Org1Id,
            Organization = _testData.Org1Admin1.Organization,
            RunOnNextConnect = true
        };

        await _dataService.AddScriptRun(scriptRun);

        scriptRun.Id = 0;
        scriptRun.RunAt = now.AddMinutes(1);

        await _dataService.AddScriptRun(scriptRun);

        Time.Adjust(TimeSpan.FromMinutes(2));

        var pendingRuns = await _dataService.GetPendingScriptRuns(_testData.Org1Device1.ID);

        Assert.AreEqual(1, pendingRuns.Count());
        Assert.AreEqual(2, pendingRuns.First().Id);

        var dto = new ScriptResultDto()
        {
            DeviceID = _testData.Org1Device1.ID,
            InputType = ScriptInputType.ScheduledScript,
            SavedScriptId = savedScript.Id,
            ScriptRunId = scriptRun.Id,
            Shell = ScriptingShell.PSCore,
            ScriptInput = "echo test"
        };

        // Un poste que l'exécution ne vise pas (ici d'une autre organisation) ne peut pas y rattacher son résultat.
        var foreignDto = new ScriptResultDto()
        {
            DeviceID = _testData.Org2Device1.ID,
            InputType = ScriptInputType.ScheduledScript,
            ScriptRunId = scriptRun.Id,
            Shell = ScriptingShell.PSCore,
            ScriptInput = "echo test"
        };
        var foreignResult = (await _dataService.AddScriptResult(foreignDto)).Value!;
        Assert.IsFalse((await _dataService.AddScriptResultToScriptRun(foreignResult.ID, scriptRun.Id)).IsSuccess);

        var scriptResult = (await _dataService.AddScriptResult(dto)).Value!;

        Assert.IsTrue((await _dataService.AddScriptResultToScriptRun(scriptResult.ID, scriptRun.Id)).IsSuccess);

        pendingRuns = await _dataService.GetPendingScriptRuns(_testData.Org1Device1.ID);

        Assert.AreEqual(0, pendingRuns.Count());
    }


    [TestCleanup]
    public void TestCleanup()
    {
        _testData.ClearData();
    }

    [TestInitialize]
    public async Task TestInit()
    {
        _testData = new TestData();
        await _testData.Init();
        _dataService = IoCActivator.ServiceProvider.GetRequiredService<IDataService>();
    }

    [TestMethod]
    public async Task UpdateOrganizationName()
    {
        Assert.AreEqual("Org1", _testData.Org1Admin1.Organization!.OrganizationName);
        await _dataService.UpdateOrganizationName(_testData.Org1Id, "Test Org");
        var updatedOrg = (await _dataService.GetOrganizationById(_testData.Org1Id)).Value;
        Assert.AreEqual("Test Org", updatedOrg!.OrganizationName);
    }

    [TestMethod]
    public async Task UpdateServerAdmins()
    {
        var currentAdmins = _dataService.GetServerAdmins();

        Assert.AreEqual(1, currentAdmins.Count);

        await _dataService.SetIsServerAdmin(_testData.Org1Admin2.Id, true, _testData.Org1Admin1.Id);

        currentAdmins = _dataService.GetServerAdmins();
        Assert.AreEqual(2, currentAdmins.Count);
        Assert.IsTrue(currentAdmins.Contains(_testData.Org1Admin1.UserName!));
        Assert.IsTrue(currentAdmins.Contains(_testData.Org1Admin2.UserName!));

        // Shouldn't be able to change themselves.
        await _dataService.SetIsServerAdmin(_testData.Org1Admin2.Id, false, _testData.Org1Admin2.Id);
        currentAdmins = _dataService.GetServerAdmins();
        Assert.AreEqual(2, currentAdmins.Count);

        // Non-admins shouldn't be able to change admins.
        await _dataService.SetIsServerAdmin(_testData.Org1User1.Id, false, _testData.Org1Admin2.Id);
        currentAdmins = _dataService.GetServerAdmins();
        Assert.AreEqual(2, currentAdmins.Count);

        await _dataService.SetIsServerAdmin(_testData.Org1Admin2.Id, false, _testData.Org1Admin1.Id);
        currentAdmins = _dataService.GetServerAdmins();
        Assert.AreEqual(1, currentAdmins.Count);
        Assert.AreEqual(_testData.Org1Admin1.UserName, currentAdmins[0]);
    }

    [TestMethod]
    public async Task VerifyInitialData()
    {
        Assert.IsNotNull((await _dataService.GetUserByName(_testData.Org1Admin1.UserName!)).Value);
        Assert.IsNotNull((await _dataService.GetUserByName(_testData.Org1Admin2.UserName!)).Value);
        Assert.IsNotNull((await _dataService.GetUserByName(_testData.Org1User1.UserName!)).Value);
        Assert.IsNotNull((await _dataService.GetUserByName(_testData.Org1User2.UserName!)).Value);
        Assert.AreEqual(2, _dataService.GetOrganizationCount());

        var devices1 = _dataService.GetAllDevices(_testData.Org1Id);
        Assert.AreEqual(2, devices1.Length);
        Assert.IsTrue(devices1.Any(x => x.ID == "Org1Device1"));
        Assert.IsTrue(devices1.Any(x => x.ID == "Org1Device2"));

        var devices2 = _dataService.GetAllDevices(_testData.Org2Id);
        Assert.AreEqual(2, devices2.Length);
        Assert.IsTrue(devices2.Any(x => x.ID == "Org2Device1"));
        Assert.IsTrue(devices2.Any(x => x.ID == "Org2Device2"));

        var org1Ids = new string[]
        {
            _testData.Org1Group1.OrganizationID,
            _testData.Org1Group2.OrganizationID,
            _testData.Org1Admin1.OrganizationID,
            _testData.Org1Admin2.OrganizationID,
            _testData.Org1User1.OrganizationID,
            _testData.Org1User2.OrganizationID,
            _testData.Org1Device1.OrganizationID,
            _testData.Org1Device2.OrganizationID
        };

        Assert.IsTrue(org1Ids.All(x => x == _testData.Org1Id));

        var org2Ids = new string[]
{
            _testData.Org2Group1.OrganizationID,
            _testData.Org2Group2.OrganizationID,
            _testData.Org2Admin1.OrganizationID,
            _testData.Org2Admin2.OrganizationID,
            _testData.Org2User1.OrganizationID,
            _testData.Org2User2.OrganizationID,
            _testData.Org2Device1.OrganizationID,
            _testData.Org2Device2.OrganizationID
};

        Assert.IsTrue(org2Ids.All(x => x == _testData.Org2Id));
    }
}
