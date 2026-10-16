using CareTogether.Infrastructure; using Microsoft.EntityFrameworkCore;
namespace CareTogether.Worker;
public sealed class Worker(ILogger<Worker> logger, IDbContextFactory<CareDbContext> factory) : BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken) { while(!stoppingToken.IsCancellationRequested) { try { await using var db=await factory.CreateDbContextAsync(stoppingToken); var upcoming=await db.Appointments.CountAsync(x=>x.StartsAt>DateTimeOffset.UtcNow&&x.StartsAt<DateTimeOffset.UtcNow.AddHours(24),stoppingToken); var overdue=await db.CareTasks.CountAsync(x=>!x.IsCompleted&&x.DueAt<DateTimeOffset.UtcNow,stoppingToken); logger.LogInformation("Reminder scan complete: {Upcoming} appointments, {Overdue} overdue tasks",upcoming,overdue); await Task.Delay(TimeSpan.FromMinutes(5),stoppingToken); } catch(Exception ex) when(!stoppingToken.IsCancellationRequested) { logger.LogWarning(ex,"Database is not ready; reminder scan will retry"); await Task.Delay(TimeSpan.FromSeconds(10),stoppingToken); } } }
}
