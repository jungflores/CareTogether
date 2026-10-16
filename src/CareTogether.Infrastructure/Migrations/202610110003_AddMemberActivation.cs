using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610110003_AddMemberActivation")]
public sealed class AddMemberActivation : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("ALTER TABLE CareTeamMembers ADD IsActive bit NOT NULL CONSTRAINT DF_CareTeamMembers_IsActive DEFAULT 1, DeactivatedAt datetimeoffset NULL;");
    protected override void Down(MigrationBuilder m) => m.Sql("ALTER TABLE CareTeamMembers DROP CONSTRAINT DF_CareTeamMembers_IsActive; ALTER TABLE CareTeamMembers DROP COLUMN IsActive, DeactivatedAt;");
}
