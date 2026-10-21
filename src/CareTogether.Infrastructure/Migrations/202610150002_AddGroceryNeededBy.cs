using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace CareTogether.Infrastructure.Migrations;

[DbContext(typeof(CareDbContext))]
[Migration("202610150002_AddGroceryNeededBy")]
public sealed class AddGroceryNeededBy : Migration
{
    protected override void Up(MigrationBuilder m) => m.AddColumn<DateOnly>(name:"NeededBy",table:"GroceryItems",type:"date",nullable:false,defaultValueSql:"CONVERT(date, SYSUTCDATETIME())");
    protected override void Down(MigrationBuilder m) => m.DropColumn(name:"NeededBy",table:"GroceryItems");
}
