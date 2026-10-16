using System.Net.Http.Json;
using System.Text;
using CareTogether.Application;
using CareTogether.Domain;
using CareTogether.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CareTogether.Api.Controllers;

[ApiController, Authorize, Route("api/care-circles/{circleId:guid}/ai-summary")]
public sealed class CareSummaryController(CareDbContext db,ICurrentUser current,IHttpClientFactory clients,IConfiguration configuration,ILogger<CareSummaryController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid circleId,[FromQuery] DateTimeOffset? startDate=null)
    {
        if(!await db.CareTeamMembers.AnyAsync(x=>x.CareCircleId==circleId&&x.UserId==current.UserId&&x.IsActive))return Forbid();
        var circle=await db.CareCircles.SingleOrDefaultAsync(x=>x.Id==circleId);if(circle is null)return NotFound();
        var dayStart=startDate??new DateTimeOffset(DateTime.Today);var dayEnd=dayStart.AddDays(1);var now=DateTimeOffset.UtcNow.ToOffset(dayStart.Offset);

        var tasks=await db.CareTasks.Where(x=>x.CareCircleId==circleId&&x.Status!=CareTaskStatus.Cancelled&&((x.DueAt>=dayStart&&x.DueAt<dayEnd)||(x.Status!=CareTaskStatus.Done&&x.DueAt<now))).OrderBy(x=>x.DueAt).Select(x=>new{x.Title,x.Details,x.DueAt,x.Status,Assignee=x.AssignedToUserId==null?"Unassigned":db.Users.Where(u=>u.Id==x.AssignedToUserId).Select(u=>u.DisplayName).FirstOrDefault()}).ToListAsync();
        var appointments=await db.Appointments.Where(x=>x.CareCircleId==circleId&&x.Status!=AppointmentStatus.Cancelled&&((x.StartsAt>=dayStart&&x.StartsAt<dayEnd)||(x.Status==AppointmentStatus.Scheduled&&x.StartsAt<now))).OrderBy(x=>x.StartsAt).Select(x=>new{x.Title,x.Provider,x.Location,x.Notes,x.StartsAt,x.Status}).ToListAsync();
        var medicationRows=await db.MedicationOccurrences.Where(x=>db.Medications.Any(m=>m.Id==x.MedicationId&&m.CareCircleId==circleId&&m.Status==MedicationStatus.Active)&&((x.ScheduledAt>=dayStart&&x.ScheduledAt<dayEnd)||(x.ScheduledAt<now&&!db.MedicationLogs.Any(l=>l.MedicationId==x.MedicationId&&l.ScheduledAt==x.ScheduledAt&&l.TakenAt!=null)))).OrderBy(x=>x.ScheduledAt).Select(x=>new{Name=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.Name).First(),Dosage=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.Dosage).First(),Instructions=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.Instructions).FirstOrDefault(),x.ScheduledAt,IsTaken=db.MedicationLogs.Any(l=>l.MedicationId==x.MedicationId&&l.ScheduledAt==x.ScheduledAt&&l.TakenAt!=null)}).ToListAsync();
        var journal=await db.JournalEntries.Where(x=>x.CareCircleId==circleId).OrderByDescending(x=>x.CreatedAt).Take(5).Select(x=>new{x.Body,x.Category,x.CreatedAt,Author=db.Users.Where(u=>u.Id==x.AuthorUserId).Select(u=>u.DisplayName).FirstOrDefault()}).ToListAsync();
        var readings=await db.HealthReadings.Where(x=>x.CareCircleId==circleId).OrderByDescending(x=>x.RecordedAt).Take(5).ToListAsync();
        var activeMedications=await db.Medications.Where(x=>x.CareCircleId==circleId&&x.Status==MedicationStatus.Active).OrderBy(x=>x.Name).Select(x=>new{x.Name,x.Dosage,x.Schedule}).ToListAsync();
        var nextAppointment=await db.Appointments.Where(x=>x.CareCircleId==circleId&&x.Status==AppointmentStatus.Scheduled&&x.StartsAt>=dayEnd).OrderBy(x=>x.StartsAt).Select(x=>new{x.Title,x.Provider,x.StartsAt,x.Location}).FirstOrDefaultAsync();
        var primaryContact=await db.EmergencyContacts.Where(x=>x.CareCircleId==circleId).OrderByDescending(x=>x.IsPrimary).ThenBy(x=>x.Name).Select(x=>new{x.Name,x.Relationship,x.Phone,x.IsPrimary}).FirstOrDefaultAsync();

        var todayTasks=tasks.Where(x=>x.DueAt>=dayStart&&x.DueAt<dayEnd).ToList();var todayAppointments=appointments.Where(x=>x.StartsAt>=dayStart&&x.StartsAt<dayEnd).ToList();var todayMedications=medicationRows.Where(x=>x.ScheduledAt>=dayStart&&x.ScheduledAt<dayEnd).ToList();
        var missedTasks=tasks.Where(x=>x.Status!=CareTaskStatus.Done&&x.DueAt<now).ToList();var missedAppointments=appointments.Where(x=>x.Status==AppointmentStatus.Scheduled&&x.StartsAt<now).ToList();var missedMedications=medicationRows.Where(x=>!x.IsTaken&&x.ScheduledAt<now).ToList();
        var context=new StringBuilder().AppendLine("AI-Powered Health & Wellness Summary").AppendLine($"{circle.RecipientName} — {dayStart:MMMM d, yyyy}").AppendLine().AppendLine("WHAT'S HAPPENING TODAY");
        Add(context,todayTasks.Select(x=>$"Task: {x.Title} at {x.DueAt.ToLocalTime():h:mm tt}; assigned to {x.Assignee}{Text(x.Details)}"));
        Add(context,todayAppointments.Select(x=>$"Appointment: {x.Title} with {x.Provider} at {x.StartsAt.ToLocalTime():h:mm tt}{Text(x.Location)}{Text(x.Notes)}"));
        Add(context,todayMedications.Select(x=>$"Medication: {x.Name} {x.Dosage} at {x.ScheduledAt.ToLocalTime():h:mm tt}; {(x.IsTaken?"administered":"due")}{Text(x.Instructions)}"));
        if(todayTasks.Count+todayAppointments.Count+todayMedications.Count==0)context.AppendLine("- No care items are scheduled today.");
        context.AppendLine().AppendLine("MISSED CARE ITEMS");
        Add(context,missedTasks.Select(x=>$"MISSED task: {x.Title}, due {x.DueAt.ToLocalTime():MMM d, h:mm tt}"));Add(context,missedAppointments.Select(x=>$"MISSED appointment: {x.Title}, scheduled {x.StartsAt.ToLocalTime():MMM d, h:mm tt}"));Add(context,missedMedications.Select(x=>$"MISSED medication: {x.Name} {x.Dosage}, scheduled {x.ScheduledAt.ToLocalTime():MMM d, h:mm tt}"));
        if(missedTasks.Count+missedAppointments.Count+missedMedications.Count==0)context.AppendLine("- No missed care items.");
        context.AppendLine().AppendLine("RECENT CARE JOURNAL");Add(context,journal.Select(x=>$"{x.CreatedAt.ToLocalTime():MMM d}: {x.Body}{(string.IsNullOrWhiteSpace(x.Category)?"":$" [{x.Category}]")} — {x.Author}"));if(journal.Count==0)context.AppendLine("- No journal entries recorded.");
        context.AppendLine().AppendLine("OTHER HELPFUL INFORMATION");if(!string.IsNullOrWhiteSpace(circle.Notes))context.AppendLine($"- Recipient information: {circle.Notes}");Add(context,activeMedications.Select(x=>$"Active medication: {x.Name} {x.Dosage}; {x.Schedule}"));if(nextAppointment is not null)context.AppendLine($"- Next appointment: {nextAppointment.Title} with {nextAppointment.Provider}, {nextAppointment.StartsAt.ToLocalTime():MMM d, h:mm tt}{Text(nextAppointment.Location)}");Add(context,readings.Select(x=>$"Health reading: {x.Type} {x.Value}{(x.SecondaryValue is null?"":$"/{x.SecondaryValue}")}{(x.Pulse is null?"":$", pulse {x.Pulse}")} on {x.RecordedAt.ToLocalTime():MMM d}"));if(primaryContact is not null)context.AppendLine($"- Emergency contact: {primaryContact.Name} ({primaryContact.Relationship}), {primaryContact.Phone}");

        var request=new AiSummaryRequest(circleId,context.ToString().Trim());AiSummaryResponse result;
        try
        {
            using var client=clients.CreateClient();client.BaseAddress=new Uri(configuration["Ai:BaseUrl"]??"http://ai:8080");var response=await client.PostAsJsonAsync("api/ai/care-summary",request);response.EnsureSuccessStatusCode();result=(await response.Content.ReadFromJsonAsync<AiSummaryResponse>())!;
        }
        catch(Exception ex)
        {
            logger.LogWarning(ex,"AI summary service unavailable for care circle {CareCircleId}; returning grounded fallback",circleId);result=new(context.ToString().Trim(),["Review missed items with the Care Circle","Confirm upcoming care assignments"],"AI service unavailable; this summary was assembled from CareTogether records and is not medical advice.");
        }
        return Ok(new{circleId,recipientName=circle.RecipientName,generatedAt=DateTimeOffset.UtcNow,result.Summary,result.SuggestedActions,result.Disclaimer});
    }

    static void Add(StringBuilder builder,IEnumerable<string> lines){foreach(var line in lines)builder.AppendLine($"- {line}");}
    static string Text(string? value)=>string.IsNullOrWhiteSpace(value)?"":$"; {value.Trim()}";
}
