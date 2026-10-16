using CareTogether.Application;
using CareTogether.Domain;
using CareTogether.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CareTogether.Api.Controllers;

[ApiController,Authorize,Route("api/chat")]
public sealed class ChatController(CareDbContext db,ICurrentUser current) : ControllerBase
{
    static readonly string[] Suggestions=["What are my tasks today?","What are my tasks for the next 7 days?","What's happening today?","Add a medication","Record a blood pressure reading"];

    [HttpPost("ask")]
    public async Task<IActionResult> Ask(ChatQuestionRequest request)
    {
        if(string.IsNullOrWhiteSpace(request.Question))return BadRequest("Enter a question.");
        var accessibleIds=await db.CareTeamMembers.Where(x=>x.UserId==current.UserId&&x.IsActive).Select(x=>x.CareCircleId).Distinct().ToListAsync();
        if(accessibleIds.Count==0)return Ok(Result("No care recipients are available for this account.","no-recipients",[]));
        var original=request.Question.Trim();var question=original.ToLowerInvariant();var now=(request.ClientNow??DateTimeOffset.UtcNow);var dayStart=new DateTimeOffset(now.Date,now.Offset);var dayEnd=dayStart.AddDays(1);
        if(IsMyTasksQuestion(question))
        {
            var (rangeEnd,rangeLabel)=TaskRange(question,dayStart);var tasks=await db.CareTasks.Where(x=>accessibleIds.Contains(x.CareCircleId)&&x.AssignedToUserId==current.UserId&&x.Status!=CareTaskStatus.Done&&x.Status!=CareTaskStatus.Cancelled&&x.DueAt>=dayStart&&x.DueAt<rangeEnd).OrderBy(x=>x.DueAt).Select(x=>new{x.Id,x.Title,x.DueAt,x.Status,Recipient=db.CareCircles.Where(c=>c.Id==x.CareCircleId).Select(c=>c.RecipientName).FirstOrDefault()}).ToListAsync();
            if(tasks.Count==0)return Ok(Result($"You have no open tasks assigned to you {rangeLabel}.","my-tasks",["Tasks"]));var lines=tasks.Take(20).Select(x=>$"{x.Title} for {x.Recipient}, due {x.DueAt.ToOffset(now.Offset):MMM d 'at' h:mm tt} ({StatusLabel(x.Status)})");var extra=tasks.Count>20?$" I found {tasks.Count-20} additional task(s); narrow the date range to see them.":"";return Ok(Result($"You have {tasks.Count} open task(s) assigned to you {rangeLabel}: {string.Join("; ",lines)}.{extra}","my-tasks",tasks.Take(20).Select(x=>$"Task {x.Id}").ToArray()));
        }
        var circleId=request.CareCircleId??(accessibleIds.Count==1?accessibleIds[0]:Guid.Empty);
        if(circleId==Guid.Empty)return Ok(Result("Select a care recipient so I can answer from the correct care record.","select-recipient",[]));
        if(!accessibleIds.Contains(circleId))return Forbid();
        var recipient=await db.CareCircles.Where(x=>x.Id==circleId).Select(x=>x.RecipientName).SingleAsync();

        if(IsCreate(question)&&(question.Contains("medication")||question.Contains("medicine")))
        {
            var name=MedicationName(original);var dosage=MedicationDosage(original);
            if(string.IsNullOrWhiteSpace(name)||string.IsNullOrWhiteSpace(dosage))return Ok(Result("Please include the medication name and dosage. Example: Add medication Metoprolol dosage: 25 mg daily at 8 AM start: Oct 13, 2026 end: Nov 13, 2026.","create-medication-missing-details",[]));
            var scheduleType=question.Contains("monthly")?MedicationScheduleType.Monthly:question.Contains("weekly")?MedicationScheduleType.Weekly:question.Contains("once")?MedicationScheduleType.Once:question.Contains("daily")?MedicationScheduleType.Daily:(MedicationScheduleType?)null;
            if(scheduleType is null)return Ok(Result("Please specify once, daily, weekly, or monthly.","create-medication-missing-schedule",[]));
            DateTimeOffset scheduledAt=now;DateOnly? start=null,end=null;DayOfWeek? weeklyDay=null;int? monthlyDay=null;string? times=null;int? occurrences=null;
            if(scheduleType==MedicationScheduleType.Once){if(!TrySchedule(original,now,out scheduledAt))return Ok(Result("For a one-time medication, include its date and time.","create-medication-missing-schedule",[]));}
            else
            {
                if(!TryTaggedDate(original,"start",out var startValue)||!TryTaggedDate(original,"end",out var endValue))return Ok(Result("For a repeating medication, include start and end dates.","create-medication-missing-dates",[]));start=DateOnly.FromDateTime(startValue);end=DateOnly.FromDateTime(endValue);
                var parsedTimes=ExtractTimes(original);if(parsedTimes.Count==0)return Ok(Result("Include at least one time of day, such as “at 8 AM” or “times: 8 AM, 8 PM.”","create-medication-missing-times",[]));times=string.Join(",",parsedTimes.Select(x=>x.ToString(@"hh\:mm")));occurrences=parsedTimes.Count;
                if(scheduleType==MedicationScheduleType.Weekly){weeklyDay=Enum.GetValues<DayOfWeek>().FirstOrDefault(x=>question.Contains(x.ToString().ToLowerInvariant()));if(!Enum.GetValues<DayOfWeek>().Any(x=>question.Contains(x.ToString().ToLowerInvariant())))return Ok(Result("For a weekly medication, include the day of the week.","create-medication-missing-day",[]));}
                if(scheduleType==MedicationScheduleType.Monthly){var day=Regex.Match(question,@"\bday\s+(?<day>\d{1,2})\b");if(!day.Success||!int.TryParse(day.Groups["day"].Value,out var number)||number is <1 or >31)return Ok(Result("For a monthly medication, include the day of month, such as “day 15.”","create-medication-missing-day",[]));monthlyDay=number;}
            }
            var instructions=ExtractDetails(original);var summary=scheduleType==MedicationScheduleType.Once?$"Once on {scheduledAt:MMM d, yyyy 'at' h:mm tt}":$"{scheduleType} • {occurrences} time(s) • {start:MMM d, yyyy} to {end:MMM d, yyyy}";var confirmation=$"Recipient: {recipient}\nMedication: {name}\nDosage: {dosage}\nSchedule: {summary}{(string.IsNullOrWhiteSpace(instructions)?"":$"\nInstructions: {instructions}")}";var action=new ChatPendingAction("medication",circleId,recipient,name,scheduledAt,null,null,instructions,false,confirmation,dosage,scheduleType,occurrences,times,start,end,weeklyDay,monthlyDay);return Ok(Result("I prepared this medication. Review the schedule before creating it.","create-medication",[],action));
        }
        if(IsCreate(question)&&question.Contains("journal"))
        {
            var body=ExtractTaggedValue(original,"entry")??TextAfterKeyword(original,"journal");if(string.IsNullOrWhiteSpace(body))return Ok(Result("What should the care-journal entry say? Example: Add journal entry: Ate well and walked for 20 minutes. category: General.","create-journal-missing-text",[]));var category=ExtractTaggedValue(original,"category");body=RemoveTaggedSuffix(body,"category");var confirmation=$"Recipient: {recipient}\nJournal entry: {body}{(string.IsNullOrWhiteSpace(category)?"":$"\nCategory: {category}")}";var action=new ChatPendingAction("journal",circleId,recipient,"Care journal entry",now,null,null,body,false,confirmation,Category:category);return Ok(Result("I prepared this care-journal entry. Review it before saving.","create-journal",[],action));
        }
        if((question.Contains("record")||IsCreate(question))&&(question.Contains("blood pressure")||question.Contains("weight")||question.Contains("glucose")||question.Contains("temperature")||question.Contains("health reading")))
        {
            var readingType=question.Contains("blood pressure")?"Blood Pressure":question.Contains("weight")?"Weight":question.Contains("glucose")?"Glucose":question.Contains("temperature")?"Temperature":"Health Reading";var numbers=Regex.Matches(original,@"(?<![\w])\d+(?:\.\d+)?").Select(x=>decimal.Parse(x.Value,CultureInfo.InvariantCulture)).ToList();if(numbers.Count==0)return Ok(Result("Include the reading value. For blood pressure, use a format such as 128/76 and optionally pulse: 72.","create-health-missing-value",[]));var value=numbers[0];decimal? secondary=readingType=="Blood Pressure"&&numbers.Count>1?numbers[1]:null;var pulseMatch=Regex.Match(original,@"\bpulse\s*:?(?<pulse>\d{1,3})",RegexOptions.IgnoreCase);int? pulse=pulseMatch.Success?int.Parse(pulseMatch.Groups["pulse"].Value):null;var confirmation=$"Recipient: {recipient}\nReading: {readingType}\nValue: {value}{(secondary is null?"":$"/{secondary}")}{(pulse is null?"":$"\nPulse: {pulse}")}\nRecorded: {now:MMM d, yyyy 'at' h:mm tt}";var action=new ChatPendingAction("health",circleId,recipient,readingType,now,null,null,null,false,confirmation,ReadingType:readingType,Value:value,SecondaryValue:secondary,Pulse:pulse);return Ok(Result("I prepared this health reading. Review it before saving.","create-health",[],action));
        }
        if((question.Contains("start")||IsCreate(question))&&(question.Contains("conversation")||question.Contains("conversion")))
        {
            var title=ExtractTaggedValue(original,"title")??TextAfterKeyword(original,question.Contains("conversation")?"conversation":"conversion");title=Regex.Replace(title??"",@"^(?:about|called|titled)\s+","",RegexOptions.IgnoreCase).Trim(' ','.',',');if(string.IsNullOrWhiteSpace(title))return Ok(Result("What should the conversation be called? Example: Start a conversation about medication changes.","create-conversation-missing-title",[]));var confirmation=$"Recipient: {recipient}\nConversation: {title}";var action=new ChatPendingAction("conversation",circleId,recipient,title,now,null,null,null,false,confirmation);return Ok(Result("I prepared this conversation. Review it before creating it.","create-conversation",[],action));
        }

        if((question.Contains("create")||question.Contains("add"))&&question.Contains("task"))
        {
            if(!TrySchedule(original,now,out var scheduledAt))return Ok(Result("Please include a date and time. For example: Create a task to check blood pressure tomorrow at 9 AM.","create-task-missing-schedule",[]));
            var title=TaskTitle(original,recipient);if(string.IsNullOrWhiteSpace(title))return Ok(Result("What should the task be called? Include it after the word task.","create-task-missing-title",[]));var assignToMe=question.Contains("assign to me")||question.Contains("assigned to me");var details=ExtractDetails(original);var confirmation=$"Recipient: {recipient}\nTask: {title}\nDue: {scheduledAt:MMM d, yyyy 'at' h:mm tt}\nAssigned to: {(assignToMe?"You":"Unassigned")}{(string.IsNullOrWhiteSpace(details)?"":$"\nInstructions: {details}")}";var action=new ChatPendingAction("task",circleId,recipient,title,scheduledAt,null,null,details,assignToMe,confirmation);return Ok(Result("I prepared this task. Review the details before creating it.","create-task",[],action));
        }
        if((question.Contains("schedule")||question.Contains("create")||question.Contains("add"))&&question.Contains("appointment"))
        {
            if(!TrySchedule(original,now,out var scheduledAt))return Ok(Result("Please include the appointment date and time. For example: Schedule an appointment with Dr. Reyes Friday at 2 PM.","create-appointment-missing-schedule",[]));var provider=Provider(original);if(string.IsNullOrWhiteSpace(provider))return Ok(Result("Who is the appointment with? Include the provider after the word “with.”","create-appointment-missing-provider",[]));var location=ExtractTaggedValue(original,"location");var details=ExtractDetails(original);var title=$"Appointment with {provider}";var confirmation=$"Recipient: {recipient}\nAppointment: {title}\nProvider: {provider}\nDate: {scheduledAt:MMM d, yyyy 'at' h:mm tt}{(string.IsNullOrWhiteSpace(location)?"":$"\nLocation: {location}")}{(string.IsNullOrWhiteSpace(details)?"":$"\nNotes: {details}")}";var action=new ChatPendingAction("appointment",circleId,recipient,title,scheduledAt,provider,location,details,false,confirmation);return Ok(Result("I prepared this appointment. Review the details before scheduling it.","create-appointment",[],action));
        }

        if(question.Contains("blood pressure")||question.Contains(" bp")||question.StartsWith("bp"))
        {
            var reading=await db.HealthReadings.Where(x=>x.CareCircleId==circleId&&x.Type.Contains("Blood")).OrderByDescending(x=>x.RecordedAt).FirstOrDefaultAsync();
            if(reading is null)return Ok(Result($"No blood-pressure reading has been recorded for {recipient}.","latest-blood-pressure",["Health readings"]));
            var value=$"{reading.Value:0.##}{(reading.SecondaryValue is null?"":$"/{reading.SecondaryValue:0.##}")}";var pulse=reading.Pulse is null?"":$" with a pulse of {reading.Pulse}";return Ok(Result($"{recipient}'s latest blood-pressure reading was {value}{pulse}, recorded {reading.RecordedAt.ToOffset(now.Offset):MMMM d, yyyy 'at' h:mm tt}.","latest-blood-pressure",[$"Health reading {reading.Id}"]));
        }
        if(question.Contains("missed")||question.Contains("overdue"))
        {
            var tasks=await db.CareTasks.Where(x=>x.CareCircleId==circleId&&x.Status!=CareTaskStatus.Done&&x.Status!=CareTaskStatus.Cancelled&&x.DueAt<now).OrderBy(x=>x.DueAt).Select(x=>new{x.Title,x.DueAt}).ToListAsync();
            var appointments=await db.Appointments.Where(x=>x.CareCircleId==circleId&&x.Status==AppointmentStatus.Scheduled&&x.StartsAt<now).OrderBy(x=>x.StartsAt).Select(x=>new{x.Title,x.StartsAt}).ToListAsync();
            var medications=await db.MedicationOccurrences.Where(x=>x.ScheduledAt<now&&db.Medications.Any(m=>m.Id==x.MedicationId&&m.CareCircleId==circleId&&m.Status==MedicationStatus.Active)&&!db.MedicationLogs.Any(l=>l.MedicationId==x.MedicationId&&l.ScheduledAt==x.ScheduledAt&&l.TakenAt!=null)).OrderBy(x=>x.ScheduledAt).Select(x=>new{Name=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.Name).First(),x.ScheduledAt}).ToListAsync();
            if(tasks.Count+appointments.Count+medications.Count==0)return Ok(Result($"There are no missed care items recorded for {recipient}.","missed-care",["Tasks","Appointments","Medication cards"]));
            var details=tasks.Select(x=>$"task {x.Title} ({x.DueAt.ToOffset(now.Offset):MMM d, h:mm tt})").Concat(appointments.Select(x=>$"appointment {x.Title} ({x.StartsAt.ToOffset(now.Offset):MMM d, h:mm tt})")).Concat(medications.Select(x=>$"medication {x.Name} ({x.ScheduledAt.ToOffset(now.Offset):MMM d, h:mm tt})"));return Ok(Result($"{recipient} has {tasks.Count} missed task(s), {appointments.Count} missed appointment(s), and {medications.Count} missed medication card(s): {string.Join("; ",details.Take(10))}.","missed-care",["Tasks","Appointments","Medication cards"]));
        }
        if(question.Contains("next appointment")||question.Contains("upcoming appointment"))
        {
            var item=await db.Appointments.Where(x=>x.CareCircleId==circleId&&x.Status==AppointmentStatus.Scheduled&&x.StartsAt>=now).OrderBy(x=>x.StartsAt).FirstOrDefaultAsync();if(item is null)return Ok(Result($"No upcoming appointment is recorded for {recipient}.","next-appointment",["Appointments"]));return Ok(Result($"{recipient}'s next appointment is {item.Title} with {item.Provider} on {item.StartsAt.ToOffset(now.Offset):MMMM d, yyyy 'at' h:mm tt}{(string.IsNullOrWhiteSpace(item.Location)?"":$" at {item.Location}")}.","next-appointment",[$"Appointment {item.Id}"]));
        }
        if(question.Contains("journal")||question.Contains("note")||question.Contains("update"))
        {
            var entries=await db.JournalEntries.Where(x=>x.CareCircleId==circleId).OrderByDescending(x=>x.CreatedAt).Take(5).Select(x=>new{x.Id,x.Body,x.Category,x.CreatedAt,Author=db.Users.Where(u=>u.Id==x.AuthorUserId).Select(u=>u.DisplayName).FirstOrDefault()}).ToListAsync();if(entries.Count==0)return Ok(Result($"No care-journal entries have been recorded for {recipient}.","care-journal",["Care Journal"]));return Ok(Result($"Recent journal updates for {recipient}: {string.Join(" ",entries.Select(x=>$"{x.CreatedAt.ToOffset(now.Offset):MMM d}: {x.Body} — {x.Author}"))}","care-journal",entries.Select(x=>$"Journal entry {x.Id}").ToArray()));
        }
        if(question.Contains("medication")||question.Contains("medicine")||question.Contains("dose"))
        {
            var occurrences=await db.MedicationOccurrences.Where(x=>x.ScheduledAt>=dayStart&&x.ScheduledAt<dayEnd&&db.Medications.Any(m=>m.Id==x.MedicationId&&m.CareCircleId==circleId&&m.Status==MedicationStatus.Active)).OrderBy(x=>x.ScheduledAt).Select(x=>new{x.Id,Name=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.Name).First(),Dosage=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.Dosage).First(),x.ScheduledAt,Taken=db.MedicationLogs.Any(l=>l.MedicationId==x.MedicationId&&l.ScheduledAt==x.ScheduledAt&&l.TakenAt!=null)}).ToListAsync();if(occurrences.Count==0)return Ok(Result($"No medication cards are scheduled today for {recipient}.","today-medications",["Medication cards"]));return Ok(Result($"Today's medications for {recipient}: {string.Join("; ",occurrences.Select(x=>$"{x.Name} {x.Dosage} at {x.ScheduledAt.ToOffset(now.Offset):h:mm tt} ({(x.Taken?"administered":"not administered")})"))}.","today-medications",occurrences.Select(x=>$"Medication card {x.Id}").ToArray()));
        }
        if(question.Contains("today")||question.Contains("happening")||question.Contains("schedule"))
        {
            var tasks=await db.CareTasks.Where(x=>x.CareCircleId==circleId&&x.Status!=CareTaskStatus.Cancelled&&x.DueAt>=dayStart&&x.DueAt<dayEnd).OrderBy(x=>x.DueAt).Select(x=>new{x.Title,x.DueAt,x.Status}).ToListAsync();var appointments=await db.Appointments.Where(x=>x.CareCircleId==circleId&&x.Status!=AppointmentStatus.Cancelled&&x.StartsAt>=dayStart&&x.StartsAt<dayEnd).OrderBy(x=>x.StartsAt).Select(x=>new{x.Title,x.Provider,x.StartsAt}).ToListAsync();var parts=tasks.Select(x=>$"task {x.Title} at {x.DueAt.ToOffset(now.Offset):h:mm tt}").Concat(appointments.Select(x=>$"appointment {x.Title} with {x.Provider} at {x.StartsAt.ToOffset(now.Offset):h:mm tt}")).ToList();if(parts.Count==0)return Ok(Result($"No tasks or appointments are scheduled today for {recipient}. Ask about medications for today's dose cards.","today-care",["Tasks","Appointments"]));return Ok(Result($"Today for {recipient}: {string.Join("; ",parts)}. You can also ask about today's medications.","today-care",["Tasks","Appointments"]));
        }
        return Ok(Result($"I can answer questions about {recipient}'s latest blood pressure, today's care and medications, missed items, next appointment, and recent care-journal entries. Try one of the suggested questions below.","help",[]));
    }

    static ChatAnswerResponse Result(string answer,string intent,IReadOnlyList<string> sources,ChatPendingAction? action=null)=>new(answer,intent,DateTimeOffset.UtcNow,sources,Suggestions,action);
    static bool IsMyTasksQuestion(string text)=>(text.Contains("my task")||text.Contains("tasks assigned to me")||text.Contains("assigned tasks"))&&!IsCreate(text);
    static (DateTimeOffset End,string Label) TaskRange(string text,DateTimeOffset start)
    {
        if(text.Contains("today"))return(start.AddDays(1),"today");var match=Regex.Match(text,@"\bnext\s+(?<count>\d+|a|one)?\s*(?<unit>day|days|week|weeks|month|months)\b",RegexOptions.IgnoreCase);if(!match.Success)return(start.AddDays(1),"today");var count=match.Groups["count"].Value is "" or "a" or "one"?1:Math.Clamp(int.Parse(match.Groups["count"].Value),1,36);var unit=match.Groups["unit"].Value.ToLowerInvariant();var end=unit.StartsWith("month")?start.AddMonths(count):unit.StartsWith("week")?start.AddDays(count*7):start.AddDays(count);var label=count==1?$"for the next {unit.TrimEnd('s')}":$"for the next {count} {unit}";return(end,label);
    }
    static string StatusLabel(CareTaskStatus status)=>status switch{CareTaskStatus.Assigned=>"assigned",CareTaskStatus.InProgress=>"in progress",_=>"open"};
    static bool IsCreate(string text)=>text.Contains("create")||text.Contains("add")||text.Contains("new ");
    static string MedicationName(string text){var match=Regex.Match(text,@"\b(?:medication|medicine)\s+(?<value>.+?)(?=\s+(?:dosage|dose)\s*:|\s+(?:once|daily|weekly|monthly)\b|$)",RegexOptions.IgnoreCase);return match.Success?match.Groups["value"].Value.Trim(' ','.',','):"";}
    static string? MedicationDosage(string text){var match=Regex.Match(text,@"\b(?:dosage|dose)\s*:\s*(?<value>.+?)(?=\s+(?:once|daily|weekly|monthly)\b|\s+\b(?:start|end|instructions?|notes?)\s*:|$)",RegexOptions.IgnoreCase);return match.Success?match.Groups["value"].Value.Trim():null;}
    static bool TryTaggedDate(string text,string tag,out DateTime value){var match=Regex.Match(text,$@"\b{tag}\s*:\s*(?<date>.+?)(?=\s+\b(?:start|end|instructions?|notes?)\s*:|$)",RegexOptions.IgnoreCase);return DateTime.TryParse(match.Success?match.Groups["date"].Value:null,CultureInfo.CurrentCulture,DateTimeStyles.AllowWhiteSpaces,out value);}
    static List<TimeSpan> ExtractTimes(string text){var matches=Regex.Matches(text,@"(?<![/\d])(?<hour>\d{1,2})(?::(?<minute>\d{2}))?\s*(?<ampm>a\.?m\.?|p\.?m\.?)\b",RegexOptions.IgnoreCase);var values=new List<TimeSpan>();foreach(Match match in matches){var hour=int.Parse(match.Groups["hour"].Value);var minute=match.Groups["minute"].Success?int.Parse(match.Groups["minute"].Value):0;if(hour is <1 or >12||minute>59)continue;var pm=match.Groups["ampm"].Value.StartsWith("p",StringComparison.OrdinalIgnoreCase);if(hour==12)hour=0;if(pm)hour+=12;var value=new TimeSpan(hour,minute,0);if(!values.Contains(value))values.Add(value);}return values;}
    static string? TextAfterKeyword(string text,string keyword){var index=text.IndexOf(keyword,StringComparison.OrdinalIgnoreCase);if(index<0)return null;return text[(index+keyword.Length)..].Trim(' ',':','-','.');}
    static string RemoveTaggedSuffix(string text,string tag){var match=Regex.Match(text,$@"\s+\b{tag}\s*:",RegexOptions.IgnoreCase);return (match.Success?text[..match.Index]:text).Trim();}
    static bool TrySchedule(string text,DateTimeOffset now,out DateTimeOffset value)
    {
        var date=now.Date;var foundDate=false;var lower=text.ToLowerInvariant();if(lower.Contains("tomorrow")){date=date.AddDays(1);foundDate=true;}else if(lower.Contains("today")){foundDate=true;}else
        {
            foreach(var day in Enum.GetValues<DayOfWeek>()){if(!Regex.IsMatch(lower,$@"\b{day.ToString().ToLowerInvariant()}\b"))continue;var delta=((int)day-(int)now.DayOfWeek+7)%7;date=date.AddDays(delta);foundDate=true;break;}
            if(!foundDate){var dateMatch=Regex.Match(text,@"\b(?:on\s+)?(?<date>\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?|(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\s+\d{1,2}(?:,\s*\d{4})?)\b",RegexOptions.IgnoreCase);if(dateMatch.Success&&DateTime.TryParse(dateMatch.Groups["date"].Value,CultureInfo.CurrentCulture,DateTimeStyles.AllowWhiteSpaces,out var parsed)){date=parsed.Date;foundDate=true;}}
        }
        var timeMatch=Regex.Match(text,@"(?:\bat\s+|@\s*)(?<hour>\d{1,2})(?::(?<minute>\d{2}))?\s*(?<ampm>a\.?m\.?|p\.?m\.?)\b",RegexOptions.IgnoreCase);if(!foundDate||!timeMatch.Success){value=default;return false;}var hour=int.Parse(timeMatch.Groups["hour"].Value);var minute=timeMatch.Groups["minute"].Success?int.Parse(timeMatch.Groups["minute"].Value):0;var pm=timeMatch.Groups["ampm"].Value.StartsWith("p",StringComparison.OrdinalIgnoreCase);if(hour is <1 or >12||minute>59){value=default;return false;}if(hour==12)hour=0;if(pm)hour+=12;value=new DateTimeOffset(date.AddHours(hour).AddMinutes(minute),now.Offset);if(value<=now&&Regex.IsMatch(lower,$@"\b{now.DayOfWeek.ToString().ToLowerInvariant()}\b"))value=value.AddDays(7);return true;
    }
    static string TaskTitle(string text,string recipient)
    {
        var index=text.IndexOf("task",StringComparison.OrdinalIgnoreCase);if(index<0)return "";var value=text[(index+4)..].Trim(' ',':','-');value=Regex.Replace(value,$@"^for\s+{Regex.Escape(recipient)}\s+to\s+","",RegexOptions.IgnoreCase);value=Regex.Replace(value,@"^to\s+","",RegexOptions.IgnoreCase);value=CutAtScheduleMarker(value);value=Regex.Replace(value,@"\s+assign(?:ed)?\s+to\s+me.*$","",RegexOptions.IgnoreCase);return value.Trim(' ','.',',');
    }
    static string Provider(string text){var match=Regex.Match(text,@"\bwith\s+(?<value>.+)$",RegexOptions.IgnoreCase);return match.Success?CutAtScheduleMarker(match.Groups["value"].Value).Trim(' ','.',','):"";}
    static string CutAtScheduleMarker(string value){var match=Regex.Match(value,@"\s+(?:today|tomorrow|on\s+|monday\b|tuesday\b|wednesday\b|thursday\b|friday\b|saturday\b|sunday\b|at\s+\d|@\s*\d|assign(?:ed)?\s+to|location\s*:|notes?\s*:)",RegexOptions.IgnoreCase);return match.Success?value[..match.Index]:value;}
    static string? ExtractDetails(string text)=>ExtractTaggedValue(text,"notes")??ExtractTaggedValue(text,"instructions");
    static string? ExtractTaggedValue(string text,string tag){var match=Regex.Match(text,$@"\b{tag}\s*:\s*(?<value>.+?)(?=\s+\b(?:location|notes?|instructions)\s*:|$)",RegexOptions.IgnoreCase);return match.Success?match.Groups["value"].Value.Trim():null;}
}
