using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610110005_AddTaskStatus")]
public sealed class AddTaskStatus : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE CareTasks ADD Status int NOT NULL CONSTRAINT DF_CareTasks_Status DEFAULT 0;");
        m.Sql("UPDATE CareTasks SET Status = 3 WHERE IsCompleted = 1;");
        m.Sql("UPDATE CareTasks SET Status = 1 WHERE IsCompleted = 0 AND AssignedToUserId IS NOT NULL;");
    }

    protected override void Down(MigrationBuilder m) => m.Sql("ALTER TABLE CareTasks DROP CONSTRAINT DF_CareTasks_Status; ALTER TABLE CareTasks DROP COLUMN Status;");
}
