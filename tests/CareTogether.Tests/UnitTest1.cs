using CareTogether.Domain;
namespace CareTogether.Tests;
public class DomainTests
{
 [Fact] public void NewMedicationIsActive(){var item=new Medication{CareCircleId=Guid.NewGuid(),Name="Example",Dosage="1 tablet",Schedule="Daily"};Assert.Equal(MedicationStatus.Active,item.Status);}
 [Fact] public void NewHandoffBeginsAsDraft(){var item=new CareHandoff{CareCircleId=Guid.NewGuid(),FromUserId=Guid.NewGuid(),ToUserId=Guid.NewGuid(),Summary="Status"};Assert.Equal(HandoffStatus.Draft,item.Status);}
}
