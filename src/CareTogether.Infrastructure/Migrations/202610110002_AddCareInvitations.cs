using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610110002_AddCareInvitations")]
public sealed class AddCareInvitations : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE CareInvitations (
 Id uniqueidentifier NOT NULL PRIMARY KEY,
 CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(),
 CareCircleId uniqueidentifier NOT NULL REFERENCES CareCircles(Id) ON DELETE CASCADE,
 Email nvarchar(320) NOT NULL,
 Role int NOT NULL,
 Message nvarchar(max) NULL,
 Token nvarchar(128) NOT NULL,
 Status int NOT NULL,
 InvitedByUserId uniqueidentifier NOT NULL REFERENCES Users(Id),
 ExpiresAt datetimeoffset NOT NULL
);
CREATE INDEX IX_CareInvitations_CareCircleId_Email_Status ON CareInvitations(CareCircleId,Email,Status);
""");
    protected override void Down(MigrationBuilder m) => m.Sql("DROP TABLE CareInvitations;");
}
