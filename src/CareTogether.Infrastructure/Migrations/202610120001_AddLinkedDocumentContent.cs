using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610120001_AddLinkedDocumentContent")]
public sealed class AddLinkedDocumentContent : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE CareDocuments ADD TaskId uniqueidentifier NULL, AppointmentId uniqueidentifier NULL, ContentType nvarchar(200) NULL, FileContent varbinary(max) NULL;");
        m.Sql("ALTER TABLE CareDocuments ADD CONSTRAINT FK_CareDocuments_CareTasks_TaskId FOREIGN KEY (TaskId) REFERENCES CareTasks(Id) ON DELETE SET NULL;");
        m.Sql("ALTER TABLE CareDocuments ADD CONSTRAINT FK_CareDocuments_Appointments_AppointmentId FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id) ON DELETE SET NULL;");
        m.Sql("CREATE INDEX IX_CareDocuments_TaskId ON CareDocuments(TaskId);");
        m.Sql("CREATE INDEX IX_CareDocuments_AppointmentId ON CareDocuments(AppointmentId);");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP INDEX IX_CareDocuments_TaskId ON CareDocuments; DROP INDEX IX_CareDocuments_AppointmentId ON CareDocuments;");
        m.Sql("ALTER TABLE CareDocuments DROP CONSTRAINT FK_CareDocuments_CareTasks_TaskId; ALTER TABLE CareDocuments DROP CONSTRAINT FK_CareDocuments_Appointments_AppointmentId;");
        m.Sql("ALTER TABLE CareDocuments DROP COLUMN TaskId, AppointmentId, ContentType, FileContent;");
    }
}
