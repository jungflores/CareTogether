using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610150001_AddGroceryItems")]
public sealed class AddGroceryItems : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name:"GroceryItems",columns:t=>new
        {
            Id=t.Column<Guid>(type:"uniqueidentifier",nullable:false),
            CreatedAt=t.Column<DateTimeOffset>(type:"datetimeoffset",nullable:false,defaultValueSql:"SYSUTCDATETIME()"),
            CareCircleId=t.Column<Guid>(type:"uniqueidentifier",nullable:false),
            CreatedByUserId=t.Column<Guid>(type:"uniqueidentifier",nullable:false),
            Name=t.Column<string>(type:"nvarchar(max)",nullable:false),
            Quantity=t.Column<string>(type:"nvarchar(max)",nullable:true),
            Notes=t.Column<string>(type:"nvarchar(max)",nullable:true),
            AssignedToUserId=t.Column<Guid>(type:"uniqueidentifier",nullable:true),
            AssignmentStatus=t.Column<int>(type:"int",nullable:false),
            IsPurchased=t.Column<bool>(type:"bit",nullable:false),
            PurchasedAt=t.Column<DateTimeOffset>(type:"datetimeoffset",nullable:true)
        },constraints:t=>t.PrimaryKey("PK_GroceryItems",x=>x.Id));
        m.CreateIndex(name:"IX_GroceryItems_CareCircleId_IsPurchased_CreatedAt",table:"GroceryItems",columns:new[]{"CareCircleId","IsPurchased","CreatedAt"});
    }
    protected override void Down(MigrationBuilder m)=>m.DropTable(name:"GroceryItems");
}
