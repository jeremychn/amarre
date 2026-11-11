using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Remotely.Server.Migrations.Sqlite;

/// <inheritdoc />
public partial class Add_DefaultDeviceGroup : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsDefault",
            table: "DeviceGroups",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);

        // Groupe par défaut « Tous » dans chaque organisation : un groupe existant nommé « Tous »
        // le devient, sinon il est créé. Il reçoit les postes sans groupe et les utilisateurs non administrateurs.
        migrationBuilder.Sql("update \"DeviceGroups\" set \"IsDefault\" = 1 where \"ID\" in (select min(g.\"ID\") from \"DeviceGroups\" g where lower(g.\"Name\") = 'tous' group by g.\"OrganizationID\");");
        migrationBuilder.Sql("insert into \"DeviceGroups\" (\"ID\", \"Name\", \"OrganizationID\", \"IsDefault\") select lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))), 2) || '-' || substr('89ab', 1 + (abs(random()) % 4), 1) || substr(lower(hex(randomblob(2))), 2) || '-' || lower(hex(randomblob(6))), 'Tous', o.\"ID\", 1 from \"Organizations\" o where not exists (select 1 from \"DeviceGroups\" g where g.\"OrganizationID\" = o.\"ID\" and g.\"IsDefault\" = 1);");
        migrationBuilder.Sql("update \"Devices\" set \"DeviceGroupID\" = (select g.\"ID\" from \"DeviceGroups\" g where g.\"OrganizationID\" = \"Devices\".\"OrganizationID\" and g.\"IsDefault\" = 1) where \"DeviceGroupID\" is null;");
        migrationBuilder.Sql("insert into \"DeviceGroupRemotelyUser\" (\"DeviceGroupsID\", \"UsersId\") select g.\"ID\", u.\"Id\" from \"RemotelyUsers\" u inner join \"DeviceGroups\" g on g.\"OrganizationID\" = u.\"OrganizationID\" and g.\"IsDefault\" = 1 where u.\"IsAdministrator\" = 0 and not exists (select 1 from \"DeviceGroupRemotelyUser\" j where j.\"DeviceGroupsID\" = g.\"ID\" and j.\"UsersId\" = u.\"Id\");");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Les groupes « Tous », leurs postes et leurs membres restent en place comme groupes ordinaires.
        migrationBuilder.DropColumn(
            name: "IsDefault",
            table: "DeviceGroups");
    }
}
