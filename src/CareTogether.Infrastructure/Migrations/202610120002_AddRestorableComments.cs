using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610120002_AddRestorableComments")]
public sealed class AddRestorableComments : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE CareTasks ADD PreviousDetails nvarchar(max) NULL;");
        m.Sql("ALTER TABLE Appointments ADD PreviousNotes nvarchar(max) NULL;");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE CareTasks DROP COLUMN PreviousDetails;");
        m.Sql("ALTER TABLE Appointments DROP COLUMN PreviousNotes;");
    }
}
