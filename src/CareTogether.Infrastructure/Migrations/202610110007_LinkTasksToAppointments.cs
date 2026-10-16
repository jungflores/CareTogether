using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610110007_LinkTasksToAppointments")]
public sealed class LinkTasksToAppointments : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE CareTasks ADD AppointmentId uniqueidentifier NULL;");
        m.Sql("ALTER TABLE CareTasks ADD CONSTRAINT FK_CareTasks_Appointments_AppointmentId FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id) ON DELETE SET NULL;");
        m.Sql("CREATE UNIQUE INDEX IX_CareTasks_AppointmentId ON CareTasks(AppointmentId) WHERE AppointmentId IS NOT NULL;");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP INDEX IX_CareTasks_AppointmentId ON CareTasks;");
        m.Sql("ALTER TABLE CareTasks DROP CONSTRAINT FK_CareTasks_Appointments_AppointmentId;");
        m.Sql("ALTER TABLE CareTasks DROP COLUMN AppointmentId;");
    }
}
