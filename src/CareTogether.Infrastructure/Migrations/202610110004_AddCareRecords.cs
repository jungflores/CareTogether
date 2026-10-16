using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610110004_AddCareRecords")]
public sealed class AddCareRecords : Migration
{
 protected override void Up(MigrationBuilder m)=>m.Sql("""
CREATE TABLE JournalEntries (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), CareCircleId uniqueidentifier NOT NULL REFERENCES CareCircles(Id) ON DELETE CASCADE, AuthorUserId uniqueidentifier NOT NULL REFERENCES Users(Id), Body nvarchar(max) NOT NULL, Category nvarchar(100) NULL);
CREATE TABLE HealthReadings (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), CareCircleId uniqueidentifier NOT NULL REFERENCES CareCircles(Id) ON DELETE CASCADE, Type nvarchar(100) NOT NULL, Value decimal(18,2) NOT NULL, SecondaryValue decimal(18,2) NULL, Pulse int NULL, RecordedAt datetimeoffset NOT NULL);
CREATE TABLE CareDocuments (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), CareCircleId uniqueidentifier NOT NULL REFERENCES CareCircles(Id) ON DELETE CASCADE, Name nvarchar(300) NOT NULL, Category nvarchar(100) NOT NULL, Notes nvarchar(max) NULL);
CREATE TABLE EmergencyContacts (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), CareCircleId uniqueidentifier NOT NULL REFERENCES CareCircles(Id) ON DELETE CASCADE, Name nvarchar(200) NOT NULL, Relationship nvarchar(100) NOT NULL, Phone nvarchar(100) NOT NULL, IsPrimary bit NOT NULL);
""");
 protected override void Down(MigrationBuilder m)=>m.Sql("DROP TABLE EmergencyContacts; DROP TABLE CareDocuments; DROP TABLE HealthReadings; DROP TABLE JournalEntries;");
}
