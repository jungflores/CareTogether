using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610140001_CompleteHandoffWorkflow")]
public sealed class CompleteHandoffWorkflow : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<DateTimeOffset>(name:"DeclinedAt",table:"CareHandoffs",type:"datetimeoffset",nullable:true);
        m.AddColumn<DateTimeOffset>(name:"CancelledAt",table:"CareHandoffs",type:"datetimeoffset",nullable:true);
        m.CreateTable(name:"UserNotifications",columns:t=>new{Id=t.Column<Guid>(type:"uniqueidentifier",nullable:false),CreatedAt=t.Column<DateTimeOffset>(type:"datetimeoffset",nullable:false,defaultValueSql:"SYSUTCDATETIME()"),UserId=t.Column<Guid>(type:"uniqueidentifier",nullable:false),Type=t.Column<string>(type:"nvarchar(max)",nullable:false),Title=t.Column<string>(type:"nvarchar(max)",nullable:false),Body=t.Column<string>(type:"nvarchar(max)",nullable:false),RelatedId=t.Column<Guid>(type:"uniqueidentifier",nullable:true),ReadAt=t.Column<DateTimeOffset>(type:"datetimeoffset",nullable:true)},constraints:t=>t.PrimaryKey("PK_UserNotifications",x=>x.Id));
        m.CreateIndex(name:"IX_UserNotifications_UserId_ReadAt_CreatedAt",table:"UserNotifications",columns:new[]{"UserId","ReadAt","CreatedAt"});
    }
    protected override void Down(MigrationBuilder m){m.DropTable(name:"UserNotifications");m.DropColumn(name:"DeclinedAt",table:"CareHandoffs");m.DropColumn(name:"CancelledAt",table:"CareHandoffs");}
}
