using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610120003_AddMedicationActions")]
public sealed class AddMedicationActions : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.Sql("ALTER TABLE Medications ADD PreviousStatus int NULL;");
        m.Sql("ALTER TABLE CareDocuments ADD MedicationId uniqueidentifier NULL;");
        m.Sql("ALTER TABLE CareDocuments ADD CONSTRAINT FK_CareDocuments_Medications_MedicationId FOREIGN KEY (MedicationId) REFERENCES Medications(Id) ON DELETE SET NULL;");
        m.Sql("CREATE INDEX IX_CareDocuments_MedicationId ON CareDocuments(MedicationId);");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP INDEX IX_CareDocuments_MedicationId ON CareDocuments;");
        m.Sql("ALTER TABLE CareDocuments DROP CONSTRAINT FK_CareDocuments_Medications_MedicationId;");
        m.Sql("ALTER TABLE CareDocuments DROP COLUMN MedicationId;");
        m.Sql("ALTER TABLE Medications DROP COLUMN PreviousStatus;");
    }
}
