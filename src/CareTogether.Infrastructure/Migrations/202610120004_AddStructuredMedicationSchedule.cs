using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610120004_AddStructuredMedicationSchedule")]
public sealed class AddStructuredMedicationSchedule : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE Medications ADD ScheduleType int NOT NULL CONSTRAINT DF_Medications_ScheduleType DEFAULT 1, OnceAt datetimeoffset NULL, Occurrences int NULL, TimesOfDay nvarchar(200) NULL, StartDate date NULL, EndDate date NULL, WeeklyDay int NULL, MonthlyDay int NULL;");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE Medications DROP CONSTRAINT DF_Medications_ScheduleType;");
        m.Sql("ALTER TABLE Medications DROP COLUMN ScheduleType, OnceAt, Occurrences, TimesOfDay, StartDate, EndDate, WeeklyDay, MonthlyDay;");
    }
}
