using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace CareTogether.Infrastructure.Migrations;
[DbContext(typeof(CareDbContext))]
[Migration("202610110001_InitialCreate")]
public sealed class InitialCreate : Migration
{
 protected override void Up(MigrationBuilder m) => m.Sql("""
CREATE TABLE Users (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), Email nvarchar(320) NOT NULL, PasswordHash nvarchar(max) NOT NULL, DisplayName nvarchar(200) NOT NULL, Role int NOT NULL);
CREATE UNIQUE INDEX IX_Users_Email ON Users(Email);
CREATE TABLE CareCircles (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), Name nvarchar(200) NOT NULL, RecipientName nvarchar(200) NOT NULL, Notes nvarchar(max) NULL);
CREATE TABLE CareTeamMembers (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), CareCircleId uniqueidentifier NOT NULL REFERENCES CareCircles(Id) ON DELETE CASCADE, UserId uniqueidentifier NOT NULL REFERENCES Users(Id), Role int NOT NULL);
CREATE UNIQUE INDEX IX_CareTeamMembers_CareCircleId_UserId ON CareTeamMembers(CareCircleId,UserId);
CREATE TABLE CareTasks (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), CareCircleId uniqueidentifier NOT NULL, Title nvarchar(300) NOT NULL, Details nvarchar(max) NULL, DueAt datetimeoffset NOT NULL, AssignedToUserId uniqueidentifier NULL, IsCompleted bit NOT NULL);
CREATE TABLE Medications (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), CareCircleId uniqueidentifier NOT NULL, Name nvarchar(300) NOT NULL, Dosage nvarchar(100) NOT NULL, Schedule nvarchar(300) NOT NULL, Instructions nvarchar(max) NULL, Status int NOT NULL);
CREATE TABLE MedicationLogs (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), MedicationId uniqueidentifier NOT NULL, ScheduledAt datetimeoffset NOT NULL, TakenAt datetimeoffset NULL, Skipped bit NOT NULL, Note nvarchar(max) NULL);
CREATE TABLE Appointments (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), CareCircleId uniqueidentifier NOT NULL, Title nvarchar(300) NOT NULL, Provider nvarchar(300) NOT NULL, StartsAt datetimeoffset NOT NULL, Location nvarchar(500) NULL, Notes nvarchar(max) NULL, Status int NOT NULL);
CREATE TABLE Conversations (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), CareCircleId uniqueidentifier NOT NULL, Title nvarchar(300) NOT NULL);
CREATE TABLE Messages (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), ConversationId uniqueidentifier NOT NULL REFERENCES Conversations(Id) ON DELETE CASCADE, SenderUserId uniqueidentifier NOT NULL, Body nvarchar(max) NOT NULL, ReadAt datetimeoffset NULL);
CREATE TABLE CareHandoffs (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), CareCircleId uniqueidentifier NOT NULL, FromUserId uniqueidentifier NOT NULL, ToUserId uniqueidentifier NOT NULL, Summary nvarchar(max) NOT NULL, Risks nvarchar(max) NULL, NextSteps nvarchar(max) NULL, Status int NOT NULL, AcceptedAt datetimeoffset NULL);
CREATE TABLE RefreshTokens (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), UserId uniqueidentifier NOT NULL, TokenHash nvarchar(128) NOT NULL, ExpiresAt datetimeoffset NOT NULL, RevokedAt datetimeoffset NULL);
""");
 protected override void Down(MigrationBuilder m) => m.Sql("DROP TABLE RefreshTokens; DROP TABLE CareHandoffs; DROP TABLE Messages; DROP TABLE Conversations; DROP TABLE Appointments; DROP TABLE MedicationLogs; DROP TABLE Medications; DROP TABLE CareTasks; DROP TABLE CareTeamMembers; DROP TABLE CareCircles; DROP TABLE Users;");
}
