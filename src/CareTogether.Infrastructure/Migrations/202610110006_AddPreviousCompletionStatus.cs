using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610110006_AddPreviousCompletionStatus")]
public sealed class AddPreviousCompletionStatus : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE CareTasks ADD PreviousStatus int NULL;");
        m.Sql("ALTER TABLE Appointments ADD PreviousStatus int NULL;");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE Appointments DROP COLUMN PreviousStatus;");
        m.Sql("ALTER TABLE CareTasks DROP COLUMN PreviousStatus;");
    }
}
