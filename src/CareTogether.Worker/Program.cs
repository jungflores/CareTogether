using CareTogether.Infrastructure; using CareTogether.Worker; using Microsoft.EntityFrameworkCore;
var builder=Host.CreateApplicationBuilder(args); builder.Services.AddDbContextFactory<CareDbContext>(o=>o.UseSqlServer(builder.Configuration.GetConnectionString("CareTogether"))); builder.Services.AddHostedService<Worker>(); await builder.Build().RunAsync();
