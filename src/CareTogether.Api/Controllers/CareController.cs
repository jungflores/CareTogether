using CareTogether.Application; using CareTogether.Domain; using CareTogether.Infrastructure; using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
namespace CareTogether.Api.Controllers;
[ApiController, Authorize, Route("api")]
public sealed class CareController(CareDbContext db, ICurrentUser current, CareTogether.Api.InvitationDeliveryService invitationDelivery, IConfiguration configuration) : ControllerBase
{
 private IQueryable<Guid> CircleIds => db.CareTeamMembers.Where(x=>x.UserId==current.UserId&&x.IsActive).Select(x=>x.CareCircleId);
 [HttpGet("care-circles")] public async Task<IActionResult> Circles() => Ok(await db.CareCircles.Where(x=>CircleIds.Contains(x.Id)).OrderBy(x=>x.RecipientName).ToListAsync());
 [HttpPost("care-circles")] public async Task<IActionResult> CreateCircle(CreateCareCircleRequest r)
 {
  var recipientName=r.RecipientName.Trim();
  if(string.IsNullOrWhiteSpace(recipientName)) return BadRequest("The care recipient's name is required.");
  var existing=await db.CareCircles.Where(x=>CircleIds.Contains(x.Id)&&x.RecipientName==recipientName).OrderBy(x=>x.CreatedAt).Select(x=>new{x.Id,x.Name,x.RecipientName,x.Notes,x.CreatedAt}).FirstOrDefaultAsync();
  if(existing is not null) return Ok(existing);
  var details=new List<string>();
  if(r.DateOfBirth is not null) details.Add($"Date of birth: {r.DateOfBirth:yyyy-MM-dd}");
  if(!string.IsNullOrWhiteSpace(r.Relationship)) details.Add($"Relationship: {r.Relationship.Trim()}");
  var circle=new CareCircle{Name=$"{recipientName}'s Care Circle",RecipientName=recipientName,Notes=details.Count==0?null:string.Join("; ",details)};
  db.CareCircles.Add(circle);
  db.CareTeamMembers.Add(new CareTeamMember{CareCircle=circle,UserId=current.UserId,Role=MemberRole.Admin});
  await db.SaveChangesAsync();
  return Created($"api/care-circles/{circle.Id}",new{circle.Id,circle.Name,circle.RecipientName,circle.Notes,circle.CreatedAt});
 }
 [HttpGet("dashboard")] public async Task<IActionResult> Dashboard([FromQuery] DateTimeOffset? startDate=null,[FromQuery] int days=1)
 {
  days=days is 1 or 7 or 15 or 30?days:1;var rangeStart=startDate??new DateTimeOffset(DateTime.Today);var rangeEnd=rangeStart.AddDays(days);var now=DateTimeOffset.UtcNow.ToOffset(rangeStart.Offset);var ids=CircleIds;var circles=await db.CareCircles.Where(x=>ids.Contains(x.Id)).ToListAsync();
  var tasks=await db.CareTasks.Where(x=>ids.Contains(x.CareCircleId)&&x.Status!=CareTaskStatus.Cancelled&&((x.DueAt>=rangeStart&&x.DueAt<rangeEnd)||(x.Status!=CareTaskStatus.Done&&x.DueAt<now))).OrderBy(x=>x.DueAt).Take(100).Select(x=>new{x.Id,x.CareCircleId,RecipientName=db.CareCircles.Where(c=>c.Id==x.CareCircleId).Select(c=>c.RecipientName).FirstOrDefault(),x.Title,x.Details,HasRestorableDetails=x.PreviousDetails!=null,x.DueAt,x.Status,IsMissed=x.Status!=CareTaskStatus.Done&&x.DueAt<now,x.AssignedToUserId,AssigneeName=x.AssignedToUserId==null?null:db.Users.Where(u=>u.Id==x.AssignedToUserId).Select(u=>u.DisplayName).FirstOrDefault(),HasDocuments=db.CareDocuments.Any(d=>d.TaskId==x.Id)}).ToListAsync();
  var appointments=await db.Appointments.Where(x=>ids.Contains(x.CareCircleId)&&x.Status!=AppointmentStatus.Cancelled&&((x.StartsAt>=rangeStart&&x.StartsAt<rangeEnd)||(x.Status==AppointmentStatus.Scheduled&&x.StartsAt<now))).OrderBy(x=>x.StartsAt).Take(100).Select(x=>new{x.Id,x.CareCircleId,RecipientName=db.CareCircles.Where(c=>c.Id==x.CareCircleId).Select(c=>c.RecipientName).FirstOrDefault(),x.Title,x.Provider,x.StartsAt,x.Location,x.Notes,HasRestorableNotes=x.PreviousNotes!=null,x.Status,IsMissed=x.Status==AppointmentStatus.Scheduled&&x.StartsAt<now,HasDocuments=db.CareDocuments.Any(d=>d.AppointmentId==x.Id)}).ToListAsync();
  var medicationRows=await db.Medications.Where(x=>ids.Contains(x.CareCircleId)&&x.Status==MedicationStatus.Active).ToListAsync();var medicationIds=medicationRows.Select(x=>x.Id).ToHashSet();var occurrenceMedicationIds=(await db.MedicationOccurrences.Where(x=>medicationIds.Contains(x.MedicationId)).Select(x=>x.MedicationId).Distinct().ToListAsync()).ToHashSet();foreach(var medication in medicationRows.Where(x=>!occurrenceMedicationIds.Contains(x.Id)))db.MedicationOccurrences.AddRange(GenerateMedicationOccurrences(medication));if(db.ChangeTracker.HasChanges())await db.SaveChangesAsync();
  var medications=await db.MedicationOccurrences.Where(x=>((x.ScheduledAt>=rangeStart&&x.ScheduledAt<rangeEnd)||(x.ScheduledAt<now&&!db.MedicationLogs.Any(l=>l.MedicationId==x.MedicationId&&l.ScheduledAt==x.ScheduledAt&&l.TakenAt!=null)))&&db.Medications.Any(m=>m.Id==x.MedicationId&&m.Status==MedicationStatus.Active&&ids.Contains(m.CareCircleId))).OrderBy(x=>x.ScheduledAt).Select(x=>new{Id=x.MedicationId,OccurrenceId=x.Id,CareCircleId=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.CareCircleId).First(),RecipientName=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>db.CareCircles.Where(c=>c.Id==m.CareCircleId).Select(c=>c.RecipientName).FirstOrDefault()).FirstOrDefault(),Name=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.Name).First(),Dosage=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.Dosage).First(),Schedule=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.Schedule).First(),Instructions=db.Medications.Where(m=>m.Id==x.MedicationId).Select(m=>m.Instructions).FirstOrDefault(),ScheduledAt=x.ScheduledAt,Status=MedicationStatus.Active,IsTaken=db.MedicationLogs.Any(l=>l.MedicationId==x.MedicationId&&l.ScheduledAt==x.ScheduledAt&&l.TakenAt!=null),IsMissed=x.ScheduledAt<now&&!db.MedicationLogs.Any(l=>l.MedicationId==x.MedicationId&&l.ScheduledAt==x.ScheduledAt&&l.TakenAt!=null),HasDocuments=db.CareDocuments.Any(d=>d.MedicationId==x.MedicationId),HasLogs=db.MedicationLogs.Any(l=>l.MedicationId==x.MedicationId)}).ToListAsync();
  return Ok(new{circles,tasks,medications,appointments,rangeStart,rangeEnd,days,missed=new{tasks=tasks.Count(x=>x.IsMissed),appointments=appointments.Count(x=>x.IsMissed),medications=medications.Count(x=>x.IsMissed)}});
 }
 [HttpGet("care-circles/{id:guid}")] public async Task<IActionResult> Circle(Guid id)
 {
  if(!await IsMember(id)) return Forbid();
  var circle=await db.CareCircles.Where(x=>x.Id==id).Select(x=>new{x.Id,x.Name,x.RecipientName,x.Notes,x.CreatedAt}).SingleAsync();
  var memberRows=await db.CareTeamMembers.Where(x=>x.CareCircleId==id).OrderByDescending(x=>x.IsActive).ThenBy(x=>x.User.DisplayName).Select(x=>new{x.Id,x.UserId,x.User.DisplayName,x.User.Email,x.Role,x.IsActive,x.DeactivatedAt}).ToListAsync();
  var interactionUserIds=new HashSet<Guid>(await db.Messages.Where(x=>db.Conversations.Any(c=>c.Id==x.ConversationId&&c.CareCircleId==id)).Select(x=>x.SenderUserId).Distinct().ToListAsync());
  interactionUserIds.UnionWith(await db.CareTasks.Where(x=>x.CareCircleId==id&&x.AssignedToUserId!=null).Select(x=>x.AssignedToUserId!.Value).Distinct().ToListAsync());
  interactionUserIds.UnionWith(await db.CareHandoffs.Where(x=>x.CareCircleId==id).Select(x=>x.FromUserId).Distinct().ToListAsync());
  interactionUserIds.UnionWith(await db.CareHandoffs.Where(x=>x.CareCircleId==id).Select(x=>x.ToUserId).Distinct().ToListAsync());
  var canManage=memberRows.Any(x=>x.UserId==current.UserId&&x.IsActive&&x.Role==MemberRole.Admin);
  var members=memberRows.Select(x=>new{x.Id,x.UserId,x.DisplayName,x.Email,Role=x.Role.ToString(),x.IsActive,x.DeactivatedAt,IsCurrentUser=x.UserId==current.UserId,HasInteractions=interactionUserIds.Contains(x.UserId),CanRemove=canManage&&x.IsActive&&x.UserId!=current.UserId&&!interactionUserIds.Contains(x.UserId)});
  var invitationRows=await db.CareInvitations.Where(x=>x.CareCircleId==id&&x.Status==InvitationStatus.Pending&&x.ExpiresAt>DateTimeOffset.UtcNow).OrderBy(x=>x.Email).Select(x=>new{x.Id,x.Email,x.Role,x.ExpiresAt,x.InvitedByUserId}).ToListAsync();
  var invitations=invitationRows.Select(x=>new{x.Id,x.Email,Role=x.Role.ToString(),x.ExpiresAt,CanCancel=canManage||x.InvitedByUserId==current.UserId});
  return Ok(new{circle.Id,circle.Name,circle.RecipientName,circle.Notes,circle.CreatedAt,members,invitations});
 }
 [HttpPost("care-circles/{id:guid}/invitations")] public async Task<IActionResult> Invite(Guid id,CreateCareInvitationRequest r)
 {
  if(!await IsMember(id)) return Forbid();
  var email=r.Email.Trim().ToLowerInvariant();
  if(string.IsNullOrWhiteSpace(email)||!email.Contains('@')) return BadRequest("Enter a valid email address.");
  var channel=r.DeliveryChannel.Equals("Text",StringComparison.OrdinalIgnoreCase)?"Text":"Email";
  if(channel=="Text"&&string.IsNullOrWhiteSpace(r.Phone))return BadRequest("Enter a mobile phone number for a text invitation.");
  if(r.Role==MemberRole.CareRecipient) return BadRequest("Invitees must be caregivers, clinicians, or administrators.");
  var existing=await db.CareInvitations.Where(x=>x.CareCircleId==id&&x.Email==email&&x.Status==InvitationStatus.Pending).OrderByDescending(x=>x.CreatedAt).FirstOrDefaultAsync();
  if(existing is null)
  {
   existing=new CareInvitation{CareCircleId=id,Email=email,Role=r.Role,Message=r.Message?.Trim(),Token=Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),InvitedByUserId=current.UserId,ExpiresAt=DateTimeOffset.UtcNow.AddDays(7)};
   db.CareInvitations.Add(existing); await db.SaveChangesAsync();
  }
  var circle=await db.CareCircles.Where(x=>x.Id==id).Select(x=>x.RecipientName).SingleAsync();var sender=await db.Users.Where(x=>x.Id==current.UserId).Select(x=>x.DisplayName).SingleAsync();var baseUrl=(configuration["Invitations:PublicBaseUrl"]??$"{Request.Scheme}://{Request.Host}").TrimEnd('/');var inviteUrl=$"{baseUrl}/invite/{existing.Token}";var personal=string.IsNullOrWhiteSpace(r.Message)?"":$"\n\n{r.Message.Trim()}";var body=$"{sender} invited you to join {circle}'s CareTogether Care Circle as {r.Role}.{personal}\n\nAccept or decline: {inviteUrl}\n\nThis invitation expires {existing.ExpiresAt:g}.";var delivery=await invitationDelivery.SendAsync(channel,channel=="Text"?r.Phone!.Trim():email,$"CareTogether invitation for {circle}",body);
  return Ok(new{status="pending",deliveryStatus=delivery.Sent?"sent":"notConfigured",delivery.Message,channel,destination=channel=="Text"?r.Phone:email,inviteUrl,invitationId=existing.Id,existing.Email,role=existing.Role.ToString(),existing.ExpiresAt});
 }
 [HttpDelete("care-circles/{circleId:guid}/invitations/{invitationId:guid}")] public async Task<IActionResult> CancelInvitation(Guid circleId,Guid invitationId)
 {
  if(!await IsMember(circleId))return Forbid();
  var invitation=await db.CareInvitations.SingleOrDefaultAsync(x=>x.Id==invitationId&&x.CareCircleId==circleId);
  if(invitation is null)return NotFound();
  if(invitation.Status!=InvitationStatus.Pending)return Conflict("Only pending invitations can be cancelled.");
  if(invitation.InvitedByUserId!=current.UserId&&!await CanManage(circleId))return Forbid();
  invitation.Status=InvitationStatus.Revoked;await db.SaveChangesAsync();
  return Ok(new{action="cancelled",invitationId,message=$"The invitation for {invitation.Email} was cancelled."});
 }
 [HttpGet("care-circles/{circleId:guid}/members/{memberId:guid}/removal-status")] public async Task<IActionResult> RemovalStatus(Guid circleId,Guid memberId)
 {
  if(!await CanManage(circleId)) return Forbid();
  var member=await db.CareTeamMembers.SingleOrDefaultAsync(x=>x.Id==memberId&&x.CareCircleId==circleId);
  if(member is null) return NotFound();
  if(member.UserId==current.UserId) return BadRequest("You cannot remove or deactivate yourself.");
  var counts=await InteractionCounts(circleId,member.UserId);
  return Ok(new{canRemove=counts.Total==0,mustDeactivate=counts.Total>0,counts.Messages,counts.Tasks,counts.Handoffs,totalInteractions=counts.Total,member.IsActive});
 }
 [HttpDelete("care-circles/{circleId:guid}/members/{memberId:guid}")] public async Task<IActionResult> RemoveMember(Guid circleId,Guid memberId)
 {
  if(!await CanManage(circleId)) return Forbid();
  var member=await db.CareTeamMembers.Include(x=>x.User).SingleOrDefaultAsync(x=>x.Id==memberId&&x.CareCircleId==circleId);
  if(member is null) return NotFound();
  if(member.UserId==current.UserId) return BadRequest("You cannot remove or deactivate yourself.");
  var counts=await InteractionCounts(circleId,member.UserId);
  if(counts.Total==0) { db.CareTeamMembers.Remove(member); await db.SaveChangesAsync(); return Ok(new{action="removed",memberId,message=$"{member.User.DisplayName} was removed from the Care Circle."}); }
  member.IsActive=false; member.DeactivatedAt=DateTimeOffset.UtcNow; await db.SaveChangesAsync();
  return Ok(new{action="deactivated",memberId,message=$"{member.User.DisplayName} was deactivated because related care history must be retained.",totalInteractions=counts.Total});
 }
 [HttpGet("tasks")] public async Task<IActionResult> Tasks([FromQuery] Guid? careCircleId=null,[FromQuery] bool mine=false)
 {
  var query=db.CareTasks.Where(x=>CircleIds.Contains(x.CareCircleId));
  if(careCircleId is not null) query=query.Where(x=>x.CareCircleId==careCircleId);
  if(mine) query=query.Where(x=>x.AssignedToUserId==current.UserId);
  var rows=await query.Join(db.CareCircles,t=>t.CareCircleId,c=>c.Id,(t,c)=>new{t.Id,t.CareCircleId,t.AppointmentId,RecipientName=c.RecipientName,t.Title,t.Details,t.DueAt,t.AssignedToUserId,AssigneeName=t.AssignedToUserId==null?null:db.Users.Where(u=>u.Id==t.AssignedToUserId).Select(u=>u.DisplayName).FirstOrDefault(),t.Status,t.IsCompleted,t.CreatedAt}).OrderBy(x=>x.DueAt).ToListAsync();
  return Ok(rows.Select(x=>new{x.Id,x.CareCircleId,x.AppointmentId,x.RecipientName,x.Title,x.Details,x.DueAt,x.AssignedToUserId,x.AssigneeName,Status=x.Status.ToString(),x.IsCompleted,x.CreatedAt}));
 }
 [HttpGet("tasks/{id:guid}")] public async Task<IActionResult> TaskById(Guid id)
 {
  var item=await db.CareTasks.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));
  if(item is null)return NotFound();
  var recipient=await db.CareCircles.Where(x=>x.Id==item.CareCircleId).Select(x=>x.RecipientName).SingleAsync();
  var assignee=item.AssignedToUserId is null?null:await db.Users.Where(x=>x.Id==item.AssignedToUserId).Select(x=>x.DisplayName).SingleOrDefaultAsync();
  var linkedAppointmentStatus=item.AppointmentId is null?null:await db.Appointments.Where(x=>x.Id==item.AppointmentId).Select(x=>(AppointmentStatus?)x.Status).SingleOrDefaultAsync();
  return Ok(new{item.Id,item.CareCircleId,item.AppointmentId,RecipientName=recipient,item.Title,item.Details,item.PreviousDetails,item.DueAt,item.AssignedToUserId,AssigneeName=assignee,Status=item.Status.ToString(),LinkedAppointmentStatus=linkedAppointmentStatus==null?null:linkedAppointmentStatus.ToString(),item.CreatedAt});
 }
 [HttpPost("tasks")] public async Task<IActionResult> CreateTask(CreateTaskRequest r)
 {
  if(!await IsMember(r.CareCircleId))return Forbid();
  var assigneeId=r.AssignToMe?current.UserId:r.AssignedToUserId;
  if(!await IsValidAssignee(r.CareCircleId,assigneeId))return BadRequest("The assignee must be an active member of this care circle.");
  if(r.AppointmentId is not null)
  {
   if(!await db.Appointments.AnyAsync(x=>x.Id==r.AppointmentId&&x.CareCircleId==r.CareCircleId))return BadRequest("The linked appointment must belong to the same care circle.");
  }
  if(string.IsNullOrWhiteSpace(r.Title))return BadRequest("Task title is required.");
  var status=NormalizeStatus(r.Status,assigneeId);
  var item=new CareTask{CareCircleId=r.CareCircleId,AppointmentId=r.AppointmentId,Title=r.Title.Trim(),Details=r.Details?.Trim(),DueAt=r.DueAt,AssignedToUserId=assigneeId,Status=status,IsCompleted=status==CareTaskStatus.Done};
  db.Add(item); await db.SaveChangesAsync(); return Created($"api/tasks/{item.Id}",item);
 }
 [HttpPut("tasks/{id:guid}")] public async Task<IActionResult> UpdateTask(Guid id,UpdateTaskRequest r)
 {
  var item=await db.CareTasks.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();
  if(!await IsMember(r.CareCircleId))return Forbid();
  var circleChanged=item.CareCircleId!=r.CareCircleId;
  Appointment? linkedAppointment=null;
  if(circleChanged)
  {
   if(item.Status==CareTaskStatus.Done)return Conflict("A completed task cannot be moved to another care recipient.");
   if(item.AppointmentId is not null){linkedAppointment=await db.Appointments.SingleAsync(x=>x.Id==item.AppointmentId);if(linkedAppointment.Status==AppointmentStatus.Completed)return Conflict("The linked appointment is completed, so its care recipient cannot be changed.");}
  }
  var assigneeId=r.AssignToMe?current.UserId:r.AssignedToUserId;
  if(!await IsValidAssignee(r.CareCircleId,assigneeId))return BadRequest("The assignee must be an active member of this care circle.");
  if(string.IsNullOrWhiteSpace(r.Title))return BadRequest("Task title is required.");
  item.CareCircleId=r.CareCircleId;item.Title=r.Title.Trim();item.Details=r.Details?.Trim();item.PreviousDetails=r.PreviousDetails?.Trim();item.DueAt=r.DueAt;item.AssignedToUserId=assigneeId;item.Status=NormalizeStatus(r.Status,assigneeId);item.IsCompleted=item.Status==CareTaskStatus.Done;
  if(circleChanged&&linkedAppointment is not null)linkedAppointment.CareCircleId=r.CareCircleId;
  await db.SaveChangesAsync();return Ok(item);
 }
 [HttpPut("tasks/{id:guid}/status")] public async Task<IActionResult> UpdateTaskStatus(Guid id,UpdateTaskStatusRequest r)
 {
  var item=await db.CareTasks.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();
  item.Status=NormalizeStatus(r.Status,item.AssignedToUserId);item.IsCompleted=item.Status==CareTaskStatus.Done;await db.SaveChangesAsync();return Ok(item);
 }
 [HttpPut("tasks/{id:guid}/complete")] public async Task<IActionResult> CompleteTask(Guid id) { var item=await db.CareTasks.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId)); if(item is null)return NotFound(); item.Status=CareTaskStatus.Done;item.IsCompleted=true; await db.SaveChangesAsync(); return Ok(item); }
 [HttpPut("tasks/{id:guid}/toggle-instructions")] public async Task<IActionResult> ToggleTaskInstructions(Guid id)
 {
  var item=await db.CareTasks.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();
  if(!string.IsNullOrWhiteSpace(item.Details)){item.PreviousDetails=item.Details;item.Details=null;}else if(!string.IsNullOrWhiteSpace(item.PreviousDetails)){item.Details=item.PreviousDetails;item.PreviousDetails=null;}else return Conflict("This task has no instructions to clear or restore.");
  await db.SaveChangesAsync();return Ok(new{item.Id,item.Details,HasRestorableDetails=item.PreviousDetails!=null});
 }
 [HttpPut("tasks/{id:guid}/toggle-done")] public async Task<IActionResult> ToggleTaskDone(Guid id)
 {
  var item=await db.CareTasks.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();
  if(item.Status==CareTaskStatus.Cancelled)return Conflict("Cancelled tasks cannot be marked done.");
  if(item.Status==CareTaskStatus.Done){item.Status=item.PreviousStatus??(item.AssignedToUserId is null?CareTaskStatus.Open:CareTaskStatus.Assigned);item.PreviousStatus=null;item.IsCompleted=false;}
  else{item.PreviousStatus=item.Status;item.Status=CareTaskStatus.Done;item.IsCompleted=true;}
  await db.SaveChangesAsync();return Ok(new{item.Id,Status=item.Status.ToString(),item.IsCompleted});
 }
 [HttpDelete("tasks/{id:guid}")] public async Task<IActionResult> DeleteTask(Guid id)
 {
  var item=await db.CareTasks.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();
  if(item.Status!=CareTaskStatus.Open)return Conflict("Only open tasks can be deleted. Change or preserve all other tasks for care-history integrity.");
  db.CareTasks.Remove(item);await db.SaveChangesAsync();return Ok(new{deleted=true,id});
 }
 [HttpGet("medications")] public async Task<IActionResult> Medications([FromQuery] Guid? careCircleId=null) { var q=db.Medications.Where(x=>CircleIds.Contains(x.CareCircleId));if(careCircleId is not null)q=q.Where(x=>x.CareCircleId==careCircleId);return Ok(await q.OrderBy(x=>x.Name).Select(x=>new{x.Id,x.CareCircleId,RecipientName=db.CareCircles.Where(c=>c.Id==x.CareCircleId).Select(c=>c.RecipientName).FirstOrDefault(),x.Name,x.Dosage,x.Schedule,x.Instructions,x.Status,x.CreatedAt,HasLogs=db.MedicationLogs.Any(l=>l.MedicationId==x.Id),HasDocuments=db.CareDocuments.Any(d=>d.MedicationId==x.Id)}).ToListAsync()); }
 [HttpGet("medications/{id:guid}")] public async Task<IActionResult> MedicationById(Guid id) { var item=await db.Medications.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));return item is null?NotFound():Ok(item); }
 [HttpPost("medications")] public async Task<IActionResult> CreateMedication(CreateMedicationRequest r) { if(!await IsMember(r.CareCircleId))return Forbid();var error=MedicationScheduleError(r);if(error is not null)return BadRequest(error);if(string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Dosage))return BadRequest("Medication name and dosage are required.");var item=new Medication{CareCircleId=r.CareCircleId,Name=r.Name.Trim(),Dosage=r.Dosage.Trim(),Schedule=r.Schedule.Trim(),Instructions=r.Instructions?.Trim(),Status=r.Status};ApplySchedule(item,r);db.Add(item);await db.SaveChangesAsync();db.MedicationOccurrences.AddRange(GenerateMedicationOccurrences(item));await db.SaveChangesAsync();return Ok(item); }
 [HttpPut("medications/{id:guid}")] public async Task<IActionResult> UpdateMedication(Guid id,CreateMedicationRequest r) { var item=await db.Medications.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();if(!await IsMember(r.CareCircleId))return Forbid();var error=MedicationScheduleError(r);if(error is not null)return BadRequest(error);if(string.IsNullOrWhiteSpace(r.Name)||string.IsNullOrWhiteSpace(r.Dosage))return BadRequest("Medication name and dosage are required.");item.CareCircleId=r.CareCircleId;item.Name=r.Name.Trim();item.Dosage=r.Dosage.Trim();item.Schedule=r.Schedule.Trim();item.Instructions=r.Instructions?.Trim();item.Status=r.Status;ApplySchedule(item,r);await db.MedicationOccurrences.Where(x=>x.MedicationId==id).ExecuteDeleteAsync();await db.SaveChangesAsync();db.MedicationOccurrences.AddRange(GenerateMedicationOccurrences(item));await db.SaveChangesAsync();return Ok(item); }
 [HttpPut("medications/{id:guid}/toggle-completed")] public async Task<IActionResult> ToggleMedicationCompleted(Guid id) { var item=await db.Medications.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();if(item.Status==MedicationStatus.Completed){item.Status=item.PreviousStatus??MedicationStatus.Active;item.PreviousStatus=null;}else{item.PreviousStatus=item.Status;item.Status=MedicationStatus.Completed;}await db.SaveChangesAsync();return Ok(new{item.Id,Status=item.Status.ToString()}); }
 [HttpDelete("medications/{id:guid}")] public async Task<IActionResult> DeleteMedication(Guid id) { var item=await db.Medications.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();if(await db.MedicationLogs.AnyAsync(x=>x.MedicationId==id)||await db.CareDocuments.AnyAsync(x=>x.MedicationId==id))return Conflict("Medications with administration history or attached documents cannot be deleted. Mark the medication completed instead.");db.Medications.Remove(item);await db.SaveChangesAsync();return Ok(new{deleted=true,id}); }
 [HttpPost("medications/{id:guid}/logs")] public async Task<IActionResult> LogMedication(Guid id,MedicationLogRequest r) { var med=await db.Medications.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId)); if(med is null)return NotFound(); var log=new MedicationLog{MedicationId=id,ScheduledAt=r.ScheduledAt,TakenAt=r.Taken?DateTimeOffset.UtcNow:null,Skipped=!r.Taken,Note=r.Note}; db.Add(log); await db.SaveChangesAsync(); return Ok(log); }
 [HttpPut("medication-occurrences/{id:guid}/toggle-taken")] public async Task<IActionResult> ToggleMedicationOccurrence(Guid id) { var occurrence=await db.MedicationOccurrences.SingleOrDefaultAsync(x=>x.Id==id&&db.Medications.Any(m=>m.Id==x.MedicationId&&CircleIds.Contains(m.CareCircleId)));if(occurrence is null)return NotFound();var log=await db.MedicationLogs.FirstOrDefaultAsync(x=>x.MedicationId==occurrence.MedicationId&&x.ScheduledAt==occurrence.ScheduledAt&&x.TakenAt!=null);if(log is null)db.MedicationLogs.Add(new MedicationLog{MedicationId=occurrence.MedicationId,ScheduledAt=occurrence.ScheduledAt,TakenAt=DateTimeOffset.UtcNow});else db.MedicationLogs.Remove(log);await db.SaveChangesAsync();return Ok(new{occurrence.Id,IsTaken=log is null}); }
 [HttpGet("medications/{id:guid}/occurrences")] public async Task<IActionResult> MedicationOccurrences(Guid id) { if(!await db.Medications.AnyAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId)))return NotFound();return Ok(await db.MedicationOccurrences.Where(x=>x.MedicationId==id).OrderBy(x=>x.ScheduledAt).Select(x=>new{x.Id,x.MedicationId,x.ScheduledAt,HasLogs=db.MedicationLogs.Any(l=>l.MedicationId==id&&l.ScheduledAt==x.ScheduledAt),IsTaken=db.MedicationLogs.Any(l=>l.MedicationId==id&&l.ScheduledAt==x.ScheduledAt&&l.TakenAt!=null)}).ToListAsync()); }
 [HttpPost("medications/{id:guid}/occurrences")] public async Task<IActionResult> CreateMedicationOccurrence(Guid id,MedicationOccurrenceRequest r) { if(!await db.Medications.AnyAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId)))return NotFound();if(await db.MedicationOccurrences.AnyAsync(x=>x.MedicationId==id&&x.ScheduledAt==r.ScheduledAt))return Conflict("An occurrence already exists at that date and time.");var item=new MedicationOccurrence{MedicationId=id,ScheduledAt=r.ScheduledAt};db.Add(item);await db.SaveChangesAsync();return Ok(item); }
 [HttpPut("medication-occurrences/{id:guid}")] public async Task<IActionResult> UpdateMedicationOccurrence(Guid id,MedicationOccurrenceRequest r) { var item=await db.MedicationOccurrences.SingleOrDefaultAsync(x=>x.Id==id&&db.Medications.Any(m=>m.Id==x.MedicationId&&CircleIds.Contains(m.CareCircleId)));if(item is null)return NotFound();if(await db.MedicationLogs.AnyAsync(x=>x.MedicationId==item.MedicationId&&x.ScheduledAt==item.ScheduledAt))return Conflict("An occurrence with administration history cannot be edited.");if(await db.MedicationOccurrences.AnyAsync(x=>x.MedicationId==item.MedicationId&&x.Id!=id&&x.ScheduledAt==r.ScheduledAt))return Conflict("An occurrence already exists at that date and time.");item.ScheduledAt=r.ScheduledAt;await db.SaveChangesAsync();return Ok(item); }
 [HttpDelete("medication-occurrences/{id:guid}")] public async Task<IActionResult> DeleteMedicationOccurrence(Guid id) { var item=await db.MedicationOccurrences.SingleOrDefaultAsync(x=>x.Id==id&&db.Medications.Any(m=>m.Id==x.MedicationId&&CircleIds.Contains(m.CareCircleId)));if(item is null)return NotFound();if(await db.MedicationLogs.AnyAsync(x=>x.MedicationId==item.MedicationId&&x.ScheduledAt==item.ScheduledAt))return Conflict("An occurrence with administration history cannot be deleted.");db.Remove(item);await db.SaveChangesAsync();return Ok(new{deleted=true,id}); }
 [HttpGet("appointments")] public async Task<IActionResult> Appointments([FromQuery] Guid? careCircleId=null) { var q=db.Appointments.Where(x=>CircleIds.Contains(x.CareCircleId));if(careCircleId is not null)q=q.Where(x=>x.CareCircleId==careCircleId);return Ok(await q.OrderBy(x=>x.StartsAt).ToListAsync()); }
 [HttpGet("appointments/{id:guid}")] public async Task<IActionResult> AppointmentById(Guid id)
 {
  var item=await db.Appointments.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();
  var linkedTaskRows=await db.CareTasks.Where(x=>x.AppointmentId==id).OrderBy(x=>x.CreatedAt).Select(x=>new{x.Id,x.Title,x.Details,x.DueAt,x.AssignedToUserId,AssigneeName=x.AssignedToUserId==null?null:db.Users.Where(u=>u.Id==x.AssignedToUserId).Select(u=>u.DisplayName).FirstOrDefault(),x.Status}).ToListAsync();
  var linkedTasks=linkedTaskRows.Select(x=>new{x.Id,x.Title,x.Details,x.DueAt,x.AssignedToUserId,x.AssigneeName,Status=x.Status.ToString()}).ToList();
  var firstLinkedTask=linkedTasks.FirstOrDefault();
  return Ok(new{item.Id,item.CareCircleId,item.Title,item.Provider,item.StartsAt,item.Location,item.Notes,item.PreviousNotes,item.Status,item.CreatedAt,LinkedTaskId=firstLinkedTask?.Id,LinkedTaskStatus=firstLinkedTask?.Status,LinkedTasks=linkedTasks});
 }
 [HttpPost("appointments")] public async Task<IActionResult> CreateAppointment(CreateAppointmentRequest r) { if(!await IsMember(r.CareCircleId))return Forbid(); var item=new Appointment{CareCircleId=r.CareCircleId,Title=r.Title,Provider=r.Provider,StartsAt=r.StartsAt,Location=r.Location,Notes=r.Notes}; db.Add(item); await db.SaveChangesAsync(); return Ok(item); }
 [HttpPut("appointments/{id:guid}")] public async Task<IActionResult> UpdateAppointment(Guid id,CreateAppointmentRequest r)
 {
  var item=await db.Appointments.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();if(!await IsMember(r.CareCircleId))return Forbid();
  if(string.IsNullOrWhiteSpace(r.Title)||string.IsNullOrWhiteSpace(r.Provider))return BadRequest("Appointment title and provider are required.");
  var circleChanged=item.CareCircleId!=r.CareCircleId;
  var scheduleChanged=item.StartsAt.UtcDateTime!=r.StartsAt.UtcDateTime;
  var linkedTasks=await db.CareTasks.Where(x=>x.AppointmentId==id).ToListAsync();
  var linkedTasksNeedSync=linkedTasks.Any(x=>circleChanged||scheduleChanged||x.CareCircleId!=r.CareCircleId||x.DueAt.UtcDateTime!=r.StartsAt.UtcDateTime);
  if(circleChanged||scheduleChanged||linkedTasksNeedSync)
  {
   if(item.Status==AppointmentStatus.Completed)return Conflict("A completed appointment's recipient, date, and time cannot be changed.");
   if(linkedTasks.Any(x=>x.Status==CareTaskStatus.Done))return Conflict("A linked task is done, so its recipient, date, and time cannot be changed.");
   if(linkedTasks.Any(x=>x.Status==CareTaskStatus.Cancelled))return Conflict("A linked task is cancelled, so its recipient, date, and time cannot be changed.");
   if(circleChanged)foreach(var assignedId in linkedTasks.Where(x=>x.AssignedToUserId is not null).Select(x=>x.AssignedToUserId!.Value).Distinct())if(!await IsValidAssignee(r.CareCircleId,assignedId))return BadRequest("A linked task's assignee is not an active member of the new care circle. Reassign the task before changing the recipient.");
  }
  item.CareCircleId=r.CareCircleId;item.Title=r.Title.Trim();item.Provider=r.Provider.Trim();item.StartsAt=r.StartsAt;item.Location=r.Location?.Trim();item.Notes=r.Notes?.Trim();item.PreviousNotes=r.PreviousNotes?.Trim();foreach(var linkedTask in linkedTasks){linkedTask.CareCircleId=r.CareCircleId;linkedTask.DueAt=r.StartsAt;}await db.SaveChangesAsync();return Ok(item);
 }
 [HttpPut("appointments/{id:guid}/toggle-comments")] public async Task<IActionResult> ToggleAppointmentComments(Guid id)
 {
  var item=await db.Appointments.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();
  if(!string.IsNullOrWhiteSpace(item.Notes)){item.PreviousNotes=item.Notes;item.Notes=null;}else if(!string.IsNullOrWhiteSpace(item.PreviousNotes)){item.Notes=item.PreviousNotes;item.PreviousNotes=null;}else return Conflict("This appointment has no comments to clear or restore.");
  await db.SaveChangesAsync();return Ok(new{item.Id,item.Notes,HasRestorableNotes=item.PreviousNotes!=null});
 }
 [HttpDelete("appointments/{id:guid}")] public async Task<IActionResult> DeleteAppointment(Guid id)
 {
  var item=await db.Appointments.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();
  if(item.Status!=AppointmentStatus.Scheduled)return Conflict("Only scheduled appointments can be deleted.");
  db.Appointments.Remove(item);await db.SaveChangesAsync();return Ok(new{deleted=true,id});
 }
 [HttpPut("appointments/{id:guid}/toggle-done")] public async Task<IActionResult> ToggleAppointmentDone(Guid id)
 {
  var item=await db.Appointments.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();
  if(item.Status==AppointmentStatus.Cancelled)return Conflict("Cancelled appointments cannot be marked done.");
  if(item.Status==AppointmentStatus.Completed){item.Status=item.PreviousStatus??AppointmentStatus.Scheduled;item.PreviousStatus=null;}
  else{item.PreviousStatus=item.Status;item.Status=AppointmentStatus.Completed;}
  await db.SaveChangesAsync();return Ok(new{item.Id,Status=item.Status.ToString()});
 }
 [HttpGet("journal")] public async Task<IActionResult> Journal([FromQuery] Guid? careCircleId=null,[FromQuery] bool mine=false) { var q=db.JournalEntries.Where(x=>CircleIds.Contains(x.CareCircleId));if(careCircleId is not null)q=q.Where(x=>x.CareCircleId==careCircleId);if(mine)q=q.Where(x=>x.AuthorUserId==current.UserId);return Ok(await q.Join(db.Users,x=>x.AuthorUserId,x=>x.Id,(x,u)=>new{x.Id,x.CareCircleId,x.Body,x.Category,x.CreatedAt,Author=u.DisplayName}).OrderByDescending(x=>x.CreatedAt).ToListAsync()); }
 [HttpPost("journal")] public async Task<IActionResult> CreateJournal(CreateJournalEntryRequest r) { if(!await IsMember(r.CareCircleId))return Forbid(); if(string.IsNullOrWhiteSpace(r.Body))return BadRequest("Journal text is required."); var item=new JournalEntry{CareCircleId=r.CareCircleId,AuthorUserId=current.UserId,Body=r.Body.Trim(),Category=r.Category?.Trim()};db.Add(item);await db.SaveChangesAsync();return Ok(item); }
 [HttpGet("health-readings")] public async Task<IActionResult> HealthReadings([FromQuery] Guid? careCircleId=null) { var q=db.HealthReadings.Where(x=>CircleIds.Contains(x.CareCircleId));if(careCircleId is not null)q=q.Where(x=>x.CareCircleId==careCircleId);return Ok(await q.OrderByDescending(x=>x.RecordedAt).ToListAsync()); }
 [HttpPost("health-readings")] public async Task<IActionResult> CreateHealthReading(CreateHealthReadingRequest r) { if(!await IsMember(r.CareCircleId))return Forbid(); var item=new HealthReading{CareCircleId=r.CareCircleId,Type=r.Type.Trim(),Value=r.Value,SecondaryValue=r.SecondaryValue,Pulse=r.Pulse,RecordedAt=r.RecordedAt};db.Add(item);await db.SaveChangesAsync();return Ok(item); }
 [HttpGet("documents")] public async Task<IActionResult> Documents([FromQuery] Guid? careCircleId=null,[FromQuery] Guid? taskId=null,[FromQuery] Guid? appointmentId=null,[FromQuery] Guid? medicationId=null)
 {
  var q=db.CareDocuments.Where(x=>CircleIds.Contains(x.CareCircleId));if(careCircleId is not null)q=q.Where(x=>x.CareCircleId==careCircleId);if(taskId is not null)q=q.Where(x=>x.TaskId==taskId);if(appointmentId is not null)q=q.Where(x=>x.AppointmentId==appointmentId);if(medicationId is not null)q=q.Where(x=>x.MedicationId==medicationId);
  return Ok(await q.OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Id,x.CareCircleId,x.TaskId,x.AppointmentId,x.MedicationId,x.Name,x.Category,x.Notes,x.ContentType,HasContent=x.FileContent!=null,x.CreatedAt}).ToListAsync());
 }
 [HttpGet("documents/{id:guid}")] public async Task<IActionResult> DocumentById(Guid id)
 {
  var item=await db.CareDocuments.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();return Ok(new{item.Id,item.Name,item.Category,item.Notes,item.ContentType,ContentBase64=item.FileContent==null?null:Convert.ToBase64String(item.FileContent),item.CreatedAt});
 }
 [HttpDelete("documents/{id:guid}")] public async Task<IActionResult> DeleteDocument(Guid id)
 {
  var item=await db.CareDocuments.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(item is null)return NotFound();db.CareDocuments.Remove(item);await db.SaveChangesAsync();return Ok(new{Deleted=true,id});
 }
 [HttpPost("documents")] public async Task<IActionResult> CreateDocument(CreateDocumentRequest r)
 {
  if(!await IsMember(r.CareCircleId))return Forbid();if(string.IsNullOrWhiteSpace(r.Name))return BadRequest("Document name is required.");if(new[]{r.TaskId,r.AppointmentId,r.MedicationId}.Count(x=>x is not null)>1)return BadRequest("A document can be linked to only one care item.");
  if(r.TaskId is not null&&!await db.CareTasks.AnyAsync(x=>x.Id==r.TaskId&&x.CareCircleId==r.CareCircleId))return BadRequest("The task must belong to the selected care recipient.");
  if(r.AppointmentId is not null&&!await db.Appointments.AnyAsync(x=>x.Id==r.AppointmentId&&x.CareCircleId==r.CareCircleId))return BadRequest("The appointment must belong to the selected care recipient.");
  if(r.MedicationId is not null&&!await db.Medications.AnyAsync(x=>x.Id==r.MedicationId&&x.CareCircleId==r.CareCircleId))return BadRequest("The medication must belong to the selected care recipient.");
  byte[]? content=null;if(!string.IsNullOrWhiteSpace(r.ContentBase64)){try{content=Convert.FromBase64String(r.ContentBase64);}catch(FormatException){return BadRequest("The uploaded document is not valid base64 content.");}if(content.Length>10*1024*1024)return BadRequest("Documents are limited to 10 MB.");}
  var item=new CareDocument{CareCircleId=r.CareCircleId,TaskId=r.TaskId,AppointmentId=r.AppointmentId,MedicationId=r.MedicationId,Name=r.Name.Trim(),Category=string.IsNullOrWhiteSpace(r.Category)?"Attachment":r.Category.Trim(),Notes=r.Notes?.Trim(),ContentType=r.ContentType?.Trim(),FileContent=content};db.Add(item);await db.SaveChangesAsync();return Ok(new{item.Id,item.Name,item.ContentType,item.CreatedAt});
 }
 [HttpGet("emergency-contacts")] public async Task<IActionResult> EmergencyContacts([FromQuery] Guid? careCircleId=null) { var q=db.EmergencyContacts.Where(x=>CircleIds.Contains(x.CareCircleId));if(careCircleId is not null)q=q.Where(x=>x.CareCircleId==careCircleId);return Ok(await q.OrderByDescending(x=>x.IsPrimary).ThenBy(x=>x.Name).ToListAsync()); }
 [HttpPost("emergency-contacts")] public async Task<IActionResult> CreateEmergencyContact(CreateEmergencyContactRequest r) { if(!await IsMember(r.CareCircleId))return Forbid();var item=new EmergencyContact{CareCircleId=r.CareCircleId,Name=r.Name.Trim(),Relationship=r.Relationship.Trim(),Phone=r.Phone.Trim(),IsPrimary=r.IsPrimary};db.Add(item);await db.SaveChangesAsync();return Ok(item); }
 [HttpGet("conversations")] public async Task<IActionResult> Conversations([FromQuery] Guid? careCircleId=null) { var q=db.Conversations.Where(x=>CircleIds.Contains(x.CareCircleId));if(careCircleId is not null)q=q.Where(x=>x.CareCircleId==careCircleId);return Ok(await q.Include(x=>x.Messages.OrderByDescending(m=>m.CreatedAt).Take(1)).OrderBy(x=>x.Title).ToListAsync()); }
 [HttpPost("conversations")] public async Task<IActionResult> CreateConversation(CreateConversationRequest r) { if(!await IsMember(r.CareCircleId))return Forbid();if(string.IsNullOrWhiteSpace(r.Title))return BadRequest("Conversation title is required.");var item=new Conversation{CareCircleId=r.CareCircleId,Title=r.Title.Trim()};db.Add(item);await db.SaveChangesAsync();return Ok(item); }
 [HttpGet("conversations/{id:guid}/messages")] public async Task<IActionResult> Messages(Guid id) { var conversation=await db.Conversations.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId)); return conversation is null?NotFound():Ok(await db.Messages.Where(x=>x.ConversationId==id).OrderBy(x=>x.CreatedAt).ToListAsync()); }
 [HttpPost("messages")] public async Task<IActionResult> SendMessage(SendMessageRequest r) { var conversation=await db.Conversations.SingleOrDefaultAsync(x=>x.Id==r.ConversationId&&CircleIds.Contains(x.CareCircleId)); if(conversation is null)return NotFound(); var item=new Message{ConversationId=r.ConversationId,SenderUserId=current.UserId,Body=r.Body}; db.Add(item); await db.SaveChangesAsync(); return Ok(item); }
 [HttpGet("handoffs")] public async Task<IActionResult> Handoffs([FromQuery] Guid? careCircleId=null,[FromQuery] bool mine=false) { var q=db.CareHandoffs.Where(x=>CircleIds.Contains(x.CareCircleId));if(careCircleId is not null)q=q.Where(x=>x.CareCircleId==careCircleId);if(mine)q=q.Where(x=>x.FromUserId==current.UserId);return Ok(await q.OrderByDescending(x=>x.CreatedAt).Select(x=>new{x.Id,x.CareCircleId,RecipientName=db.CareCircles.Where(c=>c.Id==x.CareCircleId).Select(c=>c.RecipientName).First(),x.FromUserId,FromName=db.Users.Where(u=>u.Id==x.FromUserId).Select(u=>u.DisplayName).First(),x.ToUserId,ToName=db.Users.Where(u=>u.Id==x.ToUserId).Select(u=>u.DisplayName).First(),x.Summary,x.Risks,x.NextSteps,x.CoverageStartsAt,x.CoverageEndsAt,Status=(int)x.Status,StatusName=x.Status.ToString(),x.CreatedAt,x.AcceptedAt,x.ActivatedAt,x.CompletedAt,x.DeclinedAt,x.CancelledAt,x.CompletionNotes,x.UnresolvedItems,x.CarriedForwardFromId,CanEdit=x.FromUserId==current.UserId&&(x.Status==HandoffStatus.Draft||x.Status==HandoffStatus.Sent),CanCancel=x.FromUserId==current.UserId&&(x.Status==HandoffStatus.Draft||x.Status==HandoffStatus.Sent),CanRespond=x.ToUserId==current.UserId&&x.Status==HandoffStatus.Sent,CanActivate=x.ToUserId==current.UserId&&x.Status==HandoffStatus.Accepted,CanComplete=x.ToUserId==current.UserId&&x.Status==HandoffStatus.Active,CanCarryForward=x.Status==HandoffStatus.Completed&&!string.IsNullOrEmpty(x.UnresolvedItems)}).ToListAsync()); }
 [HttpGet("handoffs/{id:guid}")] public async Task<IActionResult> Handoff(Guid id){var item=await HandoffView(id);return item is null?NotFound():Ok(item);}
 [HttpPost("handoffs")] public async Task<IActionResult> CreateHandoff(CreateHandoffRequest r) { if(!await IsMember(r.CareCircleId))return Forbid();var error=await ValidateHandoff(r.CareCircleId,r.ToUserId,r.Summary,r.CoverageStartsAt,r.CoverageEndsAt);if(error is not null)return BadRequest(error);var item=new CareHandoff{CareCircleId=r.CareCircleId,FromUserId=current.UserId,ToUserId=r.ToUserId,Summary=r.Summary.Trim(),Risks=r.Risks?.Trim(),NextSteps=r.NextSteps?.Trim(),CoverageStartsAt=r.CoverageStartsAt,CoverageEndsAt=r.CoverageEndsAt,CarriedForwardFromId=r.CarriedForwardFromId,Status=r.Send?HandoffStatus.Sent:HandoffStatus.Draft};db.Add(item);if(r.Send)AddHandoffNotification(item,"Handoff ready for review",$"Review and acknowledge the care handoff: {item.Summary}");await db.SaveChangesAsync();var delivery=r.Send?await DeliverHandoffEmail(item):new InvitationDeliveryResult(false,"Saved as draft.");return Ok(new{item.Id,Status=(int)item.Status,StatusName=item.Status.ToString(),notificationDelivery=delivery}); }
 [HttpPut("handoffs/{id:guid}")] public async Task<IActionResult> UpdateHandoff(Guid id,UpdateHandoffRequest r){var item=await db.CareHandoffs.SingleOrDefaultAsync(x=>x.Id==id&&x.FromUserId==current.UserId);if(item is null)return NotFound();if(item.Status is not (HandoffStatus.Draft or HandoffStatus.Sent))return Conflict("Only draft or review-pending handoffs can be edited.");var error=await ValidateHandoff(item.CareCircleId,r.ToUserId,r.Summary,r.CoverageStartsAt,r.CoverageEndsAt);if(error is not null)return BadRequest(error);var wasDraft=item.Status==HandoffStatus.Draft;item.ToUserId=r.ToUserId;item.Summary=r.Summary.Trim();item.Risks=r.Risks?.Trim();item.NextSteps=r.NextSteps?.Trim();item.CoverageStartsAt=r.CoverageStartsAt;item.CoverageEndsAt=r.CoverageEndsAt;item.Status=r.Send?HandoffStatus.Sent:HandoffStatus.Draft;if(r.Send){AddHandoffNotification(item,wasDraft?"Handoff ready for review":"Care handoff updated",$"A care handoff for you was {(wasDraft?"sent for review":"updated")}: {item.Summary}");}await db.SaveChangesAsync();var delivery=r.Send?await DeliverHandoffEmail(item):new InvitationDeliveryResult(false,"Saved as draft.");return Ok(new{item.Id,Status=(int)item.Status,StatusName=item.Status.ToString(),notificationDelivery=delivery});}
 [HttpPut("handoffs/{id:guid}/accept")] public async Task<IActionResult> AcceptHandoff(Guid id,AcceptHandoffRequest r) { var item=await db.CareHandoffs.SingleOrDefaultAsync(x=>x.Id==id&&x.ToUserId==current.UserId);if(item is null)return NotFound();if(item.Status!=HandoffStatus.Sent)return Conflict("Only a handoff awaiting review can be accepted.");if(!(r.MedicationReviewed&&r.SafetyReviewed&&r.ScheduleReviewed&&r.EmergencyPlanReviewed&&r.InstructionsReviewed))return BadRequest("Review and confirm every required handoff item before accepting.");item.MedicationReviewed=r.MedicationReviewed;item.SafetyReviewed=r.SafetyReviewed;item.ScheduleReviewed=r.ScheduleReviewed;item.EmergencyPlanReviewed=r.EmergencyPlanReviewed;item.InstructionsReviewed=r.InstructionsReviewed;item.Status=HandoffStatus.Accepted;item.AcceptedAt=DateTimeOffset.UtcNow;AddHandoffNotification(item,"Handoff accepted",$"Your care handoff was reviewed and accepted: {item.Summary}",item.FromUserId);await db.SaveChangesAsync();return Ok(await HandoffView(id)); }
 [HttpPut("handoffs/{id:guid}/activate")] public async Task<IActionResult> ActivateHandoff(Guid id){var item=await db.CareHandoffs.SingleOrDefaultAsync(x=>x.Id==id&&x.ToUserId==current.UserId);if(item is null)return NotFound();if(item.Status!=HandoffStatus.Accepted)return Conflict("Only an accepted handoff can start active coverage.");item.Status=HandoffStatus.Active;item.ActivatedAt=DateTimeOffset.UtcNow;AddHandoffNotification(item,"Care coverage started",$"{await UserName(current.UserId)} started the accepted handoff: {item.Summary}",item.FromUserId);await db.SaveChangesAsync();return Ok(await HandoffView(id));}
 [HttpPut("handoffs/{id:guid}/complete")] public async Task<IActionResult> CompleteHandoff(Guid id,CompleteHandoffRequest r){var item=await db.CareHandoffs.SingleOrDefaultAsync(x=>x.Id==id&&x.ToUserId==current.UserId);if(item is null)return NotFound();if(item.Status!=HandoffStatus.Active)return Conflict("Only active care coverage can be completed.");item.Status=HandoffStatus.Completed;item.CompletedAt=DateTimeOffset.UtcNow;item.CompletionNotes=r.CompletionNotes?.Trim();item.UnresolvedItems=r.UnresolvedItems?.Trim();AddHandoffNotification(item,"Handoff completed",string.IsNullOrWhiteSpace(item.UnresolvedItems)?$"Care coverage was completed: {item.Summary}":$"Care coverage was completed with unresolved items: {item.Summary}",item.FromUserId);await db.SaveChangesAsync();return Ok(await HandoffView(id));}
 [HttpPost("handoffs/{id:guid}/carry-forward")] public async Task<IActionResult> CarryForwardHandoff(Guid id,CarryForwardHandoffRequest r){var source=await db.CareHandoffs.SingleOrDefaultAsync(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId));if(source is null)return NotFound();if(source.Status!=HandoffStatus.Completed||string.IsNullOrWhiteSpace(source.UnresolvedItems))return Conflict("Only unresolved items from a completed handoff can be carried forward.");var toUserId=r.ToUserId??source.FromUserId;var error=await ValidateHandoff(source.CareCircleId,toUserId,source.UnresolvedItems,r.CoverageStartsAt,r.CoverageEndsAt);if(error is not null)return BadRequest(error);var item=new CareHandoff{CareCircleId=source.CareCircleId,FromUserId=current.UserId,ToUserId=toUserId,Summary=$"Follow-up: {source.Summary}",Risks=source.Risks,NextSteps=source.UnresolvedItems,CoverageStartsAt=r.CoverageStartsAt,CoverageEndsAt=r.CoverageEndsAt,CarriedForwardFromId=source.Id,Status=HandoffStatus.Draft};db.Add(item);await db.SaveChangesAsync();return Ok(await HandoffView(item.Id));}
 [HttpPut("handoffs/{id:guid}/decline")] public async Task<IActionResult> DeclineHandoff(Guid id){var item=await db.CareHandoffs.SingleOrDefaultAsync(x=>x.Id==id&&x.ToUserId==current.UserId);if(item is null)return NotFound();if(item.Status!=HandoffStatus.Sent)return Conflict("Only a sent handoff can be declined.");item.Status=HandoffStatus.Declined;item.DeclinedAt=DateTimeOffset.UtcNow;AddHandoffNotification(item,"Handoff declined",$"Your care handoff was declined: {item.Summary}",item.FromUserId);await db.SaveChangesAsync();return Ok(await HandoffView(id));}
 [HttpPut("handoffs/{id:guid}/cancel")] public async Task<IActionResult> CancelHandoff(Guid id){var item=await db.CareHandoffs.SingleOrDefaultAsync(x=>x.Id==id&&x.FromUserId==current.UserId);if(item is null)return NotFound();if(item.Status is not (HandoffStatus.Draft or HandoffStatus.Sent))return Conflict("Only a draft or sent handoff can be cancelled.");item.Status=HandoffStatus.Cancelled;item.CancelledAt=DateTimeOffset.UtcNow;if(item.ToUserId!=current.UserId)AddHandoffNotification(item,"Handoff cancelled",$"A care handoff was cancelled: {item.Summary}");await db.SaveChangesAsync();return Ok(await HandoffView(id));}
 [HttpGet("notifications")] public async Task<IActionResult> Notifications()=>Ok(await db.UserNotifications.Where(x=>x.UserId==current.UserId&&x.ReadAt==null).OrderByDescending(x=>x.CreatedAt).ToListAsync());
 [HttpPut("notifications/{id:guid}/read")] public async Task<IActionResult> ReadNotification(Guid id){var item=await db.UserNotifications.SingleOrDefaultAsync(x=>x.Id==id&&x.UserId==current.UserId);if(item is null)return NotFound();item.ReadAt=DateTimeOffset.UtcNow;await db.SaveChangesAsync();return Ok(new{item.Id,item.ReadAt});}
 private async Task<object?> HandoffView(Guid id)
 {
  var handoff=await db.CareHandoffs.Where(x=>x.Id==id&&CircleIds.Contains(x.CareCircleId)).Select(x=>new{x.Id,x.CareCircleId,RecipientName=db.CareCircles.Where(c=>c.Id==x.CareCircleId).Select(c=>c.RecipientName).First(),x.FromUserId,FromName=db.Users.Where(u=>u.Id==x.FromUserId).Select(u=>u.DisplayName).First(),x.ToUserId,ToName=db.Users.Where(u=>u.Id==x.ToUserId).Select(u=>u.DisplayName).First(),x.Summary,x.Risks,x.NextSteps,x.CoverageStartsAt,x.CoverageEndsAt,Status=(int)x.Status,StatusName=x.Status.ToString(),x.CreatedAt,x.MedicationReviewed,x.SafetyReviewed,x.ScheduleReviewed,x.EmergencyPlanReviewed,x.InstructionsReviewed,x.AcceptedAt,x.ActivatedAt,x.CompletedAt,x.DeclinedAt,x.CancelledAt,x.CompletionNotes,x.UnresolvedItems,x.CarriedForwardFromId,CanEdit=x.FromUserId==current.UserId&&(x.Status==HandoffStatus.Draft||x.Status==HandoffStatus.Sent),CanCancel=x.FromUserId==current.UserId&&(x.Status==HandoffStatus.Draft||x.Status==HandoffStatus.Sent),CanRespond=x.ToUserId==current.UserId&&x.Status==HandoffStatus.Sent,CanActivate=x.ToUserId==current.UserId&&x.Status==HandoffStatus.Accepted,CanComplete=x.ToUserId==current.UserId&&x.Status==HandoffStatus.Active,CanCarryForward=x.Status==HandoffStatus.Completed&&!string.IsNullOrEmpty(x.UnresolvedItems)}).FirstOrDefaultAsync();
  if(handoff is null)return null;var start=handoff.CoverageStartsAt??handoff.CreatedAt;var end=handoff.CoverageEndsAt??start.AddDays(1);
  var tasks=await db.CareTasks.Where(x=>x.CareCircleId==handoff.CareCircleId&&x.Status!=CareTaskStatus.Done&&x.Status!=CareTaskStatus.Cancelled&&x.DueAt>=start&&x.DueAt<=end).OrderBy(x=>x.DueAt).Select(x=>new{x.Id,x.Title,x.DueAt,StatusName=x.Status.ToString()}).ToListAsync();
  var appointments=await db.Appointments.Where(x=>x.CareCircleId==handoff.CareCircleId&&x.Status==AppointmentStatus.Scheduled&&x.StartsAt>=start&&x.StartsAt<=end).OrderBy(x=>x.StartsAt).Select(x=>new{x.Id,x.Title,x.Provider,x.StartsAt}).ToListAsync();
  var medications=await db.Medications.Where(x=>x.CareCircleId==handoff.CareCircleId&&x.Status==MedicationStatus.Active).OrderBy(x=>x.Name).Select(x=>new{x.Id,x.Name,x.Dosage,x.Schedule,x.Instructions}).ToListAsync();
  return new{handoff.Id,handoff.CareCircleId,handoff.RecipientName,handoff.FromUserId,handoff.FromName,handoff.ToUserId,handoff.ToName,handoff.Summary,handoff.Risks,handoff.NextSteps,handoff.CoverageStartsAt,handoff.CoverageEndsAt,handoff.Status,handoff.StatusName,handoff.CreatedAt,handoff.MedicationReviewed,handoff.SafetyReviewed,handoff.ScheduleReviewed,handoff.EmergencyPlanReviewed,handoff.InstructionsReviewed,handoff.AcceptedAt,handoff.ActivatedAt,handoff.CompletedAt,handoff.DeclinedAt,handoff.CancelledAt,handoff.CompletionNotes,handoff.UnresolvedItems,handoff.CarriedForwardFromId,handoff.CanEdit,handoff.CanCancel,handoff.CanRespond,handoff.CanActivate,handoff.CanComplete,handoff.CanCarryForward,CareBrief=new{Tasks=tasks,Appointments=appointments,Medications=medications}};
 }
 private void AddHandoffNotification(CareHandoff item,string title,string body,Guid? userId=null)=>db.UserNotifications.Add(new(){UserId=userId??item.ToUserId,Type="handoff",Title=title,Body=body,RelatedId=item.Id});
 private async Task<InvitationDeliveryResult> DeliverHandoffEmail(CareHandoff item){var recipient=await db.Users.Where(x=>x.Id==item.ToUserId).Select(x=>new{x.Email,x.DisplayName}).SingleAsync();var sender=await db.Users.Where(x=>x.Id==item.FromUserId).Select(x=>x.DisplayName).SingleAsync();var careRecipient=await db.CareCircles.Where(x=>x.Id==item.CareCircleId).Select(x=>x.RecipientName).SingleAsync();return await invitationDelivery.SendAsync("Email",recipient.Email,$"CareTogether handoff for {careRecipient}",$"Hello {recipient.DisplayName},\n\n{sender} sent you a CareTogether handoff for {careRecipient}.\n\nSummary: {item.Summary}\nRisks: {item.Risks??"None specified"}\nNext steps: {item.NextSteps??"None specified"}\n\nOpen CareTogether to accept or decline it.");}
 private async Task<string?> ValidateHandoff(Guid circleId,Guid toUserId,string summary,DateTimeOffset? startsAt,DateTimeOffset? endsAt){if(string.IsNullOrWhiteSpace(summary))return "A handoff summary is required.";if(toUserId==current.UserId)return "Choose another active caregiver to receive the handoff.";if(!await db.CareTeamMembers.AnyAsync(x=>x.CareCircleId==circleId&&x.UserId==toUserId&&x.IsActive))return "Choose an active caregiver in this Care Circle.";if(startsAt is not null&&endsAt is not null&&endsAt<=startsAt)return "Coverage must end after it starts.";return null;}
 private async Task<string> UserName(Guid id)=>await db.Users.Where(x=>x.Id==id).Select(x=>x.DisplayName).SingleAsync();
 private Task<bool> IsMember(Guid id)=>db.CareTeamMembers.AnyAsync(x=>x.CareCircleId==id&&x.UserId==current.UserId&&x.IsActive);
 private Task<bool> IsValidAssignee(Guid circleId,Guid? userId)=>userId is null?Task.FromResult(true):db.CareTeamMembers.AnyAsync(x=>x.CareCircleId==circleId&&x.UserId==userId&&x.IsActive);
 private static CareTaskStatus NormalizeStatus(CareTaskStatus status,Guid? assigneeId)=>status==CareTaskStatus.Assigned&&assigneeId is null?CareTaskStatus.Open:status;
 private static string? MedicationScheduleError(CreateMedicationRequest r)
 {
  if(string.IsNullOrWhiteSpace(r.Schedule))return "A medication schedule is required.";
  if(r.ScheduleType==MedicationScheduleType.Once)return r.OnceAt is null?"Select the one-time medication date and time.":null;
  if(r.Occurrences is <1 or >6)return "Occurrences must be between 1 and 6.";
  if(string.IsNullOrWhiteSpace(r.TimesOfDay)||r.TimesOfDay.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Length!=r.Occurrences)return "Enter one time of day for each occurrence.";
  if(r.StartDate is null||r.EndDate is null)return "Select the schedule start and end dates.";
  if(r.EndDate<r.StartDate)return "The schedule end date cannot be before the start date.";if(r.EndDate>r.StartDate.Value.AddYears(5))return "Medication schedules are limited to five years.";
  if(r.ScheduleType==MedicationScheduleType.Weekly&&r.WeeklyDay is null)return "Select a day of the week.";
  if(r.ScheduleType==MedicationScheduleType.Monthly&&r.MonthlyDay is (<1 or >31) or null)return "Select a day of the month from 1 through 31.";
  return null;
 }
 private static void ApplySchedule(Medication item,CreateMedicationRequest r){item.ScheduleType=r.ScheduleType;item.OnceAt=r.OnceAt;item.Occurrences=r.Occurrences;item.TimesOfDay=r.TimesOfDay;item.StartDate=r.StartDate;item.EndDate=r.EndDate;item.WeeklyDay=r.WeeklyDay;item.MonthlyDay=r.MonthlyDay;item.ScheduleUtcOffsetMinutes=Math.Clamp(r.ScheduleUtcOffsetMinutes,-840,840);}
 private static IEnumerable<MedicationOccurrence> GenerateMedicationOccurrences(Medication item)
 {
  if(item.ScheduleType==MedicationScheduleType.Once&&item.OnceAt is DateTimeOffset once)return[new MedicationOccurrence{MedicationId=item.Id,ScheduledAt=once}];if(item.StartDate is not DateOnly start||item.EndDate is not DateOnly end||string.IsNullOrWhiteSpace(item.TimesOfDay))return[];var times=item.TimesOfDay.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(x=>TimeSpan.TryParse(x,out var value)?value:(TimeSpan?)null).Where(x=>x is not null).Select(x=>x!.Value).ToList();var offset=TimeSpan.FromMinutes(item.ScheduleUtcOffsetMinutes);var result=new List<MedicationOccurrence>();for(var date=start;date<=end;date=date.AddDays(1)){var applies=item.ScheduleType==MedicationScheduleType.Daily||(item.ScheduleType==MedicationScheduleType.Weekly&&item.WeeklyDay==date.DayOfWeek)||(item.ScheduleType==MedicationScheduleType.Monthly&&item.MonthlyDay==date.Day);if(applies)foreach(var time in times)result.Add(new MedicationOccurrence{MedicationId=item.Id,ScheduledAt=new DateTimeOffset(date.ToDateTime(TimeOnly.FromTimeSpan(time)),offset)});}return result;
 }
 private static bool MedicationDueInRange(Medication item,DateTimeOffset rangeStart,DateTimeOffset rangeEnd)
 {
  if(item.ScheduleType==MedicationScheduleType.Once)return item.OnceAt>=rangeStart&&item.OnceAt<rangeEnd;
  var first=DateOnly.FromDateTime(rangeStart.Date);var last=DateOnly.FromDateTime(rangeEnd.AddTicks(-1).Date);if(item.StartDate is DateOnly starts&&starts>first)first=starts;if(item.EndDate is DateOnly ends&&ends<last)last=ends;if(first>last)return false;
  if(item.ScheduleType==MedicationScheduleType.Daily)return true;
  for(var date=first;date<=last;date=date.AddDays(1)){if(item.ScheduleType==MedicationScheduleType.Weekly&&item.WeeklyDay==(DayOfWeek)date.DayOfWeek)return true;if(item.ScheduleType==MedicationScheduleType.Monthly&&item.MonthlyDay==date.Day)return true;}
  return false;
 }
 private static bool MedicationMissed(Medication item,DateTimeOffset now,IEnumerable<MedicationLog> logs)
 {
  var administered=logs.Where(x=>x.TakenAt is not null).ToList();if(item.ScheduleType==MedicationScheduleType.Once)return item.OnceAt is DateTimeOffset once&&once<now&&!administered.Any(x=>Math.Abs((x.ScheduledAt-once).TotalMinutes)<2);
  if(string.IsNullOrWhiteSpace(item.TimesOfDay))return false;var times=item.TimesOfDay.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(x=>TimeSpan.TryParse(x,out var value)?value:(TimeSpan?)null).Where(x=>x is not null).Select(x=>x!.Value).ToList();if(times.Count==0)return false;
  var today=DateOnly.FromDateTime(now.Date);var first=item.StartDate??today.AddDays(-30);if(first<today.AddDays(-30))first=today.AddDays(-30);var last=item.EndDate is DateOnly end&&end<today?end:today;
  for(var date=first;date<=last;date=date.AddDays(1)){var scheduledDay=item.ScheduleType==MedicationScheduleType.Daily||(item.ScheduleType==MedicationScheduleType.Weekly&&item.WeeklyDay==date.DayOfWeek)||(item.ScheduleType==MedicationScheduleType.Monthly&&item.MonthlyDay==date.Day);if(!scheduledDay)continue;foreach(var time in times){var local=new DateTimeOffset(date.ToDateTime(TimeOnly.FromTimeSpan(time)),now.Offset);if(local>=now)continue;if(!administered.Any(x=>Math.Abs((x.ScheduledAt-local).TotalMinutes)<2))return true;}}
  return false;
 }
 private Task<bool> CanManage(Guid id)=>db.CareTeamMembers.AnyAsync(x=>x.CareCircleId==id&&x.UserId==current.UserId&&x.IsActive&&x.Role==MemberRole.Admin);
 private async Task<(int Messages,int Tasks,int Handoffs,int Total)> InteractionCounts(Guid circleId,Guid userId)
 {
  var messages=await db.Messages.CountAsync(x=>x.SenderUserId==userId&&db.Conversations.Any(c=>c.Id==x.ConversationId&&c.CareCircleId==circleId));
  var tasks=await db.CareTasks.CountAsync(x=>x.CareCircleId==circleId&&x.AssignedToUserId==userId);
  var handoffs=await db.CareHandoffs.CountAsync(x=>x.CareCircleId==circleId&&(x.FromUserId==userId||x.ToUserId==userId));
  return(messages,tasks,handoffs,messages+tasks+handoffs);
 }
}
