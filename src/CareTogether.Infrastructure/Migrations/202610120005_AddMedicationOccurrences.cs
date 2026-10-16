using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610120005_AddMedicationOccurrences")]
public sealed class AddMedicationOccurrences : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE Medications ADD ScheduleUtcOffsetMinutes int NOT NULL CONSTRAINT DF_Medications_ScheduleUtcOffsetMinutes DEFAULT 0;");
        m.Sql("CREATE TABLE MedicationOccurrences (Id uniqueidentifier NOT NULL PRIMARY KEY, CreatedAt datetimeoffset NOT NULL DEFAULT SYSUTCDATETIME(), MedicationId uniqueidentifier NOT NULL, ScheduledAt datetimeoffset NOT NULL, CONSTRAINT FK_MedicationOccurrences_Medications_MedicationId FOREIGN KEY (MedicationId) REFERENCES Medications(Id) ON DELETE CASCADE);");
        m.Sql("CREATE UNIQUE INDEX IX_MedicationOccurrences_MedicationId_ScheduledAt ON MedicationOccurrences(MedicationId,ScheduledAt);");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP TABLE MedicationOccurrences;");
        m.Sql("ALTER TABLE Medications DROP CONSTRAINT DF_Medications_ScheduleUtcOffsetMinutes;");
        m.Sql("ALTER TABLE Medications DROP COLUMN ScheduleUtcOffsetMinutes;");
    }
}
