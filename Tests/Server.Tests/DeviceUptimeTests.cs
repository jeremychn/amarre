using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Remotely.Server.Data;
using Remotely.Server.Services;
using Remotely.Shared.Entities;
using System;

namespace Remotely.Server.Tests;

[TestClass]
public class DeviceUptimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    [DataRow(0.5, "30 min", false)]
    [DataRow(5.0, "5 h", false)]
    [DataRow(24.0 * 7, "7 j", true)]
    [DataRow(24.0 * 40, "40 j", true)]
    public void Duree(double hours, string expected, bool isLong)
    {
        var device = new Device { IsOnline = true, LastBootTime = Now.AddHours(-hours) };
        Assert.AreEqual(expected, DeviceUptime.Format(DeviceUptime.Get(device, Now)));
        Assert.AreEqual(isLong, DeviceUptime.IsLong(device, Now));
    }

    [TestMethod]
    public void HorsLigneOuAgentAncien_Vide()
    {
        Assert.IsNull(DeviceUptime.Get(new Device { IsOnline = false, LastBootTime = Now.AddDays(-40) }, Now));
        Assert.IsNull(DeviceUptime.Get(new Device { IsOnline = true }, Now));
        Assert.AreEqual("—", DeviceUptime.Format(null));
    }

    [TestMethod]
    public void HorlogeDuPosteEnAvance_Zero() =>
        Assert.AreEqual(TimeSpan.Zero, DeviceUptime.Get(new Device { IsOnline = true, LastBootTime = Now.AddMinutes(5) }, Now));

    // Migrations écrites à la main : le modèle doit correspondre exactement à l'instantané de chaque fournisseur.
    [TestMethod]
    public void Migrations_Sqlite_AJour()
    {
        using var db = new SqliteDbContextDesignTime().CreateDbContext([]);
        Assert.IsFalse(db.Database.HasPendingModelChanges());
    }

    [TestMethod]
    public void Migrations_PostgreSql_AJour()
    {
        using var db = new PostgreSqlDbContextDesignTime().CreateDbContext([]);
        Assert.IsFalse(db.Database.HasPendingModelChanges());
    }
}
