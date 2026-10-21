using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610200001_ClosedLoopHandoffs")]
public sealed class ClosedLoopHandoffs : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<DateTimeOffset>(name:"CoverageStartsAt",table:"CareHandoffs",type:"datetimeoffset",nullable:true);
        m.AddColumn<DateTimeOffset>(name:"CoverageEndsAt",table:"CareHandoffs",type:"datetimeoffset",nullable:true);
        m.AddColumn<bool>(name:"MedicationReviewed",table:"CareHandoffs",type:"bit",nullable:false,defaultValue:false);
        m.AddColumn<bool>(name:"SafetyReviewed",table:"CareHandoffs",type:"bit",nullable:false,defaultValue:false);
        m.AddColumn<bool>(name:"ScheduleReviewed",table:"CareHandoffs",type:"bit",nullable:false,defaultValue:false);
        m.AddColumn<bool>(name:"EmergencyPlanReviewed",table:"CareHandoffs",type:"bit",nullable:false,defaultValue:false);
        m.AddColumn<bool>(name:"InstructionsReviewed",table:"CareHandoffs",type:"bit",nullable:false,defaultValue:false);
        m.AddColumn<DateTimeOffset>(name:"ActivatedAt",table:"CareHandoffs",type:"datetimeoffset",nullable:true);
        m.AddColumn<DateTimeOffset>(name:"CompletedAt",table:"CareHandoffs",type:"datetimeoffset",nullable:true);
        m.AddColumn<string>(name:"CompletionNotes",table:"CareHandoffs",type:"nvarchar(max)",nullable:true);
        m.AddColumn<string>(name:"UnresolvedItems",table:"CareHandoffs",type:"nvarchar(max)",nullable:true);
        m.AddColumn<Guid>(name:"CarriedForwardFromId",table:"CareHandoffs",type:"uniqueidentifier",nullable:true);
        m.CreateIndex(name:"IX_CareHandoffs_CareCircleId_Status_CoverageStartsAt",table:"CareHandoffs",columns:new[]{"CareCircleId","Status","CoverageStartsAt"});
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropIndex(name:"IX_CareHandoffs_CareCircleId_Status_CoverageStartsAt",table:"CareHandoffs");
        m.DropColumn(name:"CoverageStartsAt",table:"CareHandoffs");m.DropColumn(name:"CoverageEndsAt",table:"CareHandoffs");m.DropColumn(name:"MedicationReviewed",table:"CareHandoffs");m.DropColumn(name:"SafetyReviewed",table:"CareHandoffs");m.DropColumn(name:"ScheduleReviewed",table:"CareHandoffs");m.DropColumn(name:"EmergencyPlanReviewed",table:"CareHandoffs");m.DropColumn(name:"InstructionsReviewed",table:"CareHandoffs");m.DropColumn(name:"ActivatedAt",table:"CareHandoffs");m.DropColumn(name:"CompletedAt",table:"CareHandoffs");m.DropColumn(name:"CompletionNotes",table:"CareHandoffs");m.DropColumn(name:"UnresolvedItems",table:"CareHandoffs");m.DropColumn(name:"CarriedForwardFromId",table:"CareHandoffs");
    }
}
